using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

[DefaultExecutionOrder(-1010)]
public class FaceGestureRunner : BaseProcessRunner<FaceGestureRunner>
{
    // ── LogPrefix (required by BaseProcessRunner) ─────────────────────────────
    protected override string LogPrefix => "[FaceGestureRunner]";

    public event Action<string> OnGestureDetected;

    [Header("Face Gesture Setup")]
    [SerializeField] private string relativeWorkingDirectory = @"Multimodal_UDP/unity_gestures_bridge";
    [SerializeField] private string relativeExecutablePath = @"Multimodal_UDP/unity_gestures_bridge/unity_gestures_bridge.exe";
    [SerializeField] private string executableArguments = "";
    [SerializeField] private bool autoStartOnAwake = true;

    [Header("Gesture Trigger")]
    [SerializeField] private string eyebrowGestureName = "raise_eyebrow";

    [Header("Runtime Env Overrides")]
    [SerializeField] private bool loadOverridesFromEnvFile = true;
    [SerializeField] private string envFileName = "face.env";
    [SerializeField] private bool logResolvedPaths = true;

    private Process faceGestureProcess;

    // ── Awake hook (autostart) ────────────────────────────────────────────────
    protected override void OnAfterAwake()
    {
        if (autoStartOnAwake)
            StartFaceGesturePipe();
    }

    // ── Public API ────────────────────────────────────────────────────────────
    public void StartFaceGesturePipe()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (faceGestureProcess != null && !faceGestureProcess.HasExited)
        {
            RobitLogger.Log($"{LogPrefix} Face gesture pipe is already running.");
            return;
        }

        string workingDirectoryConfig = relativeWorkingDirectory;
        string executablePathConfig = relativeExecutablePath;
        string executableArgsConfig = executableArguments;

        LoadEnvOverrides(ref workingDirectoryConfig, ref executablePathConfig, ref executableArgsConfig);

        if (!RunnerPathResolver.TryResolvePath(
                workingDirectoryConfig,
                expectFile: false,
                out string workingDirectory,
                out string workingDetails))
        {
            RobitLogger.LogWarning(
                $"{LogPrefix} Working directory could not be resolved (face gestures unavailable).\n"
                + workingDetails);
            return;
        }

        if (!RunnerPathResolver.TryResolvePath(
                executablePathConfig,
                expectFile: true,
                out string executablePath,
                out string executableDetails))
        {
            RobitLogger.LogWarning(
                $"{LogPrefix} Face gesture executable could not be resolved (face gestures unavailable).\n"
                + executableDetails);
            return;
        }

        if (logResolvedPaths)
        {
            RobitLogger.Log(
                $"{LogPrefix} Using paths:\n"
                + $" - WorkingDir : {workingDirectory}\n"
                + $" - Executable: {executablePath}\n"
                + $" - Arguments : {executableArgsConfig}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = executableArgsConfig,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        try
        {
            faceGestureProcess = StartManagedProcess(psi);
            RobitLogger.Log($"{LogPrefix} Started face gesture pipe.");
        }
        catch (Exception ex)
        {
            RobitLogger.LogError($"{LogPrefix} Failed to start face gesture pipe: {ex.Message}");
        }
#else
        RobitLogger.LogWarning($"{LogPrefix} This runner currently supports Windows builds only.");
#endif
    }

    public void StopFaceGesturePipe()
    {
        TerminateProcess(ref faceGestureProcess);
    }

    public override void StopRunner() => StopFaceGesturePipe();

    // ── Process event overrides ───────────────────────────────────────────────
    protected override void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        string line = e.Data.Trim();
        if (TryParseGesture(line, out string gestureName))
        {
            RobitLogger.Log($"{LogPrefix} Gesture detected: {gestureName}");
            OnGestureDetected?.Invoke(gestureName);
            return;
        }

        RobitLogger.Log($"{LogPrefix}[EXE] {line}");
    }

    protected override void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        RobitLogger.LogWarning($"{LogPrefix}[EXE-ERR] {e.Data}");
    }

    // ── Gesture parsing ───────────────────────────────────────────────────────
    private bool TryParseGesture(string line, out string gestureName)
    {
        gestureName = null;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        string normalized = line.ToLowerInvariant();
        if (normalized.Contains("eyebrows raised") || normalized.Contains("eyebrow_up") || normalized.Contains("eyebrow") || normalized.Contains("gesture"))
        {
            if (normalized.Contains("eyebrows raised") || normalized.Contains("eyebrow_up") || normalized.Contains("raise_eyebrow"))
            {
                gestureName = eyebrowGestureName;
                return true;
            }

            int start = normalized.IndexOf("gesture", StringComparison.OrdinalIgnoreCase);
            if (start >= 0)
            {
                int colon = normalized.IndexOf(':', start);
                if (colon >= 0)
                {
                    gestureName = normalized.Substring(colon + 1).Trim().Trim('"', '\'', ' ', '\t');
                    return !string.IsNullOrWhiteSpace(gestureName);
                }
            }
        }

        return false;
    }

    // ── Env override helpers ─────────────────────────────────────────────────
    private void LoadEnvOverrides(ref string workingDirectoryConfig, ref string executablePathConfig, ref string executableArgsConfig)
    {
        if (!loadOverridesFromEnvFile)
            return;

        if (!RunnerPathResolver.TryResolveEnvFilePath(envFileName, out string envPath, out string details))
        {
            if (logResolvedPaths)
                RobitLogger.Log($"{LogPrefix} Env file not found. Using inspector values.\n" + details);
            return;
        }

        Dictionary<string, string> values = RunnerPathResolver.ParseEnvFile(envPath);

        if (values.TryGetValue("FACE_WORKING_DIR", out string wd) && !string.IsNullOrWhiteSpace(wd))
            workingDirectoryConfig = wd.Trim();

        if (values.TryGetValue("FACE_EXECUTABLE_PATH", out string exe) && !string.IsNullOrWhiteSpace(exe))
            executablePathConfig = exe.Trim();
        else if (values.TryGetValue("FACE_EXE_PATH", out string altExe) && !string.IsNullOrWhiteSpace(altExe))
            executablePathConfig = altExe.Trim();

        if (values.TryGetValue("FACE_EXECUTABLE_ARGS", out string args) && !string.IsNullOrWhiteSpace(args))
            executableArgsConfig = args.Trim();

        RobitLogger.Log($"{LogPrefix} Loaded env overrides from: {envPath}");
    }
}
