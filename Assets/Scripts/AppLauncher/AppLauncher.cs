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

    /// Desktop shortcuts discovered by DesktopParser in MainScene.
    /// Populated before scene transition so AppCyclerController can read it in OverlayScene.
    public List<ShortcutInfo> CachedShortcuts { get; set; } = new();

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
                currentProcess.CloseMainWindow();
                currentProcess.Dispose();
            }

            ProcessStartInfo startInfo = new ProcessStartInfo(path);
            if (!string.IsNullOrEmpty(workingDirectory) && Directory.Exists(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            currentProcess = Process.Start(startInfo);
            
            SceneManager.LoadScene("OverlayScene"); 
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
                currentProcess.CloseMainWindow();
                currentProcess.Dispose();
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
        SceneManager.LoadScene("MainScene");
        UnityEngine.Debug.Log("[AppLauncher] Returning to Home.");
    }

}
