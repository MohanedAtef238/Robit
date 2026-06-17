using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

[DefaultExecutionOrder(-880)]
public class UiAutomationRunner : BaseProcessRunner<UiAutomationRunner>
{
    // ── LogPrefix (required by BaseProcessRunner) ─────────────────────────────
    protected override string LogPrefix => "[UiAutomationRunner]";

    // ── Inspector fields ──────────────────────────────────────────────────────
    [Header("UI Automation Setup")]
    [SerializeField] private string relativeWorkingDirectory = @"D:/Projects/Robit Ui Automation System/Robit-UI-Automation";
    [SerializeField] private string executablePath           = "dotnet";
    [SerializeField] private string arguments                = "run";
    [SerializeField] private bool   autoStartOnAwake         = true;

    [Header("Runtime Env Overrides")]
    [SerializeField] private bool   loadOverridesFromEnvFile = true;
    [SerializeField] private string envFileName              = "uiautomation.env";
    [SerializeField] private bool   logResolvedPaths         = true;

    // ── State ─────────────────────────────────────────────────────────────────
    private Process automationProcess;
    private SynchronizationContext mainThreadContext;

    public event Action<string> OnMessageReceived;

    // ── Bootstrap (commented out — started externally) ────────────────────────
    // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<UiAutomationRunner>() != null)
            return;

        var go = new GameObject("UiAutomationRunner");
        go.AddComponent<UiAutomationRunner>();
    }

    // ── Awake hook (autostart) ────────────────────────────────────────────────

    /// <summary>
    /// Called by the base Awake() after the singleton is established.
    /// Handles the autoStartOnAwake behaviour specific to UiAutomationRunner.
    /// </summary>
    protected override void OnAfterAwake()
    {
        mainThreadContext = SynchronizationContext.Current;

        if (autoStartOnAwake)
            StartAutomationServer();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void StartAutomationServer()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (automationProcess != null && !automationProcess.HasExited)
        {
            RobitLogger.Log($"{LogPrefix} UI Automation server is already running.");
            return;
        }

        string workingDirectoryConfig = relativeWorkingDirectory;
        string exePathConfig          = executablePath;
        string argsConfig             = arguments;
        LoadEnvOverrides(ref workingDirectoryConfig, ref exePathConfig, ref argsConfig);

        if (!RunnerPathResolver.TryResolvePath(
                workingDirectoryConfig,
                expectFile: false,
                out string workingDirectory,
                out string workingDetails))
        {
            RobitLogger.LogWarning(
                $"{LogPrefix} Working directory could not be resolved (UI Automation unavailable).\n"
                + workingDetails);
            return;
        }

        if (logResolvedPaths)
        {
            RobitLogger.Log(
                $"{LogPrefix} Using paths:\n"
                + $" - WorkingDir: {workingDirectory}\n"
                + $" - Executable: {exePathConfig}\n"
                + $" - Arguments : {argsConfig}");
        }

        // Try to find a pre-compiled executable if configured to run via "dotnet run"
        if (exePathConfig == "dotnet" && argsConfig == "run")
        {
            string[] candidatePaths = new string[]
            {
                Path.Combine(workingDirectory, "bin", "Release", "net48", "win-x64", "publish", "Robit-UI-Automation.exe"),
                Path.Combine(workingDirectory, "bin", "Debug", "net48", "Robit-UI-Automation.exe"),
                Path.Combine(workingDirectory, "bin", "Release", "net48", "win-x64", "Robit-UI-Automation.exe")
            };

            string precompiledExe = null;
            DateTime latestTime = DateTime.MinValue;

            foreach (var candidate in candidatePaths)
            {
                if (File.Exists(candidate))
                {
                    var fi = new FileInfo(candidate);
                    if (fi.LastWriteTime > latestTime)
                    {
                        latestTime = fi.LastWriteTime;
                        precompiledExe = candidate;
                    }
                }
            }

            if (precompiledExe != null)
            {
                exePathConfig = precompiledExe;
                argsConfig = "";
                if (logResolvedPaths)
                {
                    RobitLogger.Log($"{LogPrefix} Found pre-compiled executable at '{precompiledExe}'. Bypassing 'dotnet run' for faster startup and lower resource usage.");
                }
            }
        }

        string resolvedExePath = exePathConfig;
        if (exePathConfig != "dotnet")
        {
            if (RunnerPathResolver.TryResolvePath(
                    exePathConfig,
                    expectFile: true,
                    out string resolvedExe,
                    out string exeDetails))
            {
                resolvedExePath = resolvedExe;
            }
        }

        // BuildProcessStartInfo derives WorkingDirectory from exePath, but
        // UiAutomationRunner's working directory is configured independently
        // (it may run dotnet from a project folder, not next to an exe).
        // We therefore build the PSI manually for this runner.
        var psi = new ProcessStartInfo
        {
            FileName               = resolvedExePath,
            Arguments              = argsConfig,
            WorkingDirectory       = workingDirectory,
            UseShellExecute        = false,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        try
        {
            automationProcess = StartManagedProcess(psi);
            RobitLogger.Log($"{LogPrefix} Started UI Automation server.");
        }
        catch (Exception ex)
        {
            RobitLogger.LogError($"{LogPrefix} Failed to start UI Automation server: {ex.Message}");
        }
#else
        RobitLogger.LogWarning($"{LogPrefix} This runner currently supports Windows builds only.");
#endif
    }

    public void StopAutomationServer()
    {
        TerminateProcess(ref automationProcess);
    }

    public void SendCmd(string json)
    {
        if (automationProcess == null || automationProcess.HasExited)
            return;

        try
        {
            automationProcess.StandardInput.WriteLine(json);
            automationProcess.StandardInput.Flush();
        }
        catch (Exception ex)
        {
            RobitLogger.LogWarning($"{LogPrefix} Failed to send cmd: {ex.Message}");
        }
    }

    /// <summary>Required by BaseProcessRunner — called on quit and destroy.</summary>
    public override void StopRunner() => StopAutomationServer();

    protected override void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        string line = e.Data.Trim();
        if (line.StartsWith("CMD_RESPONSE:", StringComparison.OrdinalIgnoreCase))
        {
            string payload = line.Substring("CMD_RESPONSE:".Length);

            if (mainThreadContext != null)
                mainThreadContext.Post(_ => OnMessageReceived?.Invoke(payload), null);
            else
                OnMessageReceived?.Invoke(payload);

            return;
        }

        base.OnOutputDataReceived(sender, e);
    }

    protected override void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        RobitLogger.LogWarning($"{LogPrefix}[Server-ERR] {e.Data.Trim()}");
    }

    // ── Env file overrides ────────────────────────────────────────────────────

    private void LoadEnvOverrides(
        ref string workingDirectoryConfig,
        ref string exePathConfig,
        ref string argsConfig)
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

        if (values.TryGetValue("UI_AUTO_WORKING_DIR", out string wd) && !string.IsNullOrWhiteSpace(wd))
            workingDirectoryConfig = wd.Trim();

        if (values.TryGetValue("UI_AUTO_EXE_PATH", out string exe) && !string.IsNullOrWhiteSpace(exe))
            exePathConfig = exe.Trim();

        if (values.TryGetValue("UI_AUTO_ARGS", out string args) && !string.IsNullOrWhiteSpace(args))
            argsConfig = args.Trim();

        RobitLogger.Log($"{LogPrefix} Loaded env overrides from: {envPath}");
    }
}