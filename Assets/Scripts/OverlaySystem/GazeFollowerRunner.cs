using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(-900)]
public class GazeFollowerRunner : MonoBehaviour
{
    private enum GazeLaunchMode
    {
        Auto,
        Calibrate,
        UseSaved
    }

    public static GazeFollowerRunner Instance { get; private set; }

    [Header("Python Setup")]
    [SerializeField] private string relativeWorkingDirectory = @"D:/Projects/GazeFollower";
    [SerializeField] private string relativePythonPath = @"D:/Projects/GazeFollower/.venv/Scripts/python.exe";
    [SerializeField] private string relativeScriptPath = "InputBridge/unity_gaze_bridge.py";
    [SerializeField] private bool autoStartOnAwake = true;
    [SerializeField] private bool promptForCalibrationChoice = true;

    [Header("Runtime Env Overrides")]
    [SerializeField] private bool loadOverridesFromEnvFile = true;
    [SerializeField] private string envFileName = "gaze.env";
    [SerializeField] private bool logResolvedPaths = true;

    private Process gazeProcess;
    private VisualElement startupPromptRoot;
    private bool startupFlowRunning;

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
            StartCoroutine(BeginStartupFlow());
    }

    public void StartGazeFollower()
    {
        StartGazeFollower(GazeLaunchMode.Auto);
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

    private IEnumerator BeginStartupFlow()
    {
        if (startupFlowRunning)
            yield break;

        startupFlowRunning = true;

        if (!promptForCalibrationChoice)
        {
            StartGazeFollower(GazeLaunchMode.Auto);
            startupFlowRunning = false;
            yield break;
        }

        if (!TryResolveLaunchConfiguration(out string workingDirectory, out string pythonExe, out string scriptPath))
        {
            startupFlowRunning = false;
            yield break;
        }

        bool hasSavedCalibration = QuerySavedCalibrationStatus(workingDirectory, pythonExe, scriptPath, out bool statusKnown);
        yield return StartCoroutine(ShowCalibrationChoicePrompt(hasSavedCalibration, statusKnown));
        startupFlowRunning = false;
    }

    private void StartGazeFollower(GazeLaunchMode launchMode)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (gazeProcess != null && !gazeProcess.HasExited)
        {
            RobitLogger.Log("[GazeFollowerRunner] GazeFollower is already running.");
            return;
        }

        if (!TryResolveLaunchConfiguration(out string workingDirectory, out string pythonExe, out string scriptPath))
            return;

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
            Arguments = $"\"{scriptPath}\" --mode {ToModeArgument(launchMode)}",
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
            RobitLogger.Log("[GazeFollowerRunner] Started gaze bridge.");
        }
        catch (Exception ex)
        {
            RobitLogger.LogError($"[GazeFollowerRunner] Failed to start gaze process: {ex.Message}");
        }
#else
        RobitLogger.LogWarning("[GazeFollowerRunner] This runner currently supports Windows builds only.");
