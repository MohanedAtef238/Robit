using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

// This class remains the same, as the python script will still send x, y, z
[Serializable]
public class MoveMessage
{
    public string type;
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class BaseMessage
{
    public string type;
}


[Serializable]
public class GenericMessage
{
    public string type;
    public string msg;
}

[Serializable]
public class PlayerEvent
{
    public string type;
    public string action;
    public float time;
}

public class UnityUdpClient : MonoBehaviour
{
    private const string MoveMessageType = "move";
    private const float TargetReachedThreshold = 0.001f * 0.001f; // Using squared magnitude

    public string serverIp = "127.0.0.1";
    public int serverPort = 5005;
    public GameObject controlledObject; // Assign your 2D Sprite/GameObject

    [Tooltip("Speed at which the controlled object moves toward the target position (units/sec)")]
    public float moveSpeed = 5.0f;

    private UdpClient client;
    private IPEndPoint remoteEndPoint;
    private Thread readerThread;
    private volatile bool running = false;

    // Thread-safe queue for messages from Python
    private readonly ConcurrentQueue<string> recvQueue = new ConcurrentQueue<string>();

    // Movement target state for 2D
    private Vector2 targetPosition = Vector2.zero;
    private bool hasTarget = false;
    public bool debugMessages = true;

    // Reference to the main camera to map coordinates
    private Camera mainCamera;

    void Start()
    {
        if (controlledObject == null) Debug.LogWarning("controlledObject not set — assign a 2D object in the inspector");
        mainCamera = Camera.main;
        Connect();
    }

    void Connect()
    {
        try
        {
            // The port here is the one this script LISTENS on.
            // Python will send to this port.
            client = new UdpClient(serverPort);
            running = true;
            // remoteEndPoint is not needed for receiving, but good for sending
            remoteEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
            readerThread = new Thread(ReadLoop) { IsBackground = true };
            readerThread.Start();
            Debug.Log($"UDP listener started on port {serverPort}. Waiting for messages...");

            // Send a hello message to the Python script's address (if it were listening)
            // Note: Our Python script doesn't listen, it only sends.
            // SendJson(new { type = "hello", msg = "hello from unity" });
        }
        catch (Exception e)
        {
            Debug.LogError("Connect failed: " + e.Message);
        }
    }

    void ReadLoop()
    {
        try
        {
            while (running)
            {
                if (client == null) break;
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref anyIP); // This is a blocking call
                string txt = Encoding.UTF8.GetString(data);

                // The Python script sends one JSON object per packet, ending with '\n'
                // We can process it directly without a string builder for line-by-line buffering.
                string line = txt.Trim();
                if (line.Length > 0)
                {
                    recvQueue.Enqueue(line);
                }
            }
        }
        catch (SocketException e)
        {
            // This exception is expected when the socket is closed in OnApplicationQuit
            if (!running)
            {
                Debug.Log("ReadLoop stopped as expected.");
                return;
            }
            Debug.LogError("ReadLoop SocketException: " + e.Message);
        }
        catch (Exception e)
        {
            Debug.LogError("ReadLoop exception: " + e.Message);
        }
    }

    void Update()
    {
        // Process incoming messages on Unity main thread
        while (recvQueue.TryDequeue(out var line))
        {

            if (debugMessages) Debug.Log("Dequeued message: " + line);

            try
            {
                // First, parse to a base message to determine the type
                BaseMessage baseMsg = JsonUtility.FromJson<BaseMessage>(line);

                switch (baseMsg.type)
                {
                    case MoveMessageType:
                        MoveMessage m = JsonUtility.FromJson<MoveMessage>(line);

                        // --- Convert Normalized Coordinates to Unity World Space ---
                        // The python script sends normalized coordinates (0,0 = top-left).
                        // We convert them to viewport coordinates (0,0 = bottom-left)
                        // and then to world coordinates.
                        Vector2 viewportCoord = new Vector2(m.x, 1 - m.y);
                        float zDepth = (controlledObject != null) ? controlledObject.transform.position.z : mainCamera.nearClipPlane;
                        Vector3 newTarget = mainCamera.ViewportToWorldPoint(new Vector3(viewportCoord.x, viewportCoord.y, zDepth));
                        // ---

                        if (Vector2.SqrMagnitude((Vector2)newTarget - targetPosition) > 1e-6f)
                        {
                            targetPosition = newTarget;
                            hasTarget = true;
                        }
                        break;

                    default:
                        GenericMessage g = JsonUtility.FromJson<GenericMessage>(line);
                        Debug.Log("Received generic message: " + g.type + " / " + g.msg);
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to parse incoming JSON: " + e.Message + "\n" + line);
            }
        }

        // Optional: send input message when user presses space
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            var evt = new PlayerEvent { type = "player_event", action = "jump", time = Time.time };
            SendJson(evt);
        }

        // Smoothly move controlled object toward the latest target
        if (controlledObject != null && hasTarget)
        {
            var cur = (Vector2)controlledObject.transform.position;
            var next = Vector2.MoveTowards(cur, targetPosition, moveSpeed * Time.deltaTime);
            controlledObject.transform.position = new Vector3(next.x, next.y, controlledObject.transform.position.z);

            // Stop moving if close enough to the target
            if (Vector2.SqrMagnitude((Vector2)controlledObject.transform.position - targetPosition) < TargetReachedThreshold)
            {
                hasTarget = false;
            }
        }
    }

    public void SendJson(object obj)
    {
        // This function is for sending data back to Python, which isn't required
        // for this use case but is kept for completeness.
        if (client == null) return;
        string s = JsonUtility.ToJson(obj) + "\n";
        byte[] data = Encoding.UTF8.GetBytes(s);
        try
        {
            // Note: This requires Python to be listening on a known port.
            // client.Send(data, data.Length, remoteEndPoint);
            // If the python script sends from a random port, you'd need to capture `anyIP` in ReadLoop
            // and use that as the remoteEndPoint for sending back.
            // For now, we assume the Python script only sends, so this is commented out.
        }
        catch (Exception e)
        {
            Debug.LogError("Send failed: " + e.Message);
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        // The readerThread is a background thread, so it will be terminated automatically
        // when the application quits. Closing the client will cause the blocking Receive()
        // to throw a SocketException, which is handled in ReadLoop.
        client?.Close();

        // Avoid blocking the main thread on quit.
        // if (readerThread != null && readerThread.IsAlive)
        // {
        //     readerThread.Join(200);
        // }
    }
}
