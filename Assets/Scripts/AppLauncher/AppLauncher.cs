using UnityEngine;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;
using Robit.LauncherSystem;
using System.Runtime.CompilerServices;

public class AppLauncher : MonoBehaviour
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

    public void LaunchApplication(string path, string workingDirectory)
    {
        try
        {
            if (currentProcess != null && !currentProcess.HasExited)
            {
                _processRunner.Close(currentProcess);
            }

            currentProcess = _processRunner.Start(path, workingDirectory);
            
            _sceneLoader.LoadScene("OverlayScene"); 
        }
        catch (System.Exception e)
        {
            RobitLogger.LogError($"Failed to launch application: {path}, Error: {e.Message}");
        }
    }

    /// <summary>
    /// Closes the overlay, kills the app, and shows the desktop cards again.
    /// Called by the Home macro button.
    /// </summary>
    public void ReturnToDesktop()
    {
        // Show the AppLauncher UI again
        var appUI = FindFirstObjectByType<AppLauncherUIToolkit>();
        if (appUI != null)
        {
            var doc = appUI.GetComponent<UnityEngine.UIElements.UIDocument>();
            if (doc != null && doc.rootVisualElement != null)
                doc.rootVisualElement.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
        }

        // Hide macros with shrink
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

    public void GoHome()
    {
        CloseCurrentApp();
        StartCoroutine(GoHomeRoutine());
    }

    private System.Collections.IEnumerator GoHomeRoutine()
    {   
        yield return null;
        _sceneLoader.LoadScene("MainScene");
        RobitLogger.Log("[AppLauncher] Returning to Home.");
    }

}

