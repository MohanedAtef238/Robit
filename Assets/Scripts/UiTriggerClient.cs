using System;
using System.Collections;
using NativeWebSocket;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using System.Runtime.InteropServices;

[Serializable]
public class UiElementData
{
    public float x;
    public float y;
    public float width;
    public float height;
}

[Serializable]
public class ServerMessage
{
    public string type;
    public UiElementData[] elements;
}

[RequireComponent(typeof(UIDocument))]
public class UiTriggerClient : MonoBehaviour
{
    private const string OverlaySceneName = "OverlayScene";

    [Header("Configuration")]
    [SerializeField] private string serverUrl = "ws://127.0.0.1:8181";
    [SerializeField] private bool autoConnect = true;

    [Header("Selection Overlay")]
    [SerializeField] private int maxVisibleOptions = 6;

    private WebSocket websocket;
    private UIDocument uiDocument;
    private VisualElement selectorRoot;
    private Label selectorSubtitle;
    private readonly VisualElement[] optionContainers = new VisualElement[6];
    private readonly VisualElement[] optionTrackers = new VisualElement[6];
    private readonly Label[] optionTitles = new Label[6];

    private bool uiBound;
    private int availableOptionCount;
    private bool wasHPressed;
    private readonly bool[] wasDigitPressed = new bool[6];
    private bool wasEscapePressed;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != OverlaySceneName)
        {
            enabled = false;
            return;
        }

        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("[UiTriggerClient] UIDocument is required on the OverlayScene object.");
            enabled = false;
            return;
        }
    }

    private IEnumerator Start()
    {
        if (!enabled)
            yield break;

        yield return StartCoroutine(BindWhenReady());

        if (!autoConnect)
            yield break;

        websocket = new WebSocket(serverUrl);

        websocket.OnOpen += () => Debug.Log("Connected to .NET app");
        websocket.OnError += e => Debug.Log("Error: " + e);
        websocket.OnClose += _ => Debug.Log("Connection closed");
        websocket.OnMessage += HandleWebSocketMessage;

        var connectTask = websocket.Connect();
        while (!connectTask.IsCompleted)
            yield return null;
    }

    private IEnumerator BindWhenReady()
    {
        yield return null;

        var root = uiDocument.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[UiTriggerClient] UIDocument root is null.");
            yield break;
        }

        selectorRoot = root.Q<VisualElement>("ui-trigger-selector");
        selectorSubtitle = root.Q<Label>("ui-trigger-subtitle");

        for (int i = 0; i < maxVisibleOptions; i++)
        {
            int displayIndex = i + 1;
            optionContainers[i] = root.Q<VisualElement>($"option-{displayIndex}");
            optionTrackers[i] = root.Q<VisualElement>($"option-{displayIndex}-tracker");
            optionTitles[i] = root.Q<Label>($"option-{displayIndex}-title");
        }

        if (selectorRoot == null || selectorSubtitle == null)
        {
            Debug.LogError("[UiTriggerClient] Selector UXML is missing required root elements.");
            yield break;
        }

        for (int i = 0; i < maxVisibleOptions; i++)
        {
            if (optionContainers[i] == null || optionTrackers[i] == null || optionTitles[i] == null)
            {
                Debug.LogError($"[UiTriggerClient] Selector option {i + 1} is missing required elements.");
                yield break;
            }

            optionTrackers[i].userData = i;
            optionTrackers[i].RegisterCallback<PointerDownEvent>(OnOptionPressed);
        }

        uiBound = true;
        HideSelectionOverlay();
    }

    private void Update()
    {
        websocket?.DispatchMessageQueue();

        if (!uiBound)
            return;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        bool isHPressed = (GetAsyncKeyState(0x48) & 0x8000) != 0;
        if (isHPressed && !wasHPressed)
            SendGetClosest();
        wasHPressed = isHPressed;

        for (int i = 0; i < maxVisibleOptions; i++)
        {
            bool isDigitPressed = (GetAsyncKeyState(0x31 + i) & 0x8000) != 0;
            if (isDigitPressed && !wasDigitPressed[i])
                TryInvokeIndex(i);
            wasDigitPressed[i] = isDigitPressed;
        }

        bool isEscapePressed = (GetAsyncKeyState(0x1B) & 0x8000) != 0;
        if (isEscapePressed && !wasEscapePressed)
            HideSelectionOverlay();
        wasEscapePressed = isEscapePressed;
#else
        if (Keyboard.current != null)
        {
            if (Keyboard.current.hKey.wasPressedThisFrame) SendGetClosest();
            if (Keyboard.current.digit1Key.wasPressedThisFrame) TryInvokeIndex(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) TryInvokeIndex(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) TryInvokeIndex(2);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) TryInvokeIndex(3);
            if (Keyboard.current.digit5Key.wasPressedThisFrame) TryInvokeIndex(4);
            if (Keyboard.current.digit6Key.wasPressedThisFrame) TryInvokeIndex(5);
            if (Keyboard.current.escapeKey.wasPressedThisFrame) HideSelectionOverlay();
        }
