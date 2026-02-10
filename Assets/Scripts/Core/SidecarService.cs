using UnityEngine;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;

namespace Core
{
    // Manages the lifecycle of the external Python sidecar process for eye tracking.
    // Ensures the sidecar starts with Unity and shuts down gracefully.
    public class SidecarService : MonoBehaviour
    {
        public static SidecarService Instance { get; private set; }

        [Header("Sidecar Configuration")]
        [SerializeField] private string scriptPath = "Assets/Plugins/EyeGestures/eyeGestures_to_unity.py";
        [SerializeField] private string venvPath = "Assets/Plugins/EyeGestures/.venv";
        [SerializeField] private string arguments = ""; // e.g. "--headless"
        [SerializeField] private bool showConsole = false; // Useful for debugging
        [SerializeField] private bool autoStartInEditor = true;

        private Process sidecarProcess;
        private bool isRunning = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            // Auto-create if it doesn't exist
            if (Instance == null)
            {
                var existing = FindFirstObjectByType<SidecarService>();
                if (existing == null)
                {
                    var go = new GameObject("SidecarService");
                    go.AddComponent<SidecarService>();
                    DontDestroyOnLoad(go);
                    UnityEngine.Debug.Log("[Sidecar] Service auto-initialized.");
                }
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (Application.isEditor && autoStartInEditor)
            {
                StartSidecar();
            }
            else if (!Application.isEditor)
            {
                UnityEngine.Debug.LogWarning("[Sidecar] Build path logic not yet implemented. Sidecar won't start in build.");
            }
        }

        private void OnApplicationQuit()
        {
            StopSidecar();
        }

        private void OnDestroy()
        {
            StopSidecar();
        }

        public void StartSidecar()
        {
            if (isRunning) return;

            string pythonExe = GetPythonPath();
            string script = Path.GetFullPath(Path.Combine(Application.dataPath, "..", scriptPath));

            if (!File.Exists(pythonExe))
            {
                UnityEngine.Debug.LogError($"[Sidecar] Python executable not found at: {pythonExe}");
                return;
            }

            if (!File.Exists(script))
            {
                UnityEngine.Debug.LogError($"[Sidecar] Script not found at: {script}");
                return;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = pythonExe;
                // Combine script path and user arguments
                startInfo.Arguments = $"\"{script}\" {arguments}"; 
                
                if (!showConsole)
                {
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    startInfo.CreateNoWindow = true;
                    startInfo.UseShellExecute = false; // Required for CreateNoWindow=true
                    startInfo.RedirectStandardOutput = true;
                    startInfo.RedirectStandardError = true;
                    startInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
                    startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
                }
                else
                {
                    startInfo.UseShellExecute = true; // Opens separate terminal window
                }

                startInfo.WorkingDirectory = Path.GetDirectoryName(script);

                UnityEngine.Debug.Log($"[Sidecar] Launching: {pythonExe} {startInfo.Arguments}");

                sidecarProcess = Process.Start(startInfo);
                isRunning = true;
                
                if (!showConsole)
                {
                    // log forwarding
                    sidecarProcess.OutputDataReceived += (s, e) => { 
                        if (!string.IsNullOrEmpty(e.Data)) 
                            UnityEngine.Debug.Log($"[Sidecar Py] {e.Data}"); 
                    };
                    
                    sidecarProcess.ErrorDataReceived += (s, e) => { 
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            string msg = e.Data;
                            if (msg.Contains("INFO"))
                                UnityEngine.Debug.Log($"[Sidecar Py Info] {msg}");
                            else if (msg.Contains("WARNING"))
                                UnityEngine.Debug.LogWarning($"[Sidecar Py Warn] {msg}");
                            else
                                UnityEngine.Debug.LogError($"[Sidecar Py Error] {msg}");
                        }
                    };
                    sidecarProcess.BeginOutputReadLine();
                    sidecarProcess.BeginErrorReadLine();
                }

                UnityEngine.Debug.Log($"[Sidecar] Started with PID: {sidecarProcess.Id}");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[Sidecar] Failed to start: {e.Message}");
            }
        }

        public void StopSidecar()
        {
            if (sidecarProcess != null && !sidecarProcess.HasExited)
            {
                try
                {
                    UnityEngine.Debug.Log("[Sidecar] Stopping process...");
                    sidecarProcess.Kill();
                    sidecarProcess.WaitForExit(1000); // Wait up to 1s
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[Sidecar] Error stopping process: {e.Message}");
                }
                finally
                {
                    sidecarProcess.Dispose();
                    sidecarProcess = null;
                    isRunning = false;
                }
            }
        }

        private string GetPythonPath()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string venv = Path.Combine(projectRoot, venvPath);
            
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                return Path.Combine(venv, "Scripts", "python.exe");
            }
            else
            {
                return Path.Combine(venv, "bin", "python"); 
            }
        }
    }
}
