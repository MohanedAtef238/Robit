using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        // BuildProcessStartInfo derives WorkingDirectory from exePath, but
        // UiAutomationRunner's working directory is configured independently
        // (it may run dotnet from a project folder, not next to an exe).
        // We therefore build the PSI manually for this runner.
        var psi = new ProcessStartInfo
        {
            FileName               = exePathConfig,
            Arguments              = argsConfig,
            WorkingDirectory       = workingDirectory,
            UseShellExecute        = false,
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

    /// <summary>Required by BaseProcessRunner — called on quit and destroy.</summary>
    public override void StopRunner() => StopAutomationServer();

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