#endif
    }

    private void HandleWebSocketMessage(byte[] bytes)
    {
        string message = System.Text.Encoding.UTF8.GetString(bytes).Trim();

        if (string.Equals(message, "hold done", StringComparison.OrdinalIgnoreCase))
        {
            ShowSelectionOverlay(maxVisibleOptions, "Choose an index to invoke");
            return;
        }

        try
        {
            ServerMessage msg = JsonUtility.FromJson<ServerMessage>(message);
            if (msg != null && msg.elements != null)
                ShowSelectionOverlay(Mathf.Min(maxVisibleOptions, msg.elements.Length), "Choose one of the nearby UI elements");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[UiTriggerClient] Failed to parse WebSocket message: " + ex.Message);
        }
    }

    private async void SendGetClosest()
    {
        if (websocket == null || websocket.State != WebSocketState.Open)
            return;

        HideSelectionOverlay();
        await websocket.SendText("{\"type\":\"getClosest\"}");
        Debug.Log("Requested closest elements");
    }

    private void OnOptionPressed(PointerDownEvent evt)
    {
        if (evt.currentTarget is not VisualElement target || target.userData is not int index)
            return;

        TryInvokeIndex(index);
    }

    private void TryInvokeIndex(int index)
    {
        if (!uiBound || selectorRoot == null)
            return;

        if (selectorRoot.style.display == DisplayStyle.None)
            return;

        if (index < 0 || index >= availableOptionCount || index >= maxVisibleOptions)
            return;

        InvokeIndex(index);
    }

    private async void InvokeIndex(int index)
    {
        if (websocket == null || websocket.State != WebSocketState.Open)
            return;

        HideSelectionOverlay();

        string msg = $"{{\"type\":\"invokeIndex\",\"index\":{index}}}";
        await websocket.SendText(msg);
        Debug.Log("Invoked index: " + index);
    }

    private void ShowSelectionOverlay(int optionCount, string subtitle)
    {
        if (!uiBound)
            return;

        availableOptionCount = Mathf.Clamp(optionCount, 0, maxVisibleOptions);
        selectorSubtitle.text = subtitle;
        UpdateOptionVisuals();
        selectorRoot.style.display = availableOptionCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void HideSelectionOverlay()
    {
        availableOptionCount = 0;

        if (!uiBound || selectorRoot == null)
            return;

        selectorRoot.style.display = DisplayStyle.None;
        UpdateOptionVisuals();
    }

    private void UpdateOptionVisuals()
    {
        for (int i = 0; i < maxVisibleOptions; i++)
        {
            bool enabled = i < availableOptionCount;
            optionTitles[i].text = (i + 1).ToString();

            optionContainers[i].EnableInClassList("selector-option--disabled", !enabled);
            optionTrackers[i].pickingMode = enabled ? PickingMode.Position : PickingMode.Ignore;
        }
    }

    private async void OnDestroy()
    {
        if (websocket != null)
            await websocket.Close();
    }
}
