using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-900)]
public class GazeFollowerRunner : BaseUdpProcessRunner<GazeFollowerRunner>
{
    // ── LogPrefix (required by BaseProcessRunner) ─────────────────────────────
    protected override string LogPrefix => "[GazeFollowerRunner]";

    private enum GazeLaunchMode
    {
        Auto,
        Calibrate,
        UseSaved
    }

    private const string CalibrationSceneName = "GazeCalibrationScene";

    [Tooltip("Path to the Python bridge exe, relative to StreamingAssets.")]
    [SerializeField] private string relativeExePath = @"Multimodal_UDP/unity_gaze_bridge/unity_gaze_bridge.exe";
    [SerializeField] private bool promptForCalibrationChoice = true;

    [Header("Debug")]
    [SerializeField] private bool simulateGazeWithArrowKeys = true;

    private Process gazeProcess;
    private Process cameraCheckProcess;
    private Process statusProbeProcess;

    private VisualElement startupPromptRoot;
    private bool startupFlowRunning;
    private string activeProfileId;

    private ConcurrentQueue<Vector2> gazePacketQueue = new ConcurrentQueue<Vector2>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<GazeFollowerRunner>() != null)
            return;

        var go = new GameObject("GazeFollowerRunner");
        go.AddComponent<GazeFollowerRunner>();
    }

    // To trigger the startup flow, call TriggerStartupFlow(profileId) externally.
    protected override void OnAfterAwake()
    {
        SharedCameraCapture.EnsureSpawned();
    }

    private void Update()
    {
        // Drain all packets, keep only the latest gaze position to minimize latency
        Vector2? latestGaze = null;
        while (gazePacketQueue.TryDequeue(out Vector2 gazePos))
        {
            latestGaze = gazePos;
        }

        if (simulateGazeWithArrowKeys && Keyboard.current != null && (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.rightArrowKey.isPressed || Keyboard.current.upArrowKey.isPressed || Keyboard.current.downArrowKey.isPressed))
        {
            // Simulate gaze moving via arrow keys directly in Unity
            Vector2 currentGaze = VirtualInputState.Instance.GazePosition;
            float speed = 1000f * Time.deltaTime;
            if (Keyboard.current.leftArrowKey.isPressed) currentGaze.x -= speed;
            if (Keyboard.current.rightArrowKey.isPressed) currentGaze.x += speed;
            if (Keyboard.current.upArrowKey.isPressed) currentGaze.y -= speed;
            if (Keyboard.current.downArrowKey.isPressed) currentGaze.y += speed;
            currentGaze.x = Mathf.Clamp(currentGaze.x, 0, Screen.width);
            currentGaze.y = Mathf.Clamp(currentGaze.y, 0, Screen.height);
            VirtualInputState.Instance.SetGazePosition(currentGaze);
        }
        else if (latestGaze.HasValue)
        {
            VirtualInputState.Instance.SetGazePosition(latestGaze.Value);
        }
    }

    public void StartGazeFollower()
    {
        StartGazeFollower(GazeLaunchMode.Auto);
    }

    public void StopGazeFollower()
    {
        // UDP socket and cancellation token are managed by BaseUdpProcessRunner.
        CleanupUdp();

        // gazeProcess was started via StartManagedProcess() so needs full handler
        // unhook + dispose via TerminateProcess.
        TerminateProcess(ref gazeProcess);

        // cameraCheckProcess and statusProbeProcess were NOT started via
        // StartManagedProcess() and have no base-class handlers hooked, so
        // KillAndDispose (kill-only, no unhook) is the correct call.
        KillAndDispose(ref cameraCheckProcess);
        KillAndDispose(ref statusProbeProcess);
    }

    /// <summary>Required by BaseProcessRunner — called on quit and destroy.</summary>
    public override void StopRunner() => StopGazeFollower();

    private string GetResolvedExePath()
    {
        return Path.Combine(Application.streamingAssetsPath, relativeExePath).Replace("/", "\\");
    }

    private IEnumerator BeginStartupFlow()
    {
        if (startupFlowRunning)
            yield break;

        if (gazeProcess != null && !gazeProcess.HasExited)
            yield break;

        startupFlowRunning = true;

        // Wait one frame so all Awake() calls across the scene have completed,
        // then ensure Unity owns the webcam before any Python EXE is spawned.
        yield return null;
        // Camera capture is now completely self-managed via its own Singleton.

        // Wait until SharedCameraCapture has written its first real frame into the MMF.
        // Without this, Python opens the MMF, sees frameId == 0, and starts a 20-second
        // frame-wait timeout even though the camera is still warming up.
        yield return StartCoroutine(WaitForCameraReady());
        if (!startupFlowRunning)   // WaitForCameraReady sets this false on timeout
            yield break;

        if (!promptForCalibrationChoice)
        {
            StartGazeFollower(GazeLaunchMode.Auto);
            startupFlowRunning = false;
            yield break;
        }

        string exePath = GetResolvedExePath();
        if (!File.Exists(exePath))
        {
            RobitLogger.LogWarning($"[GazeFollowerRunner] Executable not found at {exePath}. Did you run PyInstaller?");
            startupFlowRunning = false;
            yield break;
        }

        var task = QuerySavedCalibrationStatusAsync(exePath, activeProfileId);
        while (!task.IsCompleted)
            yield return null;

        var (hasSavedCalibration, statusKnown) = task.Result;

        if (!hasSavedCalibration)
        {
            RobitLogger.Log("[GazeFollowerRunner] No saved calibration found. Loading calibration scene.");
            SceneManager.LoadScene(CalibrationSceneName);
            // startupFlowRunning intentionally stays true; cleared by NotifyCalibrationComplete()
            yield break;
        }

        yield return StartCoroutine(ShowCalibrationChoicePrompt(hasSavedCalibration, statusKnown));
        startupFlowRunning = false;
    }

    /// <summary>
    /// Waits until SharedCameraCapture.IsReady is true (first webcam frame written to MMF),
    /// up to a 15-second hard cap. Sets startupFlowRunning = false and logs an error on timeout.
    /// </summary>
    private IEnumerator WaitForCameraReady()
    {
        var scc = SharedCameraCapture.Instance;
        if (scc == null)
            yield break;   // no SharedCameraCapture in scene - skip wait

        float waitStart = Time.unscaledTime;
        while (!scc.IsReady)
        {
            if (Time.unscaledTime - waitStart > 15f)
            {
                RobitLogger.LogError(
                    "[GazeFollowerRunner] Webcam never delivered a frame after 15 s. " +
                    "Check that a camera is connected and not in use by another application.");
                startupFlowRunning = false;
                yield break;
            }
            yield return null;
        }
        RobitLogger.Log("[GazeFollowerRunner] SharedCameraCapture is ready — proceeding to spawn EXEs.");
    }


    private void StartGazeFollower(GazeLaunchMode launchMode)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (gazeProcess != null && !gazeProcess.HasExited)
        {
            RobitLogger.Log($"{LogPrefix} GazeFollower is already running.");
            return;
        }

        // Guarantee the shared camera is running before Python starts reading from the MMF.
        // BeginStartupFlow already waits for IsReady; this path (direct start, no prompt)
        // delegates the same wait to a coroutine so we don't block the caller.
        // Camera capture is now completely self-managed via its own Singleton.
        StartCoroutine(WaitForCameraReadyThenStart(launchMode));
    }

    private IEnumerator WaitForCameraReadyThenStart(GazeLaunchMode launchMode)
    {
        yield return StartCoroutine(WaitForCameraReady());
        if (!startupFlowRunning && launchMode != GazeLaunchMode.UseSaved)
            yield break;   // WaitForCameraReady timed out

        string exePath = GetResolvedExePath();
        if (!File.Exists(exePath))
        {
            RobitLogger.LogWarning($"{LogPrefix} Executable not found at {exePath}.");
            yield break;
        }

        try
        {
            int assignedPort = SetupUdpAndGetPort();

            string baseArgs = $"--port {assignedPort} --mode {ToModeArgument(launchMode)}";
            if (!string.IsNullOrEmpty(activeProfileId))
                baseArgs += $" --profile-id \"{activeProfileId}\"";

            var psi = BuildProcessStartInfo(exePath, baseArgs);
            gazeProcess = StartManagedProcess(psi);

            RobitLogger.Log($"{LogPrefix} Started gaze bridge on dynamic port {assignedPort}.");
        }
        catch (Exception ex)
        {
            RobitLogger.LogError($"{LogPrefix} Failed to start gaze process: {ex.Message}");
            CleanupUdp();
        }
#else
        RobitLogger.LogWarning($"{LogPrefix} This runner currently supports Windows builds only.");
        yield break;
#endif
    }

    protected override async Task ReceiveUdpLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await udpClient.ReceiveAsync();
                string payload = Encoding.UTF8.GetString(result.Buffer).Trim();

                string[] parts = payload.Split(',');
                if (parts.Length == 2 &&
                    float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y))
                {
                    gazePacketQueue.Enqueue(new Vector2(x, y));
                }
            }
            catch (ObjectDisposedException)
            {
                break; // Expected when UdpClient is closed
            }
            catch (Exception ex)
            {
                RobitLogger.LogWarning($"{LogPrefix} UDP Receive Error: {ex.Message}");
            }
        }
    }

    // CleanupUdp() is inherited from BaseUdpProcessRunner — no local copy needed.

    private Task<(bool hasSaved, bool statusKnown)> QuerySavedCalibrationStatusAsync(string exePath, string profileId)
    {
        return Task.Run(() =>
        {
            bool hasSaved = false;
            bool statusKnown = false;
            try
            {
                string args = "--status-only";
                if (!string.IsNullOrEmpty(profileId))
                {
                    args += $" --profile-id \"{profileId}\"";
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                statusProbeProcess = Process.Start(startInfo);
                if (statusProbeProcess == null)
                    return (false, false);

                string stdout = statusProbeProcess.StandardOutput.ReadToEnd();
                string stderr = statusProbeProcess.StandardError.ReadToEnd();
                statusProbeProcess.WaitForExit(15000);

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    // Filter out harmless MediaPipe initialization info that prints to stderr
                    string filteredStderr = stderr.Replace("INFO: Created TensorFlow Lite XNNPACK delegate for CPU.", "").Trim();
                    if (!string.IsNullOrWhiteSpace(filteredStderr))
                        RobitLogger.LogWarning("[GazeFollowerRunner] Calibration status probe stderr:\n" + filteredStderr);
                }

                foreach (string rawLine in stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string line = rawLine.Trim();
                    if (!line.StartsWith("HAS_SAVED_CALIBRATION:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string payload = line.Substring("HAS_SAVED_CALIBRATION:".Length).Trim();
                    statusKnown = true;
                    hasSaved = payload == "1" || payload.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
                }
            }
            catch (Exception ex)
            {
                RobitLogger.LogWarning($"[GazeFollowerRunner] Failed to query calibration status: {ex.Message}");
            }

            return (hasSaved, statusKnown);
        });
    }

    private IEnumerator ShowCalibrationChoicePrompt(bool hasSavedCalibration, bool statusKnown)
    {
        UIDocument targetDocument = null;
        float timeoutAt = Time.unscaledTime + 10f;

        while (Time.unscaledTime < timeoutAt && targetDocument == null)
        {
            UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (UIDocument document in documents)
            {
                if (document != null && document.rootVisualElement != null)
                {
                    targetDocument = document;
                    break;
                }
            }

            if (targetDocument == null)
                yield return null;
        }

        if (targetDocument == null || targetDocument.rootVisualElement == null)
        {
            RobitLogger.LogWarning("[GazeFollowerRunner] No UIDocument was available for the calibration prompt. Falling back to automatic start.");
            StartGazeFollower(hasSavedCalibration ? GazeLaunchMode.UseSaved : GazeLaunchMode.Calibrate);
            if (hasSavedCalibration) SceneManager.LoadScene("OverlayScene");
            yield break;
        }

        bool selectionMade = false;

#if UNITY_EDITOR
        var visualTree = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Resources/GazeCalibrationPrompt.uxml");
#else
        var visualTree = Resources.Load<VisualTreeAsset>("GazeCalibrationPrompt");
#endif

        if (visualTree == null)
        {
            RobitLogger.LogError("[GazeFollowerRunner] Could not load GazeCalibrationPrompt.uxml. Falling back to automatic start.");
            StartGazeFollower(hasSavedCalibration ? GazeLaunchMode.UseSaved : GazeLaunchMode.Calibrate);
            if (hasSavedCalibration) SceneManager.LoadScene("OverlayScene");
            yield break;
        }

        startupPromptRoot = visualTree.Instantiate();
        var subtitle = startupPromptRoot.Q<Label>("subtitle");
        var calibrateBtn = startupPromptRoot.Q<Button>("calibrate-btn");
        var useSavedBtn = startupPromptRoot.Q<Button>("use-saved-btn");

        if (subtitle != null)
        {
            subtitle.text = statusKnown
                ? (hasSavedCalibration
                    ? "A saved gaze calibration was found for this profile. Choose whether to reuse it or calibrate again."
                    : "No saved gaze calibration was found for this profile. Run calibration now to set up tracking.")
                : "Calibration status could not be checked automatically. You can still calibrate now or try the saved calibration path.";
        }

        if (useSavedBtn != null)
        {
            useSavedBtn.SetEnabled(hasSavedCalibration);
            if (!hasSavedCalibration)
            {
                useSavedBtn.text = "No Saved Calibration Found";
                useSavedBtn.style.backgroundColor = new Color(0.27f, 0.30f, 0.36f, 1f);
            }
            else
            {
                useSavedBtn.clicked += () => 
                {
                    selectionMade = true;
                    RemoveStartupPrompt();
                    StartGazeFollower(GazeLaunchMode.UseSaved);
                    SceneManager.LoadScene("OverlayScene");
                };
            }
        }

        if (calibrateBtn != null)
        {
            calibrateBtn.clicked += () => 
            {
                selectionMade = true;
                RemoveStartupPrompt();
                SceneManager.LoadScene(CalibrationSceneName);
            };
        }

        targetDocument.rootVisualElement.Add(startupPromptRoot);

        while (!selectionMade)
            yield return null;
    }

    private void RemoveStartupPrompt()
    {
        if (startupPromptRoot == null)
            return;

        startupPromptRoot.RemoveFromHierarchy();
        startupPromptRoot = null;
    }

    private static string ToModeArgument(GazeLaunchMode mode)
    {
        return mode switch
        {
            GazeLaunchMode.Calibrate => "calibrate",
            GazeLaunchMode.UseSaved => "use-saved",
            _ => "auto",
        };
    }

    // OnApplicationQuit() and OnDestroy() (Instance cleanup + StopRunner()) are
    // inherited from BaseProcessRunner<T>.
    // OnErrorDataReceived() and OnProcessExited() use the base defaults.

    protected override void OnDestroy()
    {
        // Remove the UI prompt before the base clears Instance and calls StopRunner.
        RemoveStartupPrompt();
        base.OnDestroy();
    }

    /// <summary>
    /// Intercepts CALIBRATION_DONE before delegating to the base log handler.
    /// All other stdout lines are handled identically to the base implementation.
    /// </summary>
    protected override void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        string line = e.Data.Trim();

        if (string.Equals(line, "CALIBRATION_DONE", StringComparison.Ordinal))
        {
            RobitLogger.Log($"{LogPrefix} Calibration completed. Gaze is now driving mouse movement.");
            return;
        }

        base.OnOutputDataReceived(sender, e);
    }

    /// <summary>
    /// Called by GazeCalibrationController when Unity-driven calibration completes.
    /// Clears the startup flow lock and starts the gaze bridge in use-saved mode.
    /// </summary>
    public void NotifyCalibrationComplete()
    {
        startupFlowRunning = false;
        StartGazeFollower(GazeLaunchMode.UseSaved);
    }

    /// <summary>
    /// Called by GazeCalibrationController.Start() when the calibration scene is ready.
    /// Launches the camera-check preview panel before starting the actual calibration.
    /// This is invoked regardless of HOW the calibration scene was reached
    /// (first-time, "Calibrate Again", etc.).
    /// </summary>
    public void OnCalibrationSceneReady(GazeCalibrationController controller)
    {
        StartCoroutine(RunCameraCheckInCalibrationScene(controller));
    }

    /// <summary>
    /// Overlays a camera-check panel on the calibration scene's UIDocument.
    /// Streams JPEG frames from the Python bridge (camera-check mode) into a
    /// Texture2D rendered in the panel. Once the user confirms the camera is
    /// working, the panel is removed and StartCalibration() is called on the
    /// controller.
    /// </summary>
    private IEnumerator RunCameraCheckInCalibrationScene(GazeCalibrationController controller)
    {
        var calibDoc = controller.GetComponent<UIDocument>();
        if (calibDoc == null || calibDoc.rootVisualElement == null)
        {
            RobitLogger.LogWarning("[GazeFollowerRunner] Camera check skipped — UIDocument missing.");
            controller.StartCalibration(activeProfileId);
            yield break;
        }

        // Load CameraCheckPanel UXML
#if UNITY_EDITOR
        var visualTree = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Resources/CameraCheckPanel.uxml");
#else
        var visualTree = Resources.Load<VisualTreeAsset>("CameraCheckPanel");
#endif
        if (visualTree == null)
        {
            RobitLogger.LogWarning("[GazeFollowerRunner] CameraCheckPanel.uxml not found. Skipping camera check.");
            controller.StartCalibration(activeProfileId);
            yield break;
        }

        VisualElement cameraPanel = visualTree.Instantiate();
        cameraPanel.style.position = Position.Absolute;
        cameraPanel.style.left = 0;
        cameraPanel.style.top = 0;
        cameraPanel.style.right = 0;
        cameraPanel.style.bottom = 0;
        calibDoc.rootVisualElement.Add(cameraPanel);

        var previewElement = cameraPanel.Q<VisualElement>("camera-preview");
        var statusLabel    = cameraPanel.Q<Label>("camera-status");
        var errorLabel     = cameraPanel.Q<Label>("camera-error");
        var retryBtn       = cameraPanel.Q<Button>("camera-retry-btn");
        var continueBtn    = cameraPanel.Q<Button>("camera-continue-btn");

        bool selectionMade = false;

        if (continueBtn != null)
            continueBtn.clicked += () => selectionMade = true;

        if (retryBtn    != null) retryBtn.style.display    = DisplayStyle.None;
        if (errorLabel  != null) errorLabel.style.display  = DisplayStyle.None;
        
        if (statusLabel != null) statusLabel.text = "Waiting for camera...";
        if (continueBtn != null) 
        {
            continueBtn.SetEnabled(false);
            continueBtn.style.opacity = 0.5f;
        }

        SharedCameraCapture scc = SharedCameraCapture.Instance;

        // ── Main coroutine loop ───────────────────────────────────────────────
        while (!selectionMade)
        {
            try
            {
                if (scc != null && scc.IsReady)
                {
                    if (statusLabel != null && statusLabel.text != "Camera ready \u2713")
                    {
                        statusLabel.text = "Camera ready \u2713";
                        if (continueBtn != null) 
                        {
                            continueBtn.SetEnabled(true);
                            continueBtn.style.opacity = 1f;
                        }
                    }
                    
                    // SCC updates PreviewTexture internally from the MMF —
                    // just assign and repaint. No GPU readback, no Graphics.Blit.
                    if (previewElement != null && scc.PreviewTexture != null)
                    {
                        previewElement.style.backgroundImage = new StyleBackground(
                            Background.FromTexture2D(scc.PreviewTexture));
                        previewElement.MarkDirtyRepaint();
                    }
                }
                else
                {
                    if (statusLabel != null && statusLabel.text == "Camera ready \u2713")
                    {
                        statusLabel.text = "Waiting for camera...";
                        if (continueBtn != null) 
                        {
                            continueBtn.SetEnabled(false);
                            continueBtn.style.opacity = 0.5f;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[CameraCheck] Coroutine exception on frame {Time.frameCount}: {e}");
            }

            yield return null;
        }

        cameraPanel.RemoveFromHierarchy();

        // Hand off to calibration immediately.
        // The EXEs will be launched by the Calibration Controller which acts as a loading screen.
        controller.StartCalibration(activeProfileId);
    }



    /// <summary>
    /// Re-triggers the startup flow (queries calibration status and begins gaze tracking).
    /// Safe to call after returning from the calibration scene.
    /// </summary>
    public void TriggerStartupFlow(string profileId)
    {
        activeProfileId = profileId;
        StartCoroutine(BeginStartupFlow());
    }

    /// <summary>
    /// Forces the calibration flow for a specific profile (ignoring whether it is already calibrated).
    /// Used by the "Recalibrate" button.
    /// </summary>
    public void TriggerCalibrationFlow(string profileId)
    {
        StopGazeFollower();
        activeProfileId = profileId;
        SceneManager.LoadScene(CalibrationSceneName);
    }
}
