using UnityEngine;
using System.Diagnostics;
using System.IO;

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
        [SerializeField] private string arguments = "--headless"; // Default headless when launched as sidecar
        [SerializeField] private bool showConsole = false; // Set true to debug sidecar output in a visible terminal
        [SerializeField] private bool autoStartInEditor = true;

        private Process sidecarProcess;
        private bool isRunning = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;

            var existing = FindFirstObjectByType<SidecarService>();
            if (existing == null)
            {
                var go = new GameObject("SidecarService");
                go.AddComponent<SidecarService>();
                DontDestroyOnLoad(go);
                UnityEngine.Debug.Log("[Sidecar] Service auto-initialized.");
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
                // In a build, the venv path would need to be relative to the build output.
                // For now, warn and skip.
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
                string batPath = Path.GetFullPath(Path.Combine(Application.dataPath, "Plugins", "EyeGestures", "run_eye_tracker.bat"));
                UnityEngine.Debug.LogError(
                    $"[Sidecar] Python venv not found at: {pythonExe}\n" +
                    $"  → Run the setup script first: {batPath}\n" +
                    $"  → This creates the .venv and installs EyeGestures dependencies.");
                return;
            }

            if (!File.Exists(script))
            {
                UnityEngine.Debug.LogError($"[Sidecar] Bridge script not found at: {script}");
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = $"\"{script}\" {arguments}",
                    WorkingDirectory = Path.GetDirectoryName(script)
                };

                if (!showConsole)
                {
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    startInfo.CreateNoWindow = true;
                    startInfo.UseShellExecute = false;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.RedirectStandardError = true;
                    startInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
                    startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
                }
                else
                {
                    startInfo.UseShellExecute = true;
                }

                UnityEngine.Debug.Log($"[Sidecar] Launching: {pythonExe} {startInfo.Arguments}");

                sidecarProcess = Process.Start(startInfo);
                isRunning = true;
                
                if (!showConsole)
                {
                    sidecarProcess.OutputDataReceived += (s, e) => { 
                        if (!string.IsNullOrEmpty(e.Data)) 
                            UnityEngine.Debug.Log($"[Sidecar Py] {e.Data}"); 
                    };
                    
                    sidecarProcess.ErrorDataReceived += (s, e) => { 
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            string msg = e.Data;
                            // Python logging modules emit INFO/WARNING on stderr
                            if (msg.Contains("INFO"))
                                UnityEngine.Debug.Log($"[Sidecar Py] {msg}");
                            else if (msg.Contains("WARNING"))
                                UnityEngine.Debug.LogWarning($"[Sidecar Py] {msg}");
                            else
                                UnityEngine.Debug.LogError($"[Sidecar Py] {msg}");
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
            if (sidecarProcess == null || sidecarProcess.HasExited)
            {
                isRunning = false;
                return;
            }

            try
            {
                UnityEngine.Debug.Log("[Sidecar] Stopping process...");
                sidecarProcess.Kill();
                sidecarProcess.WaitForExit(2000);
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
