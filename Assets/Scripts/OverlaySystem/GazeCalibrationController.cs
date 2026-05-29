using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Manages the GazeCalibrationScene: launches the Python bridge in unity-calibrate mode,
/// receives CALI_* UDP messages, drives the calibration dot UI, and returns to the overlay
/// scene once the SVR model is saved.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GazeCalibrationController : MonoBehaviour
{
    [Tooltip("Scene to load after calibration finishes.")]
    [SerializeField] private string returnSceneName = "OverlayScene";

    [Tooltip("Path to the Python bridge exe, relative to StreamingAssets.")]
    [SerializeField] private string relativeExePath = @"Multimodal_UDP/unity_gaze_bridge.exe";

    [Tooltip("Optional explicit camera reference. Leave empty to use Camera.main.")]
    [SerializeField] private Camera sceneCamera;

    // ── UI references ────────────────────────────────────────────────────────
    private VisualElement dot;
    private VisualElement progressFill;
    private Label statusLabel;
    private Label pointLabel;
    private VisualElement fittingOverlay;

    // ── Dot animation ─────────────────────────────────────────────────────────
    private float dotTargetX;
    private float dotTargetY;
    private float dotCurrentX;
    private float dotCurrentY;
    private bool dotVisible;
    private bool firstPointReceived;

    // ── Singleton ─────────────────────────────────────────────────────────────
    public static GazeCalibrationController Instance { get; private set; }

    // ── Process / network ────────────────────────────────────────────────────
    private Process calibrationProcess;
    private UdpClient udpClient;
    private CancellationTokenSource udpCancellation;
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    // ── Per-profile calibration path ─────────────────────────────────────────
    private string calibrationProfileId;

    // ──────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;

        if (sceneCamera == null)
            sceneCamera = Camera.main;

        if (sceneCamera != null)
        {
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color(0.85f, 0.94f, 0.86f, 1f);
        }

#if !UNITY_EDITOR
        WindowManager.MakeFullscreen();
        WindowManager.SetClickThrough(false);
#endif
    }

    private void Start()
    {
        var uiDoc = GetComponent<UIDocument>();
        var root = uiDoc.rootVisualElement;

        dot          = root.Q<VisualElement>("calibration-dot");
        progressFill = root.Q<VisualElement>("progress-fill");
        statusLabel  = root.Q<Label>("status-label");
        pointLabel   = root.Q<Label>("point-label");
        fittingOverlay = root.Q<VisualElement>("fitting-overlay");

        if (fittingOverlay != null)
            fittingOverlay.style.display = DisplayStyle.None;

        if (dot != null)
            dot.style.opacity = 0;

        // Signal GazeFollowerRunner to run the camera check before calibration begins.
        // The runner will call StartCalibration() once the user confirms the camera.
        GazeFollowerRunner.Instance?.OnCalibrationSceneReady(this);
    }

    /// <summary>
    /// Called by GazeFollowerRunner after the camera check panel is confirmed.
    /// Stores the profile ID and launches the Python calibration bridge.
    /// </summary>
    public void StartCalibration(string profileId = null)
    {
        calibrationProfileId = profileId;
        StartCoroutine(LaunchBridge());
    }

    private void Update()
    {
        // Lerp dot toward target position
        if (dotVisible && dot != null)
        {
            dotCurrentX = Mathf.Lerp(dotCurrentX, dotTargetX, Time.deltaTime * 8f);
            dotCurrentY = Mathf.Lerp(dotCurrentY, dotTargetY, Time.deltaTime * 8f);

            dot.style.left = Length.Percent(dotCurrentX * 100f);
            dot.style.top  = Length.Percent(dotCurrentY * 100f);
        }

        // Drain UDP messages on main thread
        while (messageQueue.TryDequeue(out string msg))
            HandleMessage(msg);
    }

    // ── Bridge launch ─────────────────────────────────────────────────────────

    private IEnumerator LaunchBridge()
    {
        string exePath = Path.Combine(Application.streamingAssetsPath, relativeExePath)
                             .Replace("/", "\\");

        if (!File.Exists(exePath))
        {
            SetStatus($"Bridge executable not found:\n{exePath}\n\nPlease run PyInstaller and copy the build to StreamingAssets.");
            yield break;
        }

        // Open a dynamic UDP port for receiving messages from Python
        udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int assignedPort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;

        udpCancellation = new CancellationTokenSource();
        _ = Task.Run(() => ReceiveLoop(udpCancellation.Token));

        var startInfo = new ProcessStartInfo
        {
            FileName         = exePath,
            Arguments        = $"--port {assignedPort} --mode unity-calibrate"
                               + (string.IsNullOrEmpty(calibrationProfileId) ? "" : $" --profile-id \"{calibrationProfileId}\""),
            WorkingDirectory = Path.GetDirectoryName(exePath),
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        try
        {
            calibrationProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            calibrationProcess.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    RobitLogger.Log($"[GazeCalib][PY] {e.Data}");
            };
            calibrationProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    RobitLogger.LogWarning($"[GazeCalib][PY-ERR] {e.Data}");
            };

            calibrationProcess.Start();
            calibrationProcess.BeginOutputReadLine();
            calibrationProcess.BeginErrorReadLine();

            SetStatus("Initializing gaze model…\nThis may take a few seconds.");
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to start calibration bridge:\n{ex.Message}");
        }
    }

    // ── UDP receive loop (background task) ───────────────────────────────────

    private async Task ReceiveLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await udpClient.ReceiveAsync();
                string msg = Encoding.UTF8.GetString(result.Buffer).Trim();
                messageQueue.Enqueue(msg);
            }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                RobitLogger.LogWarning($"[GazeCalibrationController] UDP receive error: {ex.Message}");
            }
        }
    }

    // ── Message handling (main thread, called from Update) ───────────────────

    private void HandleMessage(string msg)
    {
        if (msg.StartsWith("CALI_SHOW_POINT ", StringComparison.Ordinal))
        {
            // Format: CALI_SHOW_POINT {x},{y} {pointIndex} {total}
            string payload = msg.Substring("CALI_SHOW_POINT ".Length);
            string[] parts = payload.Split(' ');
            if (parts.Length >= 3)
            {
                string[] xy = parts[0].Split(',');
                if (xy.Length == 2
                    && float.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                    && float.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                {
                    if (!firstPointReceived)
                    {
                        // Snap to first position instead of lerping from origin
                        dotCurrentX = x;
                        dotCurrentY = y;
                        firstPointReceived = true;
                    }

                    dotTargetX = x;
                    dotTargetY = y;
                    dotVisible = true;

                    if (dot != null)
                    {
                        dot.style.opacity = 1;
                        dot.RemoveFromClassList("dot-collecting");
                        dot.RemoveFromClassList("dot-done");
                    }

                    if (progressFill != null)
                        progressFill.style.width = Length.Percent(0);
                }

                if (pointLabel != null && parts.Length >= 3)
                    pointLabel.text = $"Point  {parts[1]}  /  {parts[2]}";

                SetStatus("Look at the dot");
            }
        }
        else if (msg.StartsWith("CALI_PROGRESS ", StringComparison.Ordinal))
        {
            if (int.TryParse(msg.Substring("CALI_PROGRESS ".Length).Trim(), out int pct))
            {
                if (progressFill != null)
                    progressFill.style.width = Length.Percent(pct);

                if (dot != null && pct > 0)
                    dot.AddToClassList("dot-collecting");
            }
        }
        else if (msg.StartsWith("CALI_POINT_DONE", StringComparison.Ordinal))
        {
            if (dot != null)
            {
                dot.RemoveFromClassList("dot-collecting");
                dot.AddToClassList("dot-done");
            }
        }
        else if (msg == "CALI_MODEL_FITTING")
        {
            if (fittingOverlay != null)
                fittingOverlay.style.display = DisplayStyle.Flex;

            SetStatus("Fitting gaze model…");
        }
        else if (msg.StartsWith("CALI_MODEL_READY", StringComparison.Ordinal))
        {
            string errorPart = msg.Substring("CALI_MODEL_READY".Length).Trim();
            if (float.TryParse(errorPart, NumberStyles.Float, CultureInfo.InvariantCulture, out float err))
                SetStatus($"Calibration complete!\nError: {err:F4}");
            else
                SetStatus("Calibration complete!");

            StartCoroutine(FinishAndReturn(1.5f));
        }
        else if (msg.StartsWith("CALI_MODEL_ERROR", StringComparison.Ordinal))
        {
            string errorMsg = msg.Substring("CALI_MODEL_ERROR".Length).Trim();

            if (fittingOverlay != null)
                fittingOverlay.style.display = DisplayStyle.None;

            SetStatus($"Calibration failed:\n{errorMsg}\n\nRestart the application to retry.");
        }
        else if (msg.StartsWith("CALI_START", StringComparison.Ordinal))
        {
            SetStatus("Calibration starting…\nFocus on each dot as it appears.");
        }
    }

    // ── Finish & return ───────────────────────────────────────────────────────

    private IEnumerator FinishAndReturn(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Cleanup();
        GazeFollowerRunner.Instance?.NotifyCalibrationComplete();
        SceneManager.LoadScene(returnSceneName);
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private void SetStatus(string text)
    {
        if (statusLabel != null)
            statusLabel.text = text;
    }

    private void Cleanup()
    {
        udpCancellation?.Cancel();
        udpCancellation?.Dispose();
        udpCancellation = null;

        udpClient?.Close();
        udpClient?.Dispose();
        udpClient = null;

        try
        {
            if (calibrationProcess != null && !calibrationProcess.HasExited)
                calibrationProcess.Kill();
        }
        catch { /* process already gone */ }

        calibrationProcess?.Dispose();
        calibrationProcess = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Cleanup();
    }
    private void OnApplicationQuit() => Cleanup();
}
