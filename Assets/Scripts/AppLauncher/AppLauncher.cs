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

    /// Safely transitions back to the Home scene.
    /// Handles transparency reset and app closing on this persistent object
    /// to avoid race conditions when the calling scene unloads.
    public void GoHome()
    {
        // 1. Force window to be opaque immediately. 
        // We do this directly via WindowManager (skipping the Transparency script's coroutine 
        // which would die when the scene unloads).
        WindowManager.MakeOpaque();

        // 2. Close any running external app
        CloseCurrentApp();

        // 3. Load the Main Scene
        SceneManager.LoadScene("MainScene");

        UnityEngine.Debug.Log("[AppLauncher] Returning to Home.");
    }
}
