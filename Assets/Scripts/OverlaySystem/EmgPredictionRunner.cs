using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-890)]
public class EmgPredictionRunner : MonoBehaviour
{
    public static EmgPredictionRunner Instance { get; private set; }

    [Header("Python Setup")]
    [SerializeField] private string relativeWorkingDirectory = @"D:/Projects/emg-work";
    [SerializeField] private string relativePythonPath = @"D:/Projects/emg-work/.venv/Scripts/python.exe";
    [SerializeField] private string relativeScriptPath = "InputBridge/unity_emg_bridge.py";
    [SerializeField] private bool autoStartOnAwake = true;

    [Header("Debug")]
    [SerializeField] private bool simulateEmgWithSpaceKey = true;

    [Header("Runtime Env Overrides")]
    [SerializeField] private bool loadOverridesFromEnvFile = true;
    [SerializeField] private string envFileName = "emg.env";
    [SerializeField] private bool logResolvedPaths = true;

    private Process emgProcess;
    private bool pythonEmgActive;
    private bool simulatedEmgActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<EmgPredictionRunner>() != null)
            return;

        var go = new GameObject("EmgPredictionRunner");
        go.AddComponent<EmgPredictionRunner>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (autoStartOnAwake)
            StartEmgPrediction();
    }

    private void Update()
    {
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
            RobitLogger.Log("[EmgPredictionRunner] EMG prediction is already running.");
            return;
        }

        string workingDirectoryConfig = relativeWorkingDirectory;
        string pythonPathConfig = relativePythonPath;
        string scriptPathConfig = relativeScriptPath;
        LoadEnvOverrides(ref workingDirectoryConfig, ref pythonPathConfig, ref scriptPathConfig);

        if (!TryResolvePath(workingDirectoryConfig, expectFile: false, out string workingDirectory, out string workingDetails))
        {
            RobitLogger.LogWarning("[EmgPredictionRunner] Working directory could not be resolved (EMG unavailable).\n" + workingDetails);
            return;
        }

        if (!TryResolvePath(pythonPathConfig, expectFile: true, out string pythonExe, out string pythonDetails))
        {
            RobitLogger.LogWarning("[EmgPredictionRunner] Python executable could not be resolved (EMG unavailable).\n" + pythonDetails);
            return;
        }

        if (!TryResolvePath(scriptPathConfig, expectFile: true, out string scriptPath, out string scriptDetails))
        {
            RobitLogger.LogWarning("[EmgPredictionRunner] EMG bridge script could not be resolved.\n" + scriptDetails);
            return;
        }

        if (logResolvedPaths)
        {
            RobitLogger.Log("[EmgPredictionRunner] Using paths:\n" +
                            $" - WorkingDir: {workingDirectory}\n" +
                            $" - PythonExe : {pythonExe}\n" +
                            $" - Script    : {scriptPath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = $"\"{scriptPath}\"",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        emgProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        emgProcess.OutputDataReceived += OnOutputDataReceived;
        emgProcess.ErrorDataReceived += OnErrorDataReceived;
        emgProcess.Exited += OnProcessExited;

        try
        {
            emgProcess.Start();
            emgProcess.BeginOutputReadLine();
            emgProcess.BeginErrorReadLine();
            RobitLogger.Log("[EmgPredictionRunner] Started EMG bridge.");
        }
        catch (Exception ex)
        {
            RobitLogger.LogError($"[EmgPredictionRunner] Failed to start EMG bridge: {ex.Message}");
        }
#else
        RobitLogger.LogWarning("[EmgPredictionRunner] This runner currently supports Windows builds only.");
#endif
    }

    public void StopEmgPrediction()
    {
        if (emgProcess == null)
            return;

        try
        {
            if (!emgProcess.HasExited)
                emgProcess.Kill();
        }
        catch (Exception ex)
        {
            RobitLogger.LogWarning($"[EmgPredictionRunner] Failed to stop EMG process: {ex.Message}");
        }
        finally
        {
            CleanupProcessHandlers();
        }
    }

    private void OnApplicationQuit()
    {
        StopEmgPrediction();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        StopEmgPrediction();
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        string line = e.Data.Trim();
        if (line.StartsWith("EMG:", StringComparison.OrdinalIgnoreCase))
        {
            string payload = line.Substring(4).Trim();
            pythonEmgActive = payload == "1" || payload.Equals("true", StringComparison.OrdinalIgnoreCase);
            ApplyEffectiveEmgState();
            return;
        }

        RobitLogger.Log($"[EmgPredictionRunner][PY] {line}");
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        RobitLogger.LogWarning($"[EmgPredictionRunner][PY-ERR] {e.Data}");
    }

    private void OnProcessExited(object sender, EventArgs e)
    {
        pythonEmgActive = false;
        ApplyEffectiveEmgState();
        RobitLogger.Log("[EmgPredictionRunner] EMG process exited.");
    }

    private void ApplyEffectiveEmgState()
    {
        bool isActive = pythonEmgActive || simulatedEmgActive;
        VirtualInputState.Instance.SetEmgPrediction(isActive, isActive ? 1f : 0f);
    }

    private void CleanupProcessHandlers()
    {
        if (emgProcess == null)
            return;

        emgProcess.OutputDataReceived -= OnOutputDataReceived;
        emgProcess.ErrorDataReceived -= OnErrorDataReceived;
        emgProcess.Exited -= OnProcessExited;
        emgProcess.Dispose();
        emgProcess = null;
    }

    private void LoadEnvOverrides(ref string workingDirectoryConfig, ref string pythonPathConfig, ref string scriptPathConfig)
    {
        if (!loadOverridesFromEnvFile)
            return;

        if (!TryResolveEnvFilePath(envFileName, out string envPath, out string details))
        {
            if (logResolvedPaths)
                RobitLogger.Log("[EmgPredictionRunner] Env file not found. Using inspector values.\n" + details);
            return;
        }

        Dictionary<string, string> values = ParseEnvFile(envPath);
        if (values.TryGetValue("EMG_WORKING_DIR", out string wd) && !string.IsNullOrWhiteSpace(wd))
            workingDirectoryConfig = wd.Trim();

        if (values.TryGetValue("EMG_PYTHON_PATH", out string py) && !string.IsNullOrWhiteSpace(py))
            pythonPathConfig = py.Trim();

        if (values.TryGetValue("EMG_SCRIPT_PATH", out string script) && !string.IsNullOrWhiteSpace(script))
            scriptPathConfig = script.Trim();

        RobitLogger.Log($"[EmgPredictionRunner] Loaded env overrides from: {envPath}");
    }

    private static Dictionary<string, string> ParseEnvFile(string envPath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] lines = File.ReadAllLines(envPath);
        foreach (string raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            string line = raw.Trim();
            if (line.StartsWith("#"))
                continue;

            int split = line.IndexOf('=');
            if (split <= 0)
                continue;

            string key = line.Substring(0, split).Trim();
            string value = line.Substring(split + 1).Trim();

            if (value.Length >= 2)
            {
                bool doubleQuoted = value.StartsWith("\"") && value.EndsWith("\"");
                bool singleQuoted = value.StartsWith("'") && value.EndsWith("'");
                if (doubleQuoted || singleQuoted)
                    value = value.Substring(1, value.Length - 2);
            }

            if (!string.IsNullOrEmpty(key))
                result[key] = value;
        }

        return result;
    }

    private static bool TryResolveEnvFilePath(string configuredEnvFileName, out string envPath, out string details)
    {
        envPath = null;
        string foundPath = null;
        var attempts = new List<string>();

        string fileName = string.IsNullOrWhiteSpace(configuredEnvFileName) ? "emg.env" : configuredEnvFileName.Trim().Trim('"');

        void TryPath(string candidate)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch
            {
                return;
            }

            if (attempts.Contains(fullPath))
                return;

            attempts.Add(fullPath);
            if (foundPath == null && File.Exists(fullPath))
                foundPath = fullPath;
        }

        string dataPath = Application.dataPath;
        string projectRoot = Directory.GetParent(dataPath)?.FullName ?? dataPath;
        string executableRoot = AppDomain.CurrentDomain.BaseDirectory;

        TryPath(Path.Combine(projectRoot, fileName));
        TryPath(Path.Combine(executableRoot, fileName));

        details = $"[EnvResolver] Configured='{fileName}'\n[EnvResolver] Attempts:\n - {string.Join("\n - ", attempts)}";
        envPath = foundPath;
        return foundPath != null;
    }

    private static bool TryResolvePath(string configuredPath, bool expectFile, out string resolvedPath, out string details)
    {
        resolvedPath = null;
        string foundPath = null;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            details = "[PathResolver] Configured path is empty.";
            return false;
        }

        string normalized = configuredPath.Trim().Trim('"');
        var attempts = new List<string>();

        bool Exists(string path) => expectFile ? File.Exists(path) : Directory.Exists(path);

        void TryCandidate(string candidate)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch
            {
                return;
            }

            if (attempts.Contains(fullPath))
                return;

            attempts.Add(fullPath);
            if (foundPath == null && Exists(fullPath))
                foundPath = fullPath;
        }

        if (Path.IsPathRooted(normalized))
            TryCandidate(normalized);
        else
        {
            string dataPath = Application.dataPath;
            string projectRoot = Directory.GetParent(dataPath)?.FullName ?? dataPath;
            string executableRoot = AppDomain.CurrentDomain.BaseDirectory;
            string streamingAssetsPath = Application.streamingAssetsPath;

            TryCandidate(Path.Combine(streamingAssetsPath, normalized));
            TryCandidate(Path.Combine(projectRoot, normalized));
            TryCandidate(Path.Combine(executableRoot, normalized));
            TryCandidate(Path.Combine(projectRoot, "..", normalized));
        }

        details = $"[PathResolver] Configured='{configuredPath}', Type={(expectFile ? "File" : "Directory")}\n" +
                  $"[PathResolver] Attempts:\n - {string.Join("\n - ", attempts)}";
        resolvedPath = foundPath;
        return foundPath != null;
    }
}
