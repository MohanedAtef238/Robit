using System;
using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(-850)]
public class VirtualPointerDriver : MonoBehaviour
{
    public static VirtualPointerDriver Instance { get; private set; }

    [Header("Cursor Output")]
    [SerializeField] private bool moveWindowsCursor = true;
    [SerializeField] private bool mirrorCursorInsideUnity = true;
    [SerializeField] private float minPixelDelta = 1f;

    [Header("EMG Mouse Mapping")]
    [SerializeField] private bool mapEmgToLeftMouseButton = true;

    [Header("Unity Cursor Look")]
    [SerializeField] private float cursorSize = 28f;
    [SerializeField] private Color cursorRingColor = new(0.18f, 0.95f, 0.88f, 0.95f);
    [SerializeField] private Color cursorFillColor = new(1f, 1f, 1f, 0.10f);
    [SerializeField] private Color cursorDotColor = new(1f, 1f, 1f, 0.95f);

    private Vector2 lastCursorPosition = new(float.MinValue, float.MinValue);
    private bool lastEmgActive;
    private UIDocument attachedDocument;
    private VisualElement cursorRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
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
    }

    private void Update()
    {
        VirtualInputState inputState = VirtualInputState.Instance;

        if (moveWindowsCursor && inputState.HasGazePosition)
            UpdateWindowsCursor(inputState.GazePosition);

        if (mirrorCursorInsideUnity && inputState.HasGazePosition)
            UpdateUnityCursor(inputState.GazePosition);
        else
            HideUnityCursor();

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
        EnsureCursorAttached();
        if (cursorRoot == null)
            return;

        Vector2 panelPosition = ConvertScreenToOverlayPosition(rawPosition);
        float halfSize = cursorSize * 0.5f;
        cursorRoot.style.left = panelPosition.x - halfSize;
        cursorRoot.style.top = panelPosition.y - halfSize;
        cursorRoot.style.display = DisplayStyle.Flex;
    }

    private void HideUnityCursor()
    {
        if (cursorRoot != null)
            cursorRoot.style.display = DisplayStyle.None;
    }

    private void UpdateMouseButton(bool emgActive)
    {
        if (emgActive == lastEmgActive)
            return;

        lastEmgActive = emgActive;
        SendMouseButton(emgActive ? Win32Interop.MouseEventFlags.LeftDown : Win32Interop.MouseEventFlags.LeftUp);
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

    private void EnsureCursorAttached()
    {
        if (attachedDocument != null && attachedDocument.rootVisualElement != null && cursorRoot?.parent == attachedDocument.rootVisualElement)
            return;

        attachedDocument = null;
        cursorRoot = null;

        UIDocument[] documents = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        foreach (UIDocument document in documents)
        {
            if (document == null || document.rootVisualElement == null)
                continue;

            attachedDocument = document;
            cursorRoot = CreateCursorElement();
            attachedDocument.rootVisualElement.Add(cursorRoot);
            break;
        }
    }

    private VisualElement CreateCursorElement()
    {
        var outer = new VisualElement
        {
            pickingMode = PickingMode.Ignore
        };

        outer.style.position = Position.Absolute;
        outer.style.width = cursorSize;
        outer.style.height = cursorSize;
        outer.style.borderTopLeftRadius = cursorSize;
        outer.style.borderTopRightRadius = cursorSize;
        outer.style.borderBottomLeftRadius = cursorSize;
        outer.style.borderBottomRightRadius = cursorSize;
        outer.style.borderTopWidth = 2f;
        outer.style.borderRightWidth = 2f;
        outer.style.borderBottomWidth = 2f;
        outer.style.borderLeftWidth = 2f;
        outer.style.borderTopColor = cursorRingColor;
        outer.style.borderRightColor = cursorRingColor;
        outer.style.borderBottomColor = cursorRingColor;
        outer.style.borderLeftColor = cursorRingColor;
        outer.style.backgroundColor = cursorFillColor;
        outer.style.display = DisplayStyle.None;

        float dotSize = Mathf.Max(6f, cursorSize * 0.22f);
        var dot = new VisualElement
        {
            pickingMode = PickingMode.Ignore
        };

        dot.style.position = Position.Absolute;
        dot.style.width = dotSize;
        dot.style.height = dotSize;
        dot.style.left = (cursorSize - dotSize) * 0.5f;
        dot.style.top = (cursorSize - dotSize) * 0.5f;
        dot.style.borderTopLeftRadius = dotSize;
        dot.style.borderTopRightRadius = dotSize;
        dot.style.borderBottomLeftRadius = dotSize;
        dot.style.borderBottomRightRadius = dotSize;
        dot.style.backgroundColor = cursorDotColor;

        outer.Add(dot);
        return outer;
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

        if (attachedDocument?.rootVisualElement != null)
        {
            float panelWidth = attachedDocument.rootVisualElement.resolvedStyle.width;
            float panelHeight = attachedDocument.rootVisualElement.resolvedStyle.height;

            if (panelWidth > 0f)
                x = Mathf.Clamp(x, 0f, panelWidth);

            if (panelHeight > 0f)
                y = Mathf.Clamp(y, 0f, panelHeight);
        }

        return new Vector2(x, y);
    }
}
