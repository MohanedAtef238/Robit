using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(-900)]
public class GazeFollowerRunner : MonoBehaviour
{
    public static GazeFollowerRunner Instance { get; private set; }

    [Header("Python Setup")]
    [SerializeField] private string relativeWorkingDirectory = "Utilities/GazeFollower";
    [SerializeField] private string relativePythonPath = "Utilities/GazeFollower/.venv/Scripts/python.exe";
    [SerializeField] private string scriptFileName = "game_Test.py";
    [SerializeField] private bool autoStartOnAwake = true;

    [Header("Runtime Env Overrides")]
    [SerializeField] private bool loadOverridesFromEnvFile = true;
    [SerializeField] private string envFileName = "gaze.env";
    [SerializeField] private bool logResolvedPaths = true;

    private Process gazeProcess;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<GazeFollowerRunner>() != null)
            return;

        var go = new GameObject("GazeFollowerRunner");
        go.AddComponent<GazeFollowerRunner>();
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
            StartGazeFollower();
    }

    public void StartGazeFollower()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (gazeProcess != null && !gazeProcess.HasExited)
        {
            RobitLogger.Log("[GazeFollowerRunner] GazeFollower is already running.");
            return;
        }

        string workingDirectoryConfig = relativeWorkingDirectory;
        string pythonPathConfig = relativePythonPath;
        string scriptFileNameConfig = scriptFileName;
        LoadEnvOverrides(ref workingDirectoryConfig, ref pythonPathConfig, ref scriptFileNameConfig);

        if (!TryResolvePath(workingDirectoryConfig, expectFile: false, out string workingDirectory, out string workingDetails))
        {
            RobitLogger.LogWarning("[GazeFollowerRunner] Working directory could not be resolved (gaze tracking unavailable).\n" + workingDetails);
            return;
        }

        if (!TryResolvePath(pythonPathConfig, expectFile: true, out string pythonExe, out string pythonDetails))
        {
            RobitLogger.LogWarning("[GazeFollowerRunner] Python executable could not be resolved (gaze tracking unavailable).\n" + pythonDetails);
            return;
        }

        string scriptRelative = Path.Combine(workingDirectoryConfig, scriptFileNameConfig);
        if (!TryResolvePath(scriptRelative, expectFile: true, out string scriptPath, out string scriptDetails))
        {
            string fallbackRelative = Path.Combine(workingDirectoryConfig, "game_test.py");
            if (!TryResolvePath(fallbackRelative, expectFile: true, out scriptPath, out string fallbackDetails))
            {
                RobitLogger.LogWarning("[GazeFollowerRunner] Gaze script could not be resolved (gaze tracking unavailable).\n" + scriptDetails + "\n" + fallbackDetails);
                return;
            }
        }

        if (logResolvedPaths)
        {
            RobitLogger.Log("[GazeFollowerRunner] Using paths:\n" +
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

        gazeProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        gazeProcess.OutputDataReceived += OnOutputDataReceived;
        gazeProcess.ErrorDataReceived += OnErrorDataReceived;
        gazeProcess.Exited += OnProcessExited;

        try
        {
            gazeProcess.Start();
            gazeProcess.BeginOutputReadLine();
            gazeProcess.BeginErrorReadLine();
            RobitLogger.Log("[GazeFollowerRunner] Started game_Test.py.");
        }
        catch (Exception ex)
        {
            RobitLogger.LogError($"[GazeFollowerRunner] Failed to start gaze process: {ex.Message}");
        }
#else
        RobitLogger.LogWarning("[GazeFollowerRunner] This runner currently supports Windows builds only.");
#endif
    }

    public void StopGazeFollower()
    {
        if (gazeProcess == null)
            return;

        try
        {
            if (!gazeProcess.HasExited)
                gazeProcess.Kill();
        }
        catch (Exception ex)
        {
            RobitLogger.LogWarning($"[GazeFollowerRunner] Failed to stop gaze process: {ex.Message}");
        }
        finally
        {
            CleanupProcessHandlers();
        }
    }

    private void OnApplicationQuit()
    {
        StopGazeFollower();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        StopGazeFollower();
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        if (string.Equals(e.Data.Trim(), "CALIBRATION_DONE", StringComparison.Ordinal))
        {
            RobitLogger.Log("[GazeFollowerRunner] Calibration completed. Gaze is now driving mouse movement.");
            return;
        }

        RobitLogger.Log($"[GazeFollowerRunner][PY] {e.Data}");
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        RobitLogger.LogWarning($"[GazeFollowerRunner][PY-ERR] {e.Data}");
    }

    private void OnProcessExited(object sender, EventArgs e)
    {
        RobitLogger.Log("[GazeFollowerRunner] Gaze process exited.");
    }

    private void CleanupProcessHandlers()
    {
        if (gazeProcess == null)
            return;

        gazeProcess.OutputDataReceived -= OnOutputDataReceived;
        gazeProcess.ErrorDataReceived -= OnErrorDataReceived;
        gazeProcess.Exited -= OnProcessExited;
        gazeProcess.Dispose();
        gazeProcess = null;
    }

    private void LoadEnvOverrides(ref string workingDirectoryConfig, ref string pythonPathConfig, ref string scriptFileNameConfig)
    {
        if (!loadOverridesFromEnvFile)
            return;

        if (!TryResolveEnvFilePath(envFileName, out string envPath, out string details))
        {
            if (logResolvedPaths)
                RobitLogger.Log("[GazeFollowerRunner] Env file not found. Using inspector values.\n" + details);
            return;
        }

        Dictionary<string, string> values = ParseEnvFile(envPath);
        if (values.TryGetValue("GAZE_WORKING_DIR", out string wd) && !string.IsNullOrWhiteSpace(wd))
            workingDirectoryConfig = wd.Trim();

        if (values.TryGetValue("GAZE_PYTHON_PATH", out string py) && !string.IsNullOrWhiteSpace(py))
            pythonPathConfig = py.Trim();

        if (values.TryGetValue("GAZE_SCRIPT_NAME", out string script) && !string.IsNullOrWhiteSpace(script))
            scriptFileNameConfig = script.Trim();

        RobitLogger.Log($"[GazeFollowerRunner] Loaded env overrides from: {envPath}");
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

            // Remove optional surrounding single/double quotes.
            if (value.Length >= 2)
            {
                bool dq = value.StartsWith("\"") && value.EndsWith("\"");
                bool sq = value.StartsWith("'") && value.EndsWith("'");
                if (dq || sq)
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

        string fileName = string.IsNullOrWhiteSpace(configuredEnvFileName) ? "gaze.env" : configuredEnvFileName.Trim().Trim('"');

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
        string buildRoot = Directory.GetParent(dataPath)?.FullName ?? dataPath;
        string executableRoot = AppDomain.CurrentDomain.BaseDirectory;
        string projectRoot = Directory.GetParent(dataPath)?.FullName ?? dataPath;

        // Build output folder (next to exe)
        TryPath(Path.Combine(buildRoot, fileName));
        // Executable base directory
        TryPath(Path.Combine(executableRoot, fileName));
        // Project root for editor testing
        TryPath(Path.Combine(projectRoot, fileName));

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

        bool Exists(string p) => expectFile ? File.Exists(p) : Directory.Exists(p);

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
            string buildRoot = Directory.GetParent(dataPath)?.FullName ?? dataPath;
            string executableRoot = AppDomain.CurrentDomain.BaseDirectory;

            // Editor: project-root relative paths.
            TryCandidate(Path.Combine(projectRoot, normalized));
            // Standalone: files copied next to the EXE.
            TryCandidate(Path.Combine(buildRoot, normalized));
            // Standalone: files copied beside *_Data folder.
            TryCandidate(Path.Combine(executableRoot, normalized));
            // One level up from build folder (common during local testing).
            TryCandidate(Path.Combine(buildRoot, "..", normalized));
        }

        details = $"[PathResolver] Configured='{configuredPath}', Type={(expectFile ? "File" : "Directory")}\n" +
                  $"[PathResolver] Attempts:\n - {string.Join("\n - ", attempts)}";
        resolvedPath = foundPath;
        return foundPath != null;
    }
}

