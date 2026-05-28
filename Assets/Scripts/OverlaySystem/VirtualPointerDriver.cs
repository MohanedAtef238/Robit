using System;
using NativeWebSocket;
using UnityEngine;

[DefaultExecutionOrder(-850)]
public class VirtualPointerDriver : MonoBehaviour
{
    public static VirtualPointerDriver Instance { get; private set; }

    [Header("Cursor Output")]
    [SerializeField] private bool moveWindowsCursor = true;
    [SerializeField] private bool mirrorCursorInsideUnity = false;
    [SerializeField] private float minPixelDelta = 1f;

    [Header("Smoothing")]
    [SerializeField] private bool useSmoothing = true;
    [SerializeField] private float smoothingSpeed = 15f;

    [Header("Input Mapping")]
    [SerializeField] private bool mapEmgToLeftMouseButton = true;
    [SerializeField] private bool clickOnReleaseOnly = false;
    [Tooltip("Minimum time in seconds the EMG must be active to count as a click (prevents noise)")]
    [SerializeField] private float minClickHoldTime = 0.05f;
    [Tooltip("Maximum time to be considered a 'click'. Longer stays become a 'drag'.")]
#pragma warning disable 0414
    [SerializeField] private float maxClickDuration = 0.5f;
#pragma warning restore 0414

    [Header("World Cursor Tracking")]
    [SerializeField] private GameObject cursorInstance;
    [SerializeField] private float cursorDistance = 5f;

    [Header("UI Automation Websocket")]
    [SerializeField] private string wsUrl = "ws://127.0.0.1:8181";
    [SerializeField] private float dragToMessageTime = 2f;
    private WebSocket websocket;
    private bool messageSentForCurrentDrag;

    private Vector2 smoothedGazePosition;
    private bool isGazeInitialized;
    private Vector2 lastCursorPosition = new(float.MinValue, float.MinValue);
    private bool lastEmgActive;
    private float emgStartTime;
    private Camera mainCamera;

    // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<VirtualPointerDriver>() != null)
            return;

        var go = new GameObject("VirtualPointerDriver");
        go.AddComponent<VirtualPointerDriver>();
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
        
        ConnectWebSocket();
    }

    private async void ConnectWebSocket()
    {
        websocket = new WebSocket(wsUrl);
        websocket.OnOpen += () => Debug.Log("[VirtualPointerDriver] WebSocket Connected");
        websocket.OnError += e => Debug.LogError("[VirtualPointerDriver] WebSocket Error: " + e);
        websocket.OnClose += _ => Debug.Log("[VirtualPointerDriver] WebSocket Closed");
        
        await websocket.Connect();
    }

    private async void OnDestroy()
    {
        if (websocket != null)
        {
            await websocket.Close();
        }
    }

    private void Update()
    {
        websocket?.DispatchMessageQueue();

        VirtualInputState inputState = VirtualInputState.Instance;

        if (inputState.HasGazePosition)
        {
            Vector2 targetPos = inputState.GazePosition;
            if (useSmoothing)
            {
                if (!isGazeInitialized)
                {
                    smoothedGazePosition = targetPos;
                    isGazeInitialized = true;
                }
                else
                {
                    smoothedGazePosition = Vector2.Lerp(smoothedGazePosition, targetPos, Time.deltaTime * smoothingSpeed);
                }
            }
            else
            {
                smoothedGazePosition = targetPos;
            }

            if (moveWindowsCursor)
                UpdateWindowsCursor(smoothedGazePosition);

            if (mirrorCursorInsideUnity)
                UpdateUnityCursor(smoothedGazePosition);
            else
                HideUnityCursor();
        }
        else
        {
            isGazeInitialized = false;
            HideUnityCursor();
        }

        if (mapEmgToLeftMouseButton)
            UpdateMouseButton(inputState.IsEmgActive);
    }

    private void UpdateWindowsCursor(Vector2 rawPosition)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (Mathf.Abs(rawPosition.x - lastCursorPosition.x) < minPixelDelta &&
            Mathf.Abs(rawPosition.y - lastCursorPosition.y) < minPixelDelta)
            return;

        int x = Mathf.RoundToInt(Mathf.Max(0f, rawPosition.x));
        int y = Mathf.RoundToInt(Mathf.Max(0f, rawPosition.y));
        Win32Interop.SetCursorPos(x, y);
        lastCursorPosition = rawPosition;
