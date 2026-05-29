using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[DefaultExecutionOrder(-900)]
public class GazeFollowerRunner : MonoBehaviour
{
    private enum GazeLaunchMode
    {
        Auto,
        Calibrate,
        UseSaved
    }

    public static GazeFollowerRunner Instance { get; private set; }

    private const string CalibrationSceneName = "GazeCalibrationScene";

    [Header("Multimodal Setup")]
    [SerializeField] private string relativeExePath = @"Multimodal_UDP/unity_gaze_bridge.exe";
    [SerializeField] private bool autoStartOnAwake = true;
    [SerializeField] private bool promptForCalibrationChoice = true;
    
    [Header("Debug")]
    [SerializeField] private bool simulateGazeWithArrowKeys = true;

    private Process gazeProcess;
    private UdpClient udpClient;
    private CancellationTokenSource udpCancellation;

    private VisualElement startupPromptRoot;
    private bool startupFlowRunning;
    private string activeProfileId;

    private ConcurrentQueue<Vector2> gazePacketQueue = new ConcurrentQueue<Vector2>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<GazeFollowerRunner>() != null)
            return;

        var go = new GameObject("GazeFollowerRunner");
        go.AddComponent<GazeFollowerRunner>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // if (autoStartOnAwake)
        //     StartCoroutine(BeginStartupFlow());
    }

    private void Update()
    {
        // Drain all packets, keep only the latest gaze position to minimize latency
        Vector2? latestGaze = null;
        while (gazePacketQueue.TryDequeue(out Vector2 gazePos))
        {
            latestGaze = gazePos;
        }

        if (latestGaze.HasValue)
        {
            VirtualInputState.Instance.SetGazePosition(latestGaze.Value);
        }
    }

    public void StartGazeFollower()
    {
        StartGazeFollower(GazeLaunchMode.Auto);
    }

    public void StopGazeFollower()
    {
        CleanupUdp();

        if (gazeProcess == null)
            return;

        try
        {
            if (!gazeProcess.HasExited)
                gazeProcess.Kill();
        }
        catch (Exception ex)
        {
            RobitLogger.LogWarning($"[GazeFollowerRunner] Failed to stop gaze process: {ex.Message}");
        }
        finally
        {
            CleanupProcessHandlers();
        }
    }

    private string GetResolvedExePath()
    {
        return Path.Combine(Application.streamingAssetsPath, relativeExePath).Replace("/", "\\");
    }

    private IEnumerator BeginStartupFlow()
    {
        if (startupFlowRunning)
            yield break;

        if (gazeProcess != null && !gazeProcess.HasExited)
            yield break;

        startupFlowRunning = true;

        if (!promptForCalibrationChoice)
        {
            StartGazeFollower(GazeLaunchMode.Auto);
            startupFlowRunning = false;
            yield break;
        }

        string exePath = GetResolvedExePath();
        if (!File.Exists(exePath))
        {
            RobitLogger.LogWarning($"[GazeFollowerRunner] Executable not found at {exePath}. Did you run PyInstaller?");
            startupFlowRunning = false;
            yield break;
        }

        var task = QuerySavedCalibrationStatusAsync(exePath, activeProfileId);
        while (!task.IsCompleted)
            yield return null;

        var (hasSavedCalibration, statusKnown) = task.Result;

        if (!hasSavedCalibration)
        {
            RobitLogger.Log("[GazeFollowerRunner] No saved calibration found. Loading calibration scene.");
            SceneManager.LoadScene(CalibrationSceneName);
            // startupFlowRunning intentionally stays true; cleared by NotifyCalibrationComplete()
            yield break;
        }

        yield return StartCoroutine(ShowCalibrationChoicePrompt(hasSavedCalibration, statusKnown));
        startupFlowRunning = false;
    }

    private void StartGazeFollower(GazeLaunchMode launchMode)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (gazeProcess != null && !gazeProcess.HasExited)
        {
            RobitLogger.Log("[GazeFollowerRunner] GazeFollower is already running.");
            return;
        }

        string exePath = GetResolvedExePath();
        if (!File.Exists(exePath))
        {
            RobitLogger.LogWarning($"[GazeFollowerRunner] Executable not found at {exePath}.");
            return;
        }

        try
        {
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int assignedPort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;

            udpCancellation = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveUdpLoop(udpCancellation.Token), udpCancellation.Token);

            string baseArgs = $"--port {assignedPort} --mode {ToModeArgument(launchMode)}";
            if (simulateGazeWithArrowKeys)
            {
                baseArgs += " --keyboard";
            }
            if (!string.IsNullOrEmpty(activeProfileId))
            {
                baseArgs += $" --profile-id \"{activeProfileId}\"";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = baseArgs,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            gazeProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            gazeProcess.OutputDataReceived += OnOutputDataReceived;
            gazeProcess.ErrorDataReceived += OnErrorDataReceived;
            gazeProcess.Exited += OnProcessExited;

            // gazeProcess.Start();
            // gazeProcess.BeginOutputReadLine();
            // gazeProcess.BeginErrorReadLine();
            RobitLogger.Log($"[GazeFollowerRunner] Started gaze bridge executable on dynamic port {assignedPort}.");
        }
        catch (Exception ex)
        {
            RobitLogger.LogError($"[GazeFollowerRunner] Failed to start gaze process: {ex.Message}");
            CleanupUdp();
        }
