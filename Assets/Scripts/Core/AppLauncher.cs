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
}
