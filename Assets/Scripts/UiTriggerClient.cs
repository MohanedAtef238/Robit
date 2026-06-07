using System;
using System.Collections;
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

    [Header("Selection Overlay")]
    [SerializeField] private int maxVisibleOptions = 6;

    private UIDocument uiDocument;
    private VisualElement selectorRoot;
    private Label selectorSubtitle;
    private readonly VisualElement[] optionContainers = new VisualElement[6];
    private readonly VisualElement[] optionTrackers = new VisualElement[6];
    private readonly Label[] optionTitles = new Label[6];

    private bool uiBound;
    private bool eventsBound;
    private int availableOptionCount;
    private bool wasHPressed;
    private readonly bool[] wasDigitPressed = new bool[6];
    private bool wasEscapePressed;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("[UiTriggerClient] UIDocument is required on the OverlayScene object.");
            enabled = false;
            return;
        }

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        RefreshEventBindings();
    }

    private IEnumerator Start()
    {
        yield return StartCoroutine(BindWhenReady());
        RefreshEventBindings();
    }

    private IEnumerator BindWhenReady()
    {
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

    private void HandleAutomationMessage(string message)
    {
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
            Debug.LogWarning("[UiTriggerClient] Failed to parse automation message: " + ex.Message);
        }
    }

    private void OnFaceGestureDetected(string gestureName)
    {
        if (!string.Equals(gestureName, "raise_eyebrow", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(gestureName, "eyebrow_up", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(gestureName, "eyebrows raised", StringComparison.OrdinalIgnoreCase))
            return;

        SendGetClosest();
    }

    private void SendGetClosest()
    {
        if (UiAutomationRunner.Instance == null)
            return;

        HideSelectionOverlay();
        UiAutomationRunner.Instance.SendCmd("{\"type\":\"getClosest\"}");
        Debug.Log("[UiTriggerClient] Requested closest elements");
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

    private void InvokeIndex(int index)
    {
        if (UiAutomationRunner.Instance == null)
            return;

        HideSelectionOverlay();

        string msg = $"{{\"type\":\"invokeIndex\",\"index\":{index}}}";
        UiAutomationRunner.Instance.SendCmd(msg);
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

    private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        RefreshEventBindings();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshEventBindings();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        RefreshEventBindings();
    }

    private void RefreshEventBindings()
    {
        bool shouldBind = SceneManager.GetActiveScene().name == OverlaySceneName;
        if (shouldBind == eventsBound)
            return;

        if (shouldBind)
            BindAutomationEvents();
        else
            UnbindAutomationEvents();
    }

    private void BindAutomationEvents()
    {
        if (eventsBound)
            return;

        if (UiAutomationRunner.Instance != null)
        {
            UiAutomationRunner.Instance.OnMessageReceived += HandleAutomationMessage;
            Debug.Log("[UiTriggerClient] UI Automation Runner events enabled");
        }

        var faceGestureRunner = FindFirstObjectByType<FaceGestureRunner>();
        if (faceGestureRunner != null)
        {
            faceGestureRunner.OnGestureDetected += OnFaceGestureDetected;
        }
        else
        {
            Debug.LogWarning("[UiTriggerClient] FaceGestureRunner not found yet; eyebrow-triggered automation will not work until it is spawned.");
        }

        eventsBound = true;
    }

    private void UnbindAutomationEvents()
    {
        if (!eventsBound)
            return;

        if (UiAutomationRunner.Instance != null)
        {
            UiAutomationRunner.Instance.OnMessageReceived -= HandleAutomationMessage;
        }

        var faceGestureRunner = FindFirstObjectByType<FaceGestureRunner>();
        if (faceGestureRunner != null)
        {
            faceGestureRunner.OnGestureDetected -= OnFaceGestureDetected;
        }

        eventsBound = false;
    }

    private void OnDestroy()
    {
        UnbindAutomationEvents();
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }
}
