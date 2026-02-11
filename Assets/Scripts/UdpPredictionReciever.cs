
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class UdpPredictionReceiver : MonoBehaviour
{
    public string wsUrl = "ws://127.0.0.1:8765";
    public string expectedMessage = "prediction:true";

    private ClientWebSocket client;
    private CancellationTokenSource cts;
    private volatile bool predictionTrueReceived;

    void Start()
    {
        cts = new CancellationTokenSource();
        _ = ReceiveLoopAsync();
        Debug.Log($"Connecting WebSocket {wsUrl}...");
    }

    private async Task ReceiveLoopAsync()
    {
        client = new ClientWebSocket();
        try
        {
            await client.ConnectAsync(new System.Uri(wsUrl), cts.Token);
            byte[] buffer = new byte[1024];

            while (!cts.IsCancellationRequested && client.State == WebSocketState.Open)
            {
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                if (msg.Trim().Equals(expectedMessage, System.StringComparison.OrdinalIgnoreCase))
                {
                    predictionTrueReceived = true;
                }
            }
        }
        catch (System.Exception ex)
        {
            if (!cts.IsCancellationRequested)
            {
                Debug.LogWarning($"WebSocket error: {ex.Message}");
            }
        }
    }

    void Update()
    {
        if (predictionTrueReceived)
        {
            predictionTrueReceived = false;
            OnPredictionTrue();
        }
    }

    private void OnPredictionTrue()
    {
        // Simulate a global left mouse click via the new Input System.
        if (Mouse.current == null)
        {
            Debug.LogWarning("No Mouse device found for simulated click.");
            return;
        }

        var press = new MouseState { buttons = 1 };
        InputSystem.QueueStateEvent(Mouse.current, press);
        InputSystem.Update();

        var release = new MouseState { buttons = 0 };
        InputSystem.QueueStateEvent(Mouse.current, release);
        InputSystem.Update();

        Debug.Log("Prediction TRUE received (mouse click simulated).");
    }

    void OnDestroy()
    {
        if (cts != null && !cts.IsCancellationRequested)
        {
            cts.Cancel();
        }
        client?.Dispose();
    }
}
