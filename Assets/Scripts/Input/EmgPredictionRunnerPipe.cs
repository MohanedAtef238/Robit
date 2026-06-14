using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-890)]
public class EmgPredictionRunnerPipe : BaseProcessRunner<EmgPredictionRunnerPipe>
{
    // ── LogPrefix (required by BaseProcessRunner) ─────────────────────────────
    protected override string LogPrefix => "[EmgPredictionRunnerPipe]";

    [Header("Multimodal Setup")]
    [SerializeField] private string relativeExePath = @"Multimodal_UDP/unity_emg_bridge/unity_emg_bridge.exe";
    [SerializeField] private string comPort = "COM4";
    [SerializeField] private bool autoStartOnAwake = true;

    [Header("Debug")]
    [SerializeField] private bool simulateEmgWithSpaceKey = false;

    private Process emgProcess;
    private bool pythonEmgActive;
    private bool simulatedEmgActive;

    // Use a queue to safely move data from the background output thread to the main Unity thread
    private ConcurrentQueue<bool> emgPacketQueue = new ConcurrentQueue<bool>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<EmgPredictionRunnerPipe>() != null)
            return;

        var go = new GameObject("EmgPredictionRunnerPipe");
        go.AddComponent<EmgPredictionRunnerPipe>();
    }

    protected override void OnAfterAwake()
    {
        if (autoStartOnAwake)
            StartEmgPrediction();
    }

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

        string args = $"--com-port {comPort}";
        var startInfo = BuildProcessStartInfo(exePath, args);

        try
        {
            emgProcess = StartManagedProcess(startInfo);
            ChildProcessTracker.AddProcess(emgProcess);
            RobitLogger.Log($"{LogPrefix} Started EMG bridge from executable.");
        }
        catch (Exception ex)
        {
            RobitLogger.LogError($"{LogPrefix} Failed to start EMG bridge: {ex.Message}");
        }
#else
        RobitLogger.LogWarning($"{LogPrefix} This runner currently supports Windows builds only.");
#endif
    }

    public void StopEmgPrediction()
    {
        TerminateProcess(ref emgProcess);
    }

    public override void StopRunner() => StopEmgPrediction();

    // ── Process event overrides ───────────────────────────────────────────────

    protected override void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        string line = e.Data.Trim();
        if (line.StartsWith("EMG_STATUS:", StringComparison.OrdinalIgnoreCase))
        {
            string status = line.Substring("EMG_STATUS:".Length).Trim();
            if (status.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                RobitLogger.LogError($"{LogPrefix} Sensor Connection Failed: {status}");
            else if (status.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
                RobitLogger.Log($"{LogPrefix} Sensor Connection Successful (EMG_OK).");
            return;
        }

        if (line.StartsWith("EMG:", StringComparison.OrdinalIgnoreCase))
        {
            string payload = line.Substring(4).Trim();
            bool isActive = payload == "1" || payload.Contains("true", StringComparison.OrdinalIgnoreCase);
            RobitLogger.Log($"Bite Detected: {isActive}");
            emgPacketQueue.Enqueue(isActive);
            return;
        }

        if (line == "1" || line.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            emgPacketQueue.Enqueue(true);
            return;
        }
        else if (line == "0" || line.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            emgPacketQueue.Enqueue(false);
            return;
        }

        // Base implementation handles standard stdout logging
        base.OnOutputDataReceived(sender, e);
    }

    protected override void OnProcessExited(object sender, EventArgs e)
    {
        emgPacketQueue.Enqueue(false);
        base.OnProcessExited(sender, e);
    }

    // ── Domain logic ──────────────────────────────────────────────────────────

    private void ApplyEffectiveEmgState()
    {
        bool isActive = pythonEmgActive || simulatedEmgActive;
        VirtualInputState.Instance.SetEmgPrediction(isActive, isActive ? 1f : 0f);
    }
}
