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
            UnityEngine.Debug.Log($"{LogPrefix} UI Automation server is already running.");
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
            UnityEngine.Debug.LogWarning(
                $"{LogPrefix} Working directory could not be resolved (UI Automation unavailable).\n"
                + workingDetails);
            return;
        }

        if (logResolvedPaths)
        {
            UnityEngine.Debug.Log(
                $"{LogPrefix} Using paths:\n"
                + $" - WorkingDir: {workingDirectory}\n"
                + $" - Executable: {exePathConfig}\n"
                + $" - Arguments : {argsConfig}");
        }

        bool launched = false;
        string securePath = @"C:\Program Files\RobitUiAutomation\Robit-UI-Automation.exe";

        // Try UIAccess launcher if using "dotnet run" configuration
        if (exePathConfig == "dotnet" && argsConfig == "run")
        {
            bool hasSecureExe = File.Exists(securePath);
            if (!hasSecureExe)
            {
                // Attempt to run the deployment script automatically!
                try
                {
                    UnityEngine.Debug.LogWarning($"{LogPrefix} Secure UIAccess executable not found at '{securePath}'. Triggering self-signing and deployment script with Administrator elevation...");

                    string scriptPath = Path.Combine(workingDirectory, "deploy_uiaccess.ps1");
                    if (File.Exists(scriptPath))
                    {
                        var deployPsi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
                            Verb = "runas",
                            UseShellExecute = true,
                            CreateNoWindow = false
                        };

                        using (var deployProcess = Process.Start(deployPsi))
                        {
                            if (deployProcess != null)
                            {
                                deployProcess.WaitForExit(); // Wait for it to build and deploy!
                            }
                        }

                        if (File.Exists(securePath))
                        {
                            UnityEngine.Debug.Log($"{LogPrefix} Deployment completed successfully. Created secure signed UIAccess executable.");
                            hasSecureExe = true;
                        }
                        else
                        {
                            UnityEngine.Debug.LogError($"{LogPrefix} Deployment completed, but secure executable was not found.");
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"{LogPrefix} Deployment script not found at '{scriptPath}'.");
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"{LogPrefix} Failed to run deployment script: {ex.Message}");
                }
            }

            if (hasSecureExe)
            {
                if (logResolvedPaths)
                {
                    UnityEngine.Debug.Log($"{LogPrefix} Attempting to launch secure signed UIAccess executable at '{securePath}'...");
                }

                try
                {
                    var securePsi = new ProcessStartInfo
                    {
                        FileName               = securePath,
                        Arguments              = "",
                        WorkingDirectory       = workingDirectory,
                        UseShellExecute        = false,
                        RedirectStandardInput  = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        CreateNoWindow         = true,
                    };
                    automationProcess = StartManagedProcess(securePsi);
                    UnityEngine.Debug.Log($"{LogPrefix} Started secure UI Automation server.");
                    launched = true;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning(
                        $"{LogPrefix} Failed to start secure UI Automation server: {ex.Message}.\n"
                        + "This error ('requires elevation') occurs when the application has UIAccess=true in its manifest but the parent process (Unity Editor) is not running as Administrator.\n"
                        + "Falling back to standard development build...");
                }
            }
        }

        // If not launched via secure UIAccess (or launching it failed), run development fallback
        if (!launched)
        {
            // If the original config was dotnet run, check for a pre-compiled local development build
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
                        UnityEngine.Debug.Log($"{LogPrefix} Found pre-compiled local development executable at '{precompiledExe}'. Bypassing 'dotnet run' for faster startup.");
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
                UnityEngine.Debug.Log($"{LogPrefix} Started development UI Automation server.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{LogPrefix} Failed to start UI Automation server fallback: {ex.Message}");
            }
        }
#else
        UnityEngine.Debug.LogWarning($"{LogPrefix} This runner currently supports Windows builds only.");
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
            UnityEngine.Debug.Log($"{LogPrefix}[SEND] {json}");
            automationProcess.StandardInput.WriteLine(json);
            automationProcess.StandardInput.Flush();
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"{LogPrefix} Failed to send cmd: {ex.Message}");
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
            UnityEngine.Debug.Log($"{LogPrefix}[RECV] {payload}");

            if (mainThreadContext != null)
                mainThreadContext.Post(_ => OnMessageReceived?.Invoke(payload), null);
            else
                OnMessageReceived?.Invoke(payload);

            return;
        }

        // Bypass base class which uses RobitLogger so that standard output is visible in standalone builds
        UnityEngine.Debug.Log($"{LogPrefix}[PY] {line}");
    }

    protected override void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        // Bypass base class which uses RobitLogger so that error output is visible in standalone builds
        UnityEngine.Debug.LogWarning($"{LogPrefix}[Server-ERR] {e.Data.Trim()}");
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
                UnityEngine.Debug.Log($"{LogPrefix} Env file not found. Using inspector values.\n" + details);
            return;
        }

        Dictionary<string, string> values = RunnerPathResolver.ParseEnvFile(envPath);

        if (values.TryGetValue("UI_AUTO_WORKING_DIR", out string wd) && !string.IsNullOrWhiteSpace(wd))
            workingDirectoryConfig = wd.Trim();

        if (values.TryGetValue("UI_AUTO_EXE_PATH", out string exe) && !string.IsNullOrWhiteSpace(exe))
            exePathConfig = exe.Trim();

        if (values.TryGetValue("UI_AUTO_ARGS", out string args) && !string.IsNullOrWhiteSpace(args))
            argsConfig = args.Trim();

        UnityEngine.Debug.Log($"{LogPrefix} Loaded env overrides from: {envPath}");
    }
}