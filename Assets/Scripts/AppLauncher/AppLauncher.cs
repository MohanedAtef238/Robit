using UnityEngine;
using System;
using System.Diagnostics;
using UnityEngine.SceneManagement;
using System.IO;

public class AppLauncher : MonoBehaviour
{
    public static AppLauncher Instance;
    private Process currentProcess;

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

    /// <summary>
    /// Returns the currently running external process, or null.
    /// Used by CaptureTextureRenderer to find the target window.
    /// </summary>
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
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to launch application: {path}, Error: {e.Message}");
        }
    }

    public void CloseCurrentApp()
    {
        // Stop any active texture capture before closing the app
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

    /// <summary>
    /// Safely transitions back to the Home scene.
    /// In the new architecture, this stops the capture session instead of
    /// resetting DWM transparency / window styles.
    /// </summary>
    public void GoHome()
    {
        // 1. Stop texture capture (replaces the legacy WindowManager.MakeOpaque())
        StopCaptureIfActive();

        // 2. Close any running external app
        CloseCurrentApp();

        // 3. Defer scene load — prevents UI Toolkit from freezing
        StartCoroutine(GoHomeRoutine());
    }

    private System.Collections.IEnumerator GoHomeRoutine()
    {   
        yield return null; // Wait for UI Toolkit to finish discharging events
        SceneManager.LoadScene("MainScene");
        UnityEngine.Debug.Log("[AppLauncher] Returning to Home.");
    }

    /// <summary>
    /// Finds and deactivates any active HolePunchController in the scene.
    /// </summary>
    private void StopCaptureIfActive()
    {
        var holePunch = FindFirstObjectByType<HolePunchController>();
        if (holePunch != null)
        {
            holePunch.Deactivate();
        }
    }
}