#else
        RobitLogger.LogWarning("[GazeFollowerRunner] This runner currently supports Windows builds only.");
#endif
    }

    private async Task ReceiveUdpLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await udpClient.ReceiveAsync();
                string payload = Encoding.UTF8.GetString(result.Buffer).Trim();

                string[] parts = payload.Split(',');
                if (parts.Length == 2 &&
                    float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y))
                {
                    gazePacketQueue.Enqueue(new Vector2(x, y));
                }
            }
            catch (ObjectDisposedException)
            {
                break; // Expected when UdpClient is closed
            }
            catch (Exception ex)
            {
                RobitLogger.LogWarning($"[GazeFollowerRunner] UDP Receive Error: {ex.Message}");
            }
        }
    }

    private void CleanupUdp()
    {
        if (udpCancellation != null)
        {
            udpCancellation.Cancel();
            udpCancellation.Dispose();
            udpCancellation = null;
        }

        if (udpClient != null)
        {
            udpClient.Close();
            udpClient.Dispose();
            udpClient = null;
        }
    }

    private Task<(bool hasSaved, bool statusKnown)> QuerySavedCalibrationStatusAsync(string exePath, string profileId)
    {
        return Task.Run(() =>
        {
            bool hasSaved = false;
            bool statusKnown = false;
            try
            {
                string args = "--status-only";
                if (!string.IsNullOrEmpty(profileId))
                {
                    args += $" --profile-id \"{profileId}\"";
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using Process process = Process.Start(startInfo);
                if (process == null)
                    return (false, false);

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(15000);

                if (!string.IsNullOrWhiteSpace(stderr))
                    RobitLogger.LogWarning("[GazeFollowerRunner] Calibration status probe stderr:\n" + stderr.Trim());

                foreach (string rawLine in stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string line = rawLine.Trim();
                    if (!line.StartsWith("HAS_SAVED_CALIBRATION:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string payload = line.Substring("HAS_SAVED_CALIBRATION:".Length).Trim();
                    statusKnown = true;
                    hasSaved = payload == "1" || payload.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
                }
            }
            catch (Exception ex)
            {
                RobitLogger.LogWarning($"[GazeFollowerRunner] Failed to query calibration status: {ex.Message}");
            }

            return (hasSaved, statusKnown);
        });
    }

    private IEnumerator ShowCalibrationChoicePrompt(bool hasSavedCalibration, bool statusKnown)
    {
        UIDocument targetDocument = null;
        float timeoutAt = Time.unscaledTime + 10f;

        while (Time.unscaledTime < timeoutAt && targetDocument == null)
        {
            UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            foreach (UIDocument document in documents)
            {
                if (document != null && document.rootVisualElement != null)
                {
                    targetDocument = document;
                    break;
                }
            }

            if (targetDocument == null)
                yield return null;
        }

        if (targetDocument == null || targetDocument.rootVisualElement == null)
        {
            RobitLogger.LogWarning("[GazeFollowerRunner] No UIDocument was available for the calibration prompt. Falling back to automatic start.");
            StartGazeFollower(hasSavedCalibration ? GazeLaunchMode.UseSaved : GazeLaunchMode.Calibrate);
            if (hasSavedCalibration) SceneManager.LoadScene("OverlayScene");
            yield break;
        }

        bool selectionMade = false;

#if UNITY_EDITOR
        var visualTree = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/GazeCalibrationPrompt.uxml");
#else
        var visualTree = Resources.Load<VisualTreeAsset>("GazeCalibrationPrompt");
#endif

        if (visualTree == null)
        {
            RobitLogger.LogError("[GazeFollowerRunner] Could not load GazeCalibrationPrompt.uxml. Falling back to automatic start.");
            StartGazeFollower(hasSavedCalibration ? GazeLaunchMode.UseSaved : GazeLaunchMode.Calibrate);
            if (hasSavedCalibration) SceneManager.LoadScene("OverlayScene");
            yield break;
        }

        startupPromptRoot = visualTree.Instantiate();
        var subtitle = startupPromptRoot.Q<Label>("subtitle");
        var calibrateBtn = startupPromptRoot.Q<Button>("calibrate-btn");
        var useSavedBtn = startupPromptRoot.Q<Button>("use-saved-btn");

        if (subtitle != null)
        {
            subtitle.text = statusKnown
                ? (hasSavedCalibration
                    ? "A saved gaze calibration was found for this profile. Choose whether to reuse it or calibrate again."
                    : "No saved gaze calibration was found for this profile. Run calibration now to set up tracking.")
                : "Calibration status could not be checked automatically. You can still calibrate now or try the saved calibration path.";
        }

        if (useSavedBtn != null)
        {
            useSavedBtn.SetEnabled(hasSavedCalibration);
            if (!hasSavedCalibration)
            {
                useSavedBtn.text = "No Saved Calibration Found";
                useSavedBtn.style.backgroundColor = new Color(0.27f, 0.30f, 0.36f, 1f);
            }
            else
            {
                useSavedBtn.clicked += () => 
                {
                    selectionMade = true;
                    RemoveStartupPrompt();
                    StartGazeFollower(GazeLaunchMode.UseSaved);
                    SceneManager.LoadScene("OverlayScene");
                };
            }
        }

        if (calibrateBtn != null)
        {
            calibrateBtn.clicked += () => 
            {
                selectionMade = true;
                RemoveStartupPrompt();
                SceneManager.LoadScene(CalibrationSceneName);
            };
        }

        targetDocument.rootVisualElement.Add(startupPromptRoot);

        while (!selectionMade)
            yield return null;
    }

    private void RemoveStartupPrompt()
    {
        if (startupPromptRoot == null)
            return;

        startupPromptRoot.RemoveFromHierarchy();
        startupPromptRoot = null;
    }

    private static string ToModeArgument(GazeLaunchMode mode)
    {
        return mode switch
        {
            GazeLaunchMode.Calibrate => "calibrate",
            GazeLaunchMode.UseSaved => "use-saved",
            _ => "auto",
        };
    }

    private void OnApplicationQuit()
    {
        StopGazeFollower();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        RemoveStartupPrompt();
        StopGazeFollower();
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        string line = e.Data.Trim();

        if (string.Equals(line, "CALIBRATION_DONE", StringComparison.Ordinal))
        {
            RobitLogger.Log("[GazeFollowerRunner] Calibration completed. Gaze is now driving mouse movement.");
            return;
        }

        RobitLogger.Log($"[GazeFollowerRunner][PY] {line}");
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        RobitLogger.LogWarning($"[GazeFollowerRunner][PY-ERR] {e.Data}");
    }

    private void OnProcessExited(object sender, EventArgs e)
    {
        RobitLogger.Log("[GazeFollowerRunner] Gaze process exited.");
    }

    private void CleanupProcessHandlers()
    {
        if (gazeProcess == null)
            return;

        gazeProcess.OutputDataReceived -= OnOutputDataReceived;
        gazeProcess.ErrorDataReceived -= OnErrorDataReceived;
        gazeProcess.Exited -= OnProcessExited;
        gazeProcess.Dispose();
        gazeProcess = null;
    }

    /// <summary>
    /// Called by GazeCalibrationController when Unity-driven calibration completes.
    /// Clears the startup flow lock and starts the gaze bridge in use-saved mode.
    /// </summary>
    public void NotifyCalibrationComplete()
    {
        startupFlowRunning = false;
        StartGazeFollower(GazeLaunchMode.UseSaved);
    }

    /// <summary>
    /// Called by GazeCalibrationController.Start() when the calibration scene is ready.
    /// Launches the camera-check preview panel before starting the actual calibration.
    /// This is invoked regardless of HOW the calibration scene was reached
    /// (first-time, "Calibrate Again", etc.).
    /// </summary>
    public void OnCalibrationSceneReady(GazeCalibrationController controller)
    {
        StartCoroutine(RunCameraCheckInCalibrationScene(controller));
    }

    /// <summary>
    /// Overlays a camera-check panel on the calibration scene's UIDocument.
    /// Streams JPEG frames from the Python bridge (camera-check mode) into a
    /// Texture2D rendered in the panel. Once the user confirms the camera is
    /// working, the panel is removed and StartCalibration() is called on the
    /// controller.
    /// </summary>
    private IEnumerator RunCameraCheckInCalibrationScene(GazeCalibrationController controller)
    {
        string exePath = GetResolvedExePath();

        var calibDoc = controller.GetComponent<UIDocument>();
        if (calibDoc == null || calibDoc.rootVisualElement == null)
        {
            RobitLogger.LogWarning("[GazeFollowerRunner] Camera check skipped — UIDocument missing.");
            controller.StartCalibration(activeProfileId);
            yield break;
        }

        // Load CameraCheckPanel UXML
#if UNITY_EDITOR
        var visualTree = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/CameraCheckPanel.uxml");
#else
        var visualTree = Resources.Load<VisualTreeAsset>("CameraCheckPanel");
#endif
        if (visualTree == null)
        {
            RobitLogger.LogWarning("[GazeFollowerRunner] CameraCheckPanel.uxml not found. Skipping camera check.");
            controller.StartCalibration(activeProfileId);
            yield break;
        }

        VisualElement cameraPanel = visualTree.Instantiate();
        cameraPanel.style.position = Position.Absolute;
        cameraPanel.style.left = 0;
        cameraPanel.style.top = 0;
        cameraPanel.style.right = 0;
        cameraPanel.style.bottom = 0;
        calibDoc.rootVisualElement.Add(cameraPanel);

        var previewElement = cameraPanel.Q<VisualElement>("camera-preview");
        var statusLabel    = cameraPanel.Q<Label>("camera-status");
        var errorLabel     = cameraPanel.Q<Label>("camera-error");
        var retryBtn       = cameraPanel.Q<Button>("camera-retry-btn");
        var continueBtn    = cameraPanel.Q<Button>("camera-continue-btn");

        // Texture updated every frame from incoming JPEG bytes
        Texture2D previewTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);

        bool selectionMade = false;
        bool cameraOk      = false;

        // Mutable camera-check process state (replaced on Retry)
        UdpClient                  camUdp     = null;
        Process                    camProcess = null;
        CancellationTokenSource    camCts     = null;
        ConcurrentQueue<byte[]>    camQueue   = new ConcurrentQueue<byte[]>();

        void LaunchCameraCheckProcess()
        {
            // Tear down any previous instance
            try { camCts?.Cancel(); } catch { }
            try { if (camProcess != null && !camProcess.HasExited) camProcess.Kill(); } catch { }
            camProcess?.Dispose();
            camUdp?.Close();
            camUdp?.Dispose();

            // Drain stale packets
            while (camQueue.TryDequeue(out _)) { }

            cameraOk = false;
            if (statusLabel  != null) statusLabel.text                    = "Starting camera\u2026";
            if (continueBtn  != null) 
            {
                continueBtn.SetEnabled(false);
                continueBtn.style.opacity = 0.5f;
            }
            if (retryBtn     != null) retryBtn.style.display               = DisplayStyle.None;
            if (errorLabel   != null) errorLabel.style.display             = DisplayStyle.None;
            if (previewElement != null)
                previewElement.style.backgroundImage = StyleKeyword.None;

            if (!File.Exists(exePath))
            {
                if (statusLabel != null) statusLabel.text = "Executable Missing";
                if (errorLabel != null)
                {
                    errorLabel.text = $"Bridge not found at:\n{exePath}";
                    errorLabel.style.display = DisplayStyle.Flex;
                }
                if (retryBtn != null) retryBtn.style.display = DisplayStyle.Flex;
                return;
            }

            camUdp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int camPort = ((IPEndPoint)camUdp.Client.LocalEndPoint).Port;
            camCts = new CancellationTokenSource();

            var psi = new ProcessStartInfo
            {
                FileName               = exePath,
                Arguments              = $"--mode camera-check --port {camPort}",
                WorkingDirectory       = Path.GetDirectoryName(exePath),
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };

            try 
            {
                camProcess = Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                if (statusLabel != null) statusLabel.text = "Launch Failed";
                if (errorLabel != null)
                {
                    errorLabel.text = $"Failed to start bridge:\n{ex.Message}";
                    errorLabel.style.display = DisplayStyle.Flex;
                }
                if (retryBtn != null) retryBtn.style.display = DisplayStyle.Flex;
                return;
            }

            // Background UDP receive loop
            var capturedUdp = camUdp;
            var capturedCts = camCts;
            _ = Task.Run(async () =>
            {
                while (!capturedCts.IsCancellationRequested)
                {
                    try
                    {
                        var res = await capturedUdp.ReceiveAsync();
                        camQueue.Enqueue(res.Buffer);
                    }
                    catch { break; }
                }
            }, capturedCts.Token);
        }

        // Wire up buttons before first launch
        if (retryBtn != null)
            retryBtn.clicked += LaunchCameraCheckProcess;

        if (continueBtn != null)
            continueBtn.clicked += () => selectionMade = true;

        // Set initial UI state
        if (continueBtn != null) 
        {
            continueBtn.SetEnabled(false);
            continueBtn.style.opacity = 0.5f;
        }
        if (retryBtn    != null) retryBtn.style.display    = DisplayStyle.None;
        if (errorLabel  != null) errorLabel.style.display  = DisplayStyle.None;

        LaunchCameraCheckProcess();

        // ── Main coroutine loop ───────────────────────────────────────────────
        while (!selectionMade)
        {
            while (camQueue.TryDequeue(out byte[] packet))
            {
                // JPEG always starts with 0xFF 0xD8; text messages start with 'C' (CAM_)
                if (packet.Length >= 2 && packet[0] == 0xFF && packet[1] == 0xD8)
                {
                    // Live JPEG frame — only render after camera confirmed OK
                    if (cameraOk && previewElement != null)
                    {
                        previewTexture.LoadImage(packet);
                        previewTexture.Apply();
                        previewElement.style.backgroundImage =
                            new StyleBackground(Background.FromTexture2D(previewTexture));
                    }
                }
                else
                {
                    string text = Encoding.UTF8.GetString(packet).Trim();

                    if (text == "CAM_OK")
                    {
                        cameraOk = true;
                        if (statusLabel  != null) statusLabel.text            = "Camera ready \u2713";
                        if (continueBtn  != null) 
                        {
                            continueBtn.SetEnabled(true);
                            continueBtn.style.opacity = 1f;
                        }
                        if (retryBtn     != null) retryBtn.style.display      = DisplayStyle.None;
                        if (errorLabel   != null) errorLabel.style.display    = DisplayStyle.None;
                    }
                    else if (text.StartsWith("CAM_ERROR:", StringComparison.Ordinal))
                    {
                        string reason = text.Substring("CAM_ERROR:".Length);
                        cameraOk = false;
                        if (statusLabel  != null) statusLabel.text            = "Camera not detected.";
                        if (errorLabel   != null)
                        {
                            errorLabel.text                  = reason;
                            errorLabel.style.display         = DisplayStyle.Flex;
                        }
                        if (retryBtn     != null) retryBtn.style.display      = DisplayStyle.Flex;
                        if (continueBtn  != null) 
                        {
                            continueBtn.SetEnabled(false);
                            continueBtn.style.opacity = 0.5f;
                        }
                    }
                }
            }

            yield return null;
        }

        // ── User clicked Continue — clean up camera-check resources ───────────
        try { camCts?.Cancel(); } catch { }
        camCts?.Dispose();
        camUdp?.Close();
        camUdp?.Dispose();
        try { if (camProcess != null && !camProcess.HasExited) camProcess.Kill(); } catch { }
        camProcess?.Dispose();

        cameraPanel.RemoveFromHierarchy();
        UnityEngine.Object.Destroy(previewTexture);

        // Hand off to calibration
        controller.StartCalibration(activeProfileId);
    }

    /// <summary>
    /// Re-triggers the startup flow (queries calibration status and begins gaze tracking).
    /// Safe to call after returning from the calibration scene.
    /// </summary>
    public void TriggerStartupFlow(string profileId)
    {
        activeProfileId = profileId;
        StartCoroutine(BeginStartupFlow());
    }
}
