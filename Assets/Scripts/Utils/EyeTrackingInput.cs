using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.InteropServices;

// Receives gaze data from Python and provides smoothed input to Unity
public class EyeTrackingInput : MonoBehaviour
{
    public static EyeTrackingInput Instance { get; private set; }
    
    [Header("Network")]
    [SerializeField] private int receivePort = 5005;
    
    [Header("Smoothing & filtering")]
    [SerializeField] private float smoothingFactor = 0.2f;
    [SerializeField] private float minMoveThreshold = 0.005f; // Normalized
    
    [Header("Mouse Simulation")]
    [SerializeField] private bool simulateMouse = true;
    
    public Vector2 GazePosition { get; private set; } // Unity screen space
    public Vector2 NormalizedGaze { get; private set; } // 0-1 range
    public bool IsConnected { get; private set; }
    public bool IsBlinking { get; set; }
    public float FixationStrength { get; private set; }

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning;
    private Vector2 smoothedNormGaze = new Vector2(0.5f, 0.5f);
    private float lastPacketTime;
    private object threadLock = new object();

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        isRunning = true;
        InitializeUDP();
    }

    private void InitializeUDP()
    {
        try
        {
            udpClient = new UdpClient(receivePort);
            udpClient.Client.ReceiveTimeout = 1000;
            receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            receiveThread.Start();
            Debug.Log($"[EyeInput] UDP Receiver successfully bound to port {receivePort}. Waiting for data...");
        }
        catch (SocketException se) 
        { 
            Debug.LogError($"[EyeInput] UDP SocketException detected! Is port {receivePort} already in use? Details: {se.Message}"); 
        }
        catch (Exception e) 
        { 
            Debug.LogError($"[EyeInput] UDP Generic Error: {e.Message}"); 
        }
    }

    private System.Collections.Generic.Queue<string> messageQueue = new System.Collections.Generic.Queue<string>();
    private object queueLock = new object();

    private void Update()
    {
        // Process all queued messages
        while (true)
        {
            string json = null;
            lock (queueLock)
            {
                if (messageQueue.Count > 0)
                    json = messageQueue.Dequeue();
            }

            if (json == null) break;
            ProcessMessage(json);
        }

        // Connection timeout check
        IsConnected = (Time.time - lastPacketTime) < 2.0f;
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEP);
                string json = System.Text.Encoding.UTF8.GetString(data);
                
                // Enqueue for main thread processing
                lock (queueLock)
                {
                    messageQueue.Enqueue(json);
                }
            }
            catch (SocketException) { continue; }
            catch (Exception e) 
            { 
               if (isRunning) Debug.LogWarning($"[EyeInput] Receive error in background thread: {e.Message}"); 
            }
        }
    }

    private void ProcessMessage(string json)
    {
        try
        {
            var msg = JsonUtility.FromJson<BridgeMessage>(json);
            lastPacketTime = Time.time;

            if (msg.type == "gaze")
            {
                Vector2 targetNorm = new Vector2(msg.norm_x, msg.norm_y);
                
                // Jump threshold check to filter micro-jitter
                if (Vector2.Distance(smoothedNormGaze, targetNorm) > minMoveThreshold)
                {
                    smoothedNormGaze = Vector2.Lerp(smoothedNormGaze, targetNorm, smoothingFactor);
                }

                NormalizedGaze = smoothedNormGaze;
                // Convert to Unity Bottom-Left screen space
                GazePosition = new Vector2(
                    smoothedNormGaze.x * Screen.width, 
                    (1.0f - smoothedNormGaze.y) * Screen.height
                );
                IsBlinking = msg.blink;
                FixationStrength = msg.fixation;

                if (simulateMouse)
                {
                    // Windows uses Top-Left coordinates
                    SetCursorPos((int)(msg.gaze_x), (int)(msg.gaze_y));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[EyeInput] Error processing message: {ex.Message}");
        }
    }

    private void OnApplicationQuit()
    {
        isRunning = false;
        udpClient?.Close();
        if (receiveThread != null && receiveThread.IsAlive) 
        {
            // improved thread cleanup
            receiveThread.Join(500);
        }
    }

    [Serializable]
    private class BridgeMessage
    {
        public string type;
        public float gaze_x;
        public float gaze_y;
        public float norm_x;
        public float norm_y;
        public bool blink;
        public float fixation;
    }
}
