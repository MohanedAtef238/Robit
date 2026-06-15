using UnityEngine;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using Robit.LauncherSystem;
using System.Runtime.CompilerServices;

public class AppLauncher : MonoBehaviour, IAppLauncher
{
    internal static AppLauncher Instance;
    private IProcess currentProcess;
    private IProcessRunner _processRunner = new WindowsProcessRunner();
    private ISceneLoader _sceneLoader = new UnitySceneLoader();

    public List<ShortcutInfo> CachedShortcuts { get; set; } = new();

    public IProcessRunner ProcessRunner { get => _processRunner; set => _processRunner = value; }
    public ISceneLoader SceneLoader { get => _sceneLoader; set => _sceneLoader = value; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyFPSCap()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("AppLauncher");
        go.AddComponent<AppLauncher>();
        DontDestroyOnLoad(go);
    }

    protected void Awake() => Initialize();


    internal void Initialize()
    {
        if (Instance == null)
        {
            Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (Instance != this)
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }

    public IProcess CurrentProcess => currentProcess;

    /// Launches an external application and closes the home page.
    /// No scene load is needed — we remain in OverlayScene.
    public void LaunchApplication(string path, string workingDirectory)
    {
        try
        {
            if (currentProcess != null && !currentProcess.HasExited)
            {
                _processRunner.Close(currentProcess);
            }

            currentProcess = _processRunner.Start(path, workingDirectory);

            // Close the home page overlay; the external app takes the foreground.
            var homeCtrl = FindFirstObjectByType<HomePageController>();
            homeCtrl?.Close();
        }
        catch (Exception e)
        {
            RobitLogger.LogError($"Failed to launch application: {path}, Error: {e.Message}");
        }
    }

    /// Shows the home page and the app launcher carousel.
    /// Called by ReturnToDesktopAction and GoHome().
    public void ReturnToDesktop()
    {
        var homeCtrl = FindFirstObjectByType<HomePageController>();
        if (homeCtrl != null && !homeCtrl.IsOpen)
        {
            homeCtrl.Open();
        }

        var launcherCtrl = FindFirstObjectByType<AppLauncherUIToolkit>();
        if (launcherCtrl != null)
        {
            launcherCtrl.Open();
        }

        // Collapse the macro button ring if it is open.
        var macroCtrl = FindFirstObjectByType<MacroButtonController>();
        if (macroCtrl != null)
            macroCtrl.HideWithShrink();

        RobitLogger.Log("[AppLauncher] Returned to desktop.");
    }

    public void CloseCurrentApp()
    {
        if (currentProcess != null && !currentProcess.HasExited)
        {
            try
            {
                _processRunner.Close(currentProcess);
            }
            catch (Exception e)
            {
                RobitLogger.LogWarning($"Failed to close process gracefully: {e.Message}");
            }
            finally
            {
                currentProcess = null;
            }
        }
    }

    /// Closes the running app and brings up the home page + app launcher carousel.
    public void GoHome()
    {
        CloseCurrentApp();
        ReturnToDesktop();
    }
}
