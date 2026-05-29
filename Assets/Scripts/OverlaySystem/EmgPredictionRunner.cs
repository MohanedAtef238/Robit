using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-890)]
public class EmgPredictionRunner : MonoBehaviour
{
    public static EmgPredictionRunner Instance { get; private set; }

    [Header("Multimodal Setup")]
    [SerializeField] private string relativeExePath = @"Multimodal_UDP/unity_emg_bridge.exe";
    [SerializeField] private string comPort = "COM4";
    [SerializeField] private bool autoStartOnAwake = true;

    public string CurrentSensorStatus { get; private set; } = "UNKNOWN";

    [Header("Debug")]
    [SerializeField] private bool simulateEmgWithSpaceKey = true;

    private Process emgProcess;
    private UdpClient udpClient;
    private CancellationTokenSource udpCancellation;
    
    private bool pythonEmgActive;
    private bool simulatedEmgActive;

    private ConcurrentQueue<bool> emgPacketQueue = new ConcurrentQueue<bool>();

    // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<EmgPredictionRunner>() != null)
            return;

        var go = new GameObject("EmgPredictionRunner");
        go.AddComponent<EmgPredictionRunner>();
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
        //     StartEmgPrediction();
    }

    private void Update()
    {
        // Process UDP packets
        while (emgPacketQueue.TryDequeue(out bool pythonState))
        {
            pythonEmgActive = pythonState;
            ApplyEffectiveEmgState();
        }

        if (!simulateEmgWithSpaceKey || Keyboard.current == null)
            return;

        bool isPressed = Keyboard.current.spaceKey.isPressed;
        if (simulatedEmgActive == isPressed)
            return;

        simulatedEmgActive = isPressed;
        ApplyEffectiveEmgState();
    }

    public void StartEmgPrediction()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (emgProcess != null && !emgProcess.HasExited)
        {
            RobitLogger.Log("[EmgPredictionRunner] EMG prediction is already running.");
            return;
        }

        // Setup UDP Socket
        try
        {
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int assignedPort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
            
            udpCancellation = new CancellationTokenSource();
            _ = Task.Run(() => ReceiveUdpLoop(udpCancellation.Token), udpCancellation.Token);

            string exePath = Path.Combine(Application.streamingAssetsPath, relativeExePath).Replace("/", "\\");

            if (!File.Exists(exePath))
            {
                RobitLogger.LogWarning($"[EmgPredictionRunner] Executable not found at {exePath}. Did you run PyInstaller?");
                return;
            }

            string baseArgs = $"--port {assignedPort} --com-port {comPort}";

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

            emgProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            emgProcess.OutputDataReceived += OnOutputDataReceived;
            emgProcess.ErrorDataReceived += OnErrorDataReceived;
            emgProcess.Exited += OnProcessExited;

            // emgProcess.Start();
            // emgProcess.BeginOutputReadLine();
            // emgProcess.BeginErrorReadLine();
            RobitLogger.Log($"[EmgPredictionRunner] Started EMG bridge executable on dynamic port {assignedPort}.");
        }
        catch (Exception ex)
        {
            RobitLogger.LogError($"[EmgPredictionRunner] Failed to start EMG bridge: {ex.Message}");
            CleanupUdp();
        }
#else
        RobitLogger.LogWarning("[EmgPredictionRunner] This runner currently supports Windows builds only.");
#endif
    }

    public void StopEmgPrediction()
    {
        CleanupUdp();

        if (emgProcess == null)
            return;

        try
        {
            if (!emgProcess.HasExited)
                emgProcess.Kill();
        }
        catch (Exception ex)
        {
            RobitLogger.LogWarning($"[EmgPredictionRunner] Failed to stop EMG process: {ex.Message}");
        }
        finally
        {
            CleanupProcessHandlers();
        }
    }

    private async Task ReceiveUdpLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await udpClient.ReceiveAsync();
                string payload = Encoding.UTF8.GetString(result.Buffer).Trim();

                if (payload.StartsWith("EMG_STATUS:", StringComparison.OrdinalIgnoreCase))
                {
                    string status = payload.Substring("EMG_STATUS:".Length).Trim();
                    CurrentSensorStatus = status;
                    if (status.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                    {
                        RobitLogger.LogError($"[EmgPredictionRunner] Sensor Connection Failed: {status}");
                    }
                    else if (status.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
                    {
                        RobitLogger.Log("[EmgPredictionRunner] Sensor Connection Successful (EMG_OK).");
                    }
                }
                else if (payload == "1" || payload.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    emgPacketQueue.Enqueue(true);
                }
                else if (payload == "0" || payload.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    emgPacketQueue.Enqueue(false);
                }
            }
            catch (ObjectDisposedException)
            {
                break; // Expected when UdpClient is closed
            }
            catch (Exception ex)
            {
                RobitLogger.LogWarning($"[EmgPredictionRunner] UDP Receive Error: {ex.Message}");
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

    private void OnApplicationQuit()
    {
        StopEmgPrediction();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        StopEmgPrediction();
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        RobitLogger.Log($"[EmgPredictionRunner][PY] {e.Data}");
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        RobitLogger.LogWarning($"[EmgPredictionRunner][PY-ERR] {e.Data}");
    }

    private void OnProcessExited(object sender, EventArgs e)
    {
        emgPacketQueue.Enqueue(false); // Reset on exit
        RobitLogger.Log("[EmgPredictionRunner] EMG process exited.");
    }

    private void ApplyEffectiveEmgState()
    {
        bool isActive = pythonEmgActive || simulatedEmgActive;
        VirtualInputState.Instance.SetEmgPrediction(isActive, isActive ? 1f : 0f);
    }

    private void CleanupProcessHandlers()
    {
        if (emgProcess == null)
            return;

        emgProcess.OutputDataReceived -= OnOutputDataReceived;
        emgProcess.ErrorDataReceived -= OnErrorDataReceived;
        emgProcess.Exited -= OnProcessExited;
        emgProcess.Dispose();
        emgProcess = null;
    }
}