#endif
    }

    private bool TryResolveLaunchConfiguration(out string workingDirectory, out string pythonExe, out string scriptPath)
    {
        string workingDirectoryConfig = relativeWorkingDirectory;
        string pythonPathConfig = relativePythonPath;
        string scriptPathConfig = relativeScriptPath;
        LoadEnvOverrides(ref workingDirectoryConfig, ref pythonPathConfig, ref scriptPathConfig);

        if (!TryResolvePath(workingDirectoryConfig, expectFile: false, out workingDirectory, out string workingDetails))
        {
            RobitLogger.LogWarning("[GazeFollowerRunner] Working directory could not be resolved (gaze tracking unavailable).\n" + workingDetails);
            pythonExe = null;
            scriptPath = null;
            return false;
        }

        if (!TryResolvePath(pythonPathConfig, expectFile: true, out pythonExe, out string pythonDetails))
        {
            RobitLogger.LogWarning("[GazeFollowerRunner] Python executable could not be resolved (gaze tracking unavailable).\n" + pythonDetails);
            scriptPath = null;
            return false;
        }

        if (!TryResolvePath(scriptPathConfig, expectFile: true, out scriptPath, out string scriptDetails))
        {
            RobitLogger.LogWarning("[GazeFollowerRunner] Gaze bridge script could not be resolved (gaze tracking unavailable).\n" + scriptDetails);
            return false;
        }

        return true;
    }

    private bool QuerySavedCalibrationStatus(string workingDirectory, string pythonExe, string scriptPath, out bool statusKnown)
    {
        statusKnown = false;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{scriptPath}\" --status-only",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using Process process = Process.Start(startInfo);
            if (process == null)
                return false;

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(15000);

            if (!string.IsNullOrWhiteSpace(stderr))
                RobitLogger.LogWarning("[GazeFollowerRunner] Calibration status probe stderr:\n" + stderr.Trim());

            foreach (string rawLine in stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                if (!line.StartsWith("HAS_SAVED_CALIBRATION:", StringComparison.OrdinalIgnoreCase))
                    continue;

                string payload = line.Substring("HAS_SAVED_CALIBRATION:".Length).Trim();
                statusKnown = true;
                return payload == "1" || payload.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            RobitLogger.LogWarning($"[GazeFollowerRunner] Failed to query calibration status: {ex.Message}");
        }

        return false;
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
            yield break;
        }

        bool selectionMade = false;
        startupPromptRoot = BuildPromptVisualTree(hasSavedCalibration, statusKnown, mode =>
        {
            selectionMade = true;
            RemoveStartupPrompt();
            StartGazeFollower(mode);
        });

        targetDocument.rootVisualElement.Add(startupPromptRoot);

        while (!selectionMade)
            yield return null;
    }

    private VisualElement BuildPromptVisualTree(bool hasSavedCalibration, bool statusKnown, Action<GazeLaunchMode> onSelected)
    {
        var overlay = new VisualElement
        {
            pickingMode = PickingMode.Position
        };
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0;
        overlay.style.top = 0;
        overlay.style.right = 0;
        overlay.style.bottom = 0;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.alignItems = Align.Center;
        overlay.style.backgroundColor = new Color(0.03f, 0.05f, 0.09f, 0.72f);
        overlay.style.paddingLeft = 24;
        overlay.style.paddingRight = 24;
        overlay.style.paddingTop = 24;
        overlay.style.paddingBottom = 24;

        var card = new VisualElement();
        card.style.width = 520;
        card.style.maxWidth = new Length(92, LengthUnit.Percent);
        card.style.paddingLeft = 28;
        card.style.paddingRight = 28;
        card.style.paddingTop = 24;
        card.style.paddingBottom = 24;
        card.style.backgroundColor = new Color(0.09f, 0.12f, 0.18f, 0.98f);
        card.style.borderTopLeftRadius = 18;
        card.style.borderTopRightRadius = 18;
        card.style.borderBottomLeftRadius = 18;
        card.style.borderBottomRightRadius = 18;
        card.style.borderLeftWidth = 1;
        card.style.borderRightWidth = 1;
        card.style.borderTopWidth = 1;
        card.style.borderBottomWidth = 1;
        card.style.borderLeftColor = new Color(0.28f, 0.40f, 0.52f, 1f);
        card.style.borderRightColor = new Color(0.28f, 0.40f, 0.52f, 1f);
        card.style.borderTopColor = new Color(0.28f, 0.40f, 0.52f, 1f);
        card.style.borderBottomColor = new Color(0.28f, 0.40f, 0.52f, 1f);

        var title = new Label("Eye Tracking Setup");
        title.style.fontSize = 24;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = new Color(0.96f, 0.98f, 1f, 1f);
        title.style.marginBottom = 8;

        string subtitleText = statusKnown
            ? (hasSavedCalibration
                ? "A saved gaze calibration was found. Choose whether to reuse it or calibrate again."
                : "No saved gaze calibration was found. Run calibration now to set up tracking.")
            : "Calibration status could not be checked automatically. You can still calibrate now or try the saved calibration path.";

        var subtitle = new Label(subtitleText);
        subtitle.style.whiteSpace = WhiteSpace.Normal;
        subtitle.style.fontSize = 14;
        subtitle.style.color = new Color(0.81f, 0.87f, 0.93f, 1f);
        subtitle.style.marginBottom = 16;

        Button calibrateButton = CreatePromptButton("Calibrate Again", new Color(0.18f, 0.74f, 0.56f, 1f));
        calibrateButton.clicked += () => onSelected?.Invoke(GazeLaunchMode.Calibrate);

        Button useSavedButton = CreatePromptButton(
            hasSavedCalibration ? "Use Saved Calibration" : "No Saved Calibration Found",
            hasSavedCalibration ? new Color(0.19f, 0.50f, 0.86f, 1f) : new Color(0.27f, 0.30f, 0.36f, 1f));
        useSavedButton.SetEnabled(hasSavedCalibration);
        if (hasSavedCalibration)
            useSavedButton.clicked += () => onSelected?.Invoke(GazeLaunchMode.UseSaved);

        var footer = new Label("This follows the same saved-calibration logic used in game_test.py.");
        footer.style.marginTop = 14;
        footer.style.fontSize = 12;
        footer.style.color = new Color(0.62f, 0.70f, 0.78f, 1f);

        card.Add(title);
        card.Add(subtitle);
        card.Add(calibrateButton);
        card.Add(useSavedButton);
        card.Add(footer);
        overlay.Add(card);
        return overlay;
    }

    private static Button CreatePromptButton(string text, Color backgroundColor)
    {
        var button = new Button { text = text };
        button.style.height = 44;
        button.style.marginTop = 8;
        button.style.borderTopLeftRadius = 12;
        button.style.borderTopRightRadius = 12;
        button.style.borderBottomLeftRadius = 12;
        button.style.borderBottomRightRadius = 12;
        button.style.backgroundColor = backgroundColor;
        button.style.color = Color.white;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        return button;
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

    private void OnApplicationQuit()
    {
        StopGazeFollower();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        RemoveStartupPrompt();
        StopGazeFollower();
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        string line = e.Data.Trim();

        if (string.Equals(line, "CALIBRATION_DONE", StringComparison.Ordinal))
        {
            RobitLogger.Log("[GazeFollowerRunner] Calibration completed. Gaze is now driving mouse movement.");
            return;
        }

        if (line.StartsWith("GAZE:", StringComparison.OrdinalIgnoreCase))
        {
            string payload = line.Substring(5).Trim();
            string[] parts = payload.Split(',');
            if (parts.Length == 2 &&
                float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y))
            {
                VirtualInputState.Instance.SetGazePosition(new Vector2(x, y));
                return;
            }
        }

        RobitLogger.Log($"[GazeFollowerRunner][PY] {line}");
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

    private void LoadEnvOverrides(ref string workingDirectoryConfig, ref string pythonPathConfig, ref string scriptPathConfig)
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

        if (values.TryGetValue("GAZE_SCRIPT_PATH", out string script) && !string.IsNullOrWhiteSpace(script))
            scriptPathConfig = script.Trim();

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

        TryPath(Path.Combine(buildRoot, fileName));
        TryPath(Path.Combine(executableRoot, fileName));
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
            string buildRoot = Directory.GetParent(dataPath)?.FullName ?? dataPath;
            string executableRoot = AppDomain.CurrentDomain.BaseDirectory;
            string streamingAssetsPath = Application.streamingAssetsPath;

            TryCandidate(Path.Combine(streamingAssetsPath, normalized));
            TryCandidate(Path.Combine(projectRoot, normalized));
            TryCandidate(Path.Combine(buildRoot, normalized));
            TryCandidate(Path.Combine(executableRoot, normalized));
            TryCandidate(Path.Combine(buildRoot, "..", normalized));
        }

        details = $"[PathResolver] Configured='{configuredPath}', Type={(expectFile ? "File" : "Directory")}\n" +
                  $"[PathResolver] Attempts:\n - {string.Join("\n - ", attempts)}";
        resolvedPath = foundPath;
        return foundPath != null;
    }
}
