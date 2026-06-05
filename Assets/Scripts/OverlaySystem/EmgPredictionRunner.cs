using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-890)]
public class EmgPredictionRunner : BaseUdpProcessRunner<EmgPredictionRunner>
{
    // ── LogPrefix (required by BaseProcessRunner) ─────────────────────────────
    protected override string LogPrefix => "[EmgPredictionRunner]";

    // ── Inspector fields ──────────────────────────────────────────────────────
    [Header("Multimodal Setup")]
    [SerializeField] private string relativeExePath = @"Multimodal_UDP/unity_emg_bridge.exe";
    [SerializeField] private string comPort = "COM4";

    [Header("Debug")]
    [SerializeField] private bool simulateEmgWithSpaceKey = true;

    // ── State ─────────────────────────────────────────────────────────────────
    public string CurrentSensorStatus { get; private set; } = "UNKNOWN";

    private Process emgProcess;
    private bool pythonEmgActive;
    private bool simulatedEmgActive;

    private ConcurrentQueue<bool> emgPacketQueue = new ConcurrentQueue<bool>();

    // ── Bootstrap (commented out — started externally) ────────────────────────
    // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<EmgPredictionRunner>() != null)
            return;

        var go = new GameObject("EmgPredictionRunner");
        go.AddComponent<EmgPredictionRunner>();
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Update()
    {
        while (emgPacketQueue.TryDequeue(out bool pythonState))
        {
            pythonEmgActive = pythonState;
            ApplyEffectiveEmgState();
        }

        if (!simulateEmgWithSpaceKey || Keyboard.current == null)
            return;

        bool isPressed = Keyboard.current.spaceKey.isPressed;
        if (simulatedEmgActive == isPressed)
            return;

        simulatedEmgActive = isPressed;
        ApplyEffectiveEmgState();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void StartEmgPrediction()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (emgProcess != null && !emgProcess.HasExited)
        {
            RobitLogger.Log($"{LogPrefix} EMG prediction is already running.");
            return;
        }

        string exePath = Path.Combine(Application.streamingAssetsPath, relativeExePath)
                             .Replace("/", "\\");

        if (!File.Exists(exePath))
        {
            RobitLogger.LogWarning($"{LogPrefix} Executable not found at {exePath}. Did you run PyInstaller?");
            return;
        }

        try
        {
            int assignedPort = SetupUdpAndGetPort();
            string args      = $"--port {assignedPort} --com-port {comPort}";

            var psi = BuildProcessStartInfo(exePath, args);
            emgProcess = StartManagedProcess(psi);
            ChildProcessTracker.AddProcess(emgProcess);

            RobitLogger.Log($"{LogPrefix} Started EMG bridge on dynamic port {assignedPort}.");
        }
        catch (Exception ex)
        {
            RobitLogger.LogError($"{LogPrefix} Failed to start EMG bridge: {ex.Message}");
            CleanupUdp();
        }
#else
        RobitLogger.LogWarning($"{LogPrefix} This runner currently supports Windows builds only.");
#endif
    }

    public void StopEmgPrediction()
    {
        CleanupUdp();
        TerminateProcess(ref emgProcess);
    }

    /// <summary>Required by BaseProcessRunner — called on quit and destroy.</summary>
    public override void StopRunner() => StopEmgPrediction();

    // ── UDP receive loop ──────────────────────────────────────────────────────

    protected override async Task ReceiveUdpLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result  = await udpClient.ReceiveAsync();
                string           payload = Encoding.UTF8.GetString(result.Buffer).Trim();

                if (payload.StartsWith("EMG_STATUS:", StringComparison.OrdinalIgnoreCase))
                {
                    string status = payload.Substring("EMG_STATUS:".Length).Trim();
                    CurrentSensorStatus = status;
                    if (status.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                        RobitLogger.LogError($"{LogPrefix} Sensor Connection Failed: {status}");
                    else if (status.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
                        RobitLogger.Log($"{LogPrefix} Sensor Connection Successful (EMG_OK).");
                }
                else if (payload == "1" || payload.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    emgPacketQueue.Enqueue(true);
                }
                else if (payload == "0" || payload.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    emgPacketQueue.Enqueue(false);
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

    // ── Process event overrides ───────────────────────────────────────────────

    /// <summary>
    /// On exit, reset the EMG prediction state and log.
    /// The base implementation handles the log; we prepend the queue reset.
    /// </summary>
    protected override void OnProcessExited(object sender, EventArgs e)
    {
        emgPacketQueue.Enqueue(false); // Reset prediction state on process exit
        base.OnProcessExited(sender, e);
    }

    // ── Domain logic ──────────────────────────────────────────────────────────

    private void ApplyEffectiveEmgState()
    {
        bool isActive = pythonEmgActive || simulatedEmgActive;
        VirtualInputState.Instance.SetEmgPrediction(isActive, isActive ? 1f : 0f);
    }
}
