using UnityEngine;
using System;
using System.Diagnostics;
using UnityEngine.SceneManagement;
using System.IO;

public class AppLauncher : MonoBehaviour
{
    public static AppLauncher Instance;
    private Process currentProcess;

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
            UnityEngine.Debug.Log($"[AppLauncher] Sending '{path}' to HolePunchController.");

            var holePunch = FindFirstObjectByType<HolePunchController>();
            if (holePunch != null)
            {
                holePunch.targetProcessName = System.IO.Path.GetFileNameWithoutExtension(path);
                holePunch.executablePath = path;

                // Open the overlay panel animation
                var captureController = FindFirstObjectByType<RobitCaptureController>();
                if (captureController != null)
                    captureController.OpenPanel();
                
                // Activate the hole punch
                holePunch.Activate();
            }
            else
            {
                UnityEngine.Debug.LogWarning("[AppLauncher] No HolePunchController found! Launching normally.");
                if (currentProcess != null && !currentProcess.HasExited)
                {
                    currentProcess.CloseMainWindow();
                    currentProcess.Dispose();
                }

                ProcessStartInfo startInfo = new ProcessStartInfo(path);
                if (!string.IsNullOrEmpty(workingDirectory) && Directory.Exists(workingDirectory))
                    startInfo.WorkingDirectory = workingDirectory;

                currentProcess = Process.Start(startInfo);
            }

            // Hide the AppLauncher UI
            var appUI = FindFirstObjectByType<AppLauncherUIToolkit>();
            if (appUI != null)
            {
                var doc = appUI.GetComponent<UnityEngine.UIElements.UIDocument>();
                if (doc != null && doc.rootVisualElement != null)
                    doc.rootVisualElement.style.display = UnityEngine.UIElements.DisplayStyle.None;
            }

            // Show macro buttons with bounce animation
            var macroCtrl = FindFirstObjectByType<MacroButtonController>();
            if (macroCtrl != null)
                macroCtrl.ShowWithBounce();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to prepare application: {path}, Error: {e.Message}");
        }
    }

    /// <summary>
    /// Closes the overlay, kills the app, and shows the desktop cards again.
    /// Called by the Home macro button.
    /// </summary>
    public void ReturnToDesktop()
    {
        // Close the overlay panel animation
        var captureController = FindFirstObjectByType<RobitCaptureController>();
        if (captureController != null)
            captureController.ClosePanel();

        // Kill the running app
        var holePunch = FindFirstObjectByType<HolePunchController>();
        if (holePunch != null)
            holePunch.CloseTargetApp();

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
        StopCaptureIfActive();

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
        StopCaptureIfActive();
        CloseCurrentApp();
        StartCoroutine(GoHomeRoutine());
    }

    private System.Collections.IEnumerator GoHomeRoutine()
    {   
        yield return null;
        SceneManager.LoadScene("MainScene");
        UnityEngine.Debug.Log("[AppLauncher] Returning to Home.");
    }

    private void StopCaptureIfActive()
    {
        var holePunch = FindFirstObjectByType<HolePunchController>();
        if (holePunch != null)
            holePunch.Deactivate();
    }
}
