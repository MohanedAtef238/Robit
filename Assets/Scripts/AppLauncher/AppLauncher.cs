using UnityEngine;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;

public class AppLauncher : MonoBehaviour
{
    public static AppLauncher Instance;
    private Process currentProcess;
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

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {   
            Destroy(gameObject);
        }
    }

    public Process CurrentProcess => currentProcess;

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
            UnityEngine.Debug.LogError($"Failed to launch application: {path}, Error: {e.Message}");
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

        UnityEngine.Debug.Log("[AppLauncher] Returned to desktop.");
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
                UnityEngine.Debug.LogWarning($"Failed to close process gracefully: {e.Message}");
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
        UnityEngine.Debug.Log("[AppLauncher] Returning to Home.");
    }

}
