using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(-880)]
public class UiAutomationRunner : MonoBehaviour
{
    public static UiAutomationRunner Instance { get; private set; }

    [Header("UI Automation Setup")]
    [SerializeField] private string relativeWorkingDirectory = @"D:/Projects/Robit Ui Automation System/Robit-UI-Automation";
    [SerializeField] private string executablePath = "dotnet";
    [SerializeField] private string arguments = "run";
    [SerializeField] private bool autoStartOnAwake = true;

    [Header("Runtime Env Overrides")]
    [SerializeField] private bool loadOverridesFromEnvFile = true;
    [SerializeField] private string envFileName = "uiautomation.env";
    [SerializeField] private bool logResolvedPaths = true;

    private Process automationProcess;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<UiAutomationRunner>() != null)
            return;

        var go = new GameObject("UiAutomationRunner");
        go.AddComponent<UiAutomationRunner>();
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
            StartAutomationServer();
    }

    public void StartAutomationServer()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (automationProcess != null && !automationProcess.HasExited)
        {
            RobitLogger.Log("[UiAutomationRunner] UI Automation server is already running.");
            return;
        }

        string workingDirectoryConfig = relativeWorkingDirectory;
        string exePathConfig = executablePath;
        string argsConfig = arguments;
        LoadEnvOverrides(ref workingDirectoryConfig, ref exePathConfig, ref argsConfig);

        if (!RunnerPathResolver.TryResolvePath(workingDirectoryConfig, expectFile: false, out string workingDirectory, out string workingDetails))
        {
            RobitLogger.LogWarning("[UiAutomationRunner] Working directory could not be resolved (UI Automation unavailable).\n" + workingDetails);
            return;
        }

        if (logResolvedPaths)
        {
            RobitLogger.Log("[UiAutomationRunner] Using paths:\n" +
                            $" - WorkingDir: {workingDirectory}\n" +
                            $" - Executable: {exePathConfig}\n" +
                            $" - Arguments : {argsConfig}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePathConfig,
            Arguments = argsConfig,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        automationProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        automationProcess.OutputDataReceived += OnOutputDataReceived;
        automationProcess.ErrorDataReceived += OnErrorDataReceived;
        automationProcess.Exited += OnProcessExited;

        try
        {
            automationProcess.Start();
            automationProcess.BeginOutputReadLine();
            automationProcess.BeginErrorReadLine();
            RobitLogger.Log("[UiAutomationRunner] Started UI Automation server.");
        }
        catch (Exception ex)
        {
            RobitLogger.LogError($"[UiAutomationRunner] Failed to start UI Automation server: {ex.Message}");
        }
#else
        RobitLogger.LogWarning("[UiAutomationRunner] This runner currently supports Windows builds only.");
#endif
    }

    public void StopAutomationServer()
    {
        if (automationProcess == null)
            return;

        try
        {
            if (!automationProcess.HasExited)
                automationProcess.Kill();
        }
        catch (Exception ex)
        {
            RobitLogger.LogWarning($"[UiAutomationRunner] Failed to stop UI Automation process: {ex.Message}");
        }
        finally
        {
            CleanupProcessHandlers();
        }
    }

    private void OnApplicationQuit()
    {
        StopAutomationServer();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        StopAutomationServer();
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        RobitLogger.Log($"[UiAutomationRunner][Server] {e.Data.Trim()}");
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        RobitLogger.LogWarning($"[UiAutomationRunner][Server-ERR] {e.Data.Trim()}");
    }

    private void OnProcessExited(object sender, EventArgs e)
    {
        RobitLogger.Log("[UiAutomationRunner] UI Automation process exited.");
    }

    private void CleanupProcessHandlers()
    {
        if (automationProcess == null)
            return;

        automationProcess.OutputDataReceived -= OnOutputDataReceived;
        automationProcess.ErrorDataReceived -= OnErrorDataReceived;
        automationProcess.Exited -= OnProcessExited;
        automationProcess.Dispose();
        automationProcess = null;
    }

    private void LoadEnvOverrides(ref string workingDirectoryConfig, ref string exePathConfig, ref string argsConfig)
    {
        if (!loadOverridesFromEnvFile)
            return;

        if (!RunnerPathResolver.TryResolveEnvFilePath(envFileName, out string envPath, out string details))
        {
            if (logResolvedPaths)
                RobitLogger.Log("[UiAutomationRunner] Env file not found. Using inspector values.\n" + details);
            return;
        }

        Dictionary<string, string> values = RunnerPathResolver.ParseEnvFile(envPath);
        if (values.TryGetValue("UI_AUTO_WORKING_DIR", out string wd) && !string.IsNullOrWhiteSpace(wd))
            workingDirectoryConfig = wd.Trim();

        if (values.TryGetValue("UI_AUTO_EXE_PATH", out string exe) && !string.IsNullOrWhiteSpace(exe))
            exePathConfig = exe.Trim();

        if (values.TryGetValue("UI_AUTO_ARGS", out string args) && !string.IsNullOrWhiteSpace(args))
            argsConfig = args.Trim();

        RobitLogger.Log($"[UiAutomationRunner] Loaded env overrides from: {envPath}");
    }

}