#endif
    }

    private void UpdateUnityCursor(Vector2 rawPosition)
    {
        if (cursorInstance == null)
            return;

        // Resolve camera lazily to handle scene changes or re-instantiated cameras
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        Vector2 overlayPos = ConvertScreenToOverlayPosition(rawPosition);
        
        // Convert top-left (Windows Client) to bottom-left (Unity Screen)
        Vector3 screenPos = new Vector3(overlayPos.x, Screen.height - overlayPos.y, cursorDistance);
        
        cursorInstance.transform.position = mainCamera.ScreenToWorldPoint(screenPos);
        cursorInstance.SetActive(true);
    }

    private void HideUnityCursor()
    {
        if (cursorInstance != null)
            cursorInstance.SetActive(false);
    }

    private void UpdateMouseButton(bool emgActive)
    {
        if (emgActive != lastEmgActive)
        {
            if (emgActive)
            {
                // Signal Started
                emgStartTime = Time.time;
                messageSentForCurrentDrag = false;
                
                if (!clickOnReleaseOnly)
                {
                    SendMouseButton(Win32Interop.MouseEventFlags.LeftDown);
                }
            }
            else
            {
                // Signal Ended
                float duration = Time.time - emgStartTime;

                if (clickOnReleaseOnly)
                {
                    // Only fire if it was held long enough to not be noise, 
                    // but short enough to be a intentional "tap"
                    if (duration >= minClickHoldTime && !messageSentForCurrentDrag)
                    {
                        SendMouseButton(Win32Interop.MouseEventFlags.LeftDown);
                        SendMouseButton(Win32Interop.MouseEventFlags.LeftUp);
                    }
                }
                else
                {
                    if (!messageSentForCurrentDrag)
                    {
                        SendMouseButton(Win32Interop.MouseEventFlags.LeftUp);
                    }
                }
            }

            lastEmgActive = emgActive;
        }
        else if (emgActive)
        {
            if (!messageSentForCurrentDrag && Time.time - emgStartTime >= dragToMessageTime)
            {
                messageSentForCurrentDrag = true;
                SendGetClosest();
                if (!clickOnReleaseOnly)
                {
                    // Abort the drag in Windows since it triggered the UI overlay
                    SendMouseButton(Win32Interop.MouseEventFlags.LeftUp);
                }
            }
        }
    }

    private async void SendGetClosest()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.SendText("{\"type\":\"getClosest\"}");
            Debug.Log("[VirtualPointerDriver] Requested closest elements after 2s drag");
        }
    }

    private static void SendMouseButton(Win32Interop.MouseEventFlags flags)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        Win32Interop.INPUT[] inputs =
        {
            new Win32Interop.INPUT
            {
                type = Win32Interop.InputType.Mouse,
                union = new Win32Interop.InputUnion
                {
                    mi = new Win32Interop.MOUSEINPUT
                    {
                        dwFlags = (uint)flags
                    }
                }
            }
        };

        Win32Interop.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32Interop.INPUT)));
#endif
    }

    private Vector2 ConvertScreenToOverlayPosition(Vector2 rawScreenPosition)
    {
        float x = rawScreenPosition.x;
        float y = rawScreenPosition.y;

#if !UNITY_EDITOR
        IntPtr hWnd = WindowManager.GetWindowHandle();
        if (hWnd != IntPtr.Zero)
        {
            Win32Interop.POINT clientPoint = new Win32Interop.POINT
            {
                X = Mathf.RoundToInt(rawScreenPosition.x),
                Y = Mathf.RoundToInt(rawScreenPosition.y)
            };

            if (Win32Interop.ScreenToClient(hWnd, ref clientPoint))
            {
                x = clientPoint.X;
                y = clientPoint.Y;
            }
        }
#endif

        x = Mathf.Clamp(x, 0f, Screen.width);
        y = Mathf.Clamp(y, 0f, Screen.height);

        return new Vector2(x, y);
    }
}