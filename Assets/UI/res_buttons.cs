using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Attach to any GameObject with a UIDocument.
/// Listens for clicks on Buttons tagged with the USS class "resolution-btn".
/// Changes the windowed resolution to match the button's name (100, 125, 150, 200).
/// Completely self-contained — no dependencies on other scripts.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class ResolutionButtonController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The UIDocument that contains the resolution Buttons. Defaults to the one on this GameObject.")]
    [SerializeField] private UIDocument _document;

    [Header("Settings")]
    [Tooltip("USS class shared by all four resolution Buttons.")]
    [SerializeField] private string _buttonClass = "resolution-btn";

    [Tooltip("Base resolution that 100% maps to. Defaults to 1920x1080.")]
    [SerializeField] private Vector2Int _baseResolution = new Vector2Int(1920, 1080);

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (_document == null)
            _document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        StartCoroutine(RegisterButtons());
    }

    private void OnDisable()
    {
        UnregisterButtons();
    }

    // ── Registration ──────────────────────────────────────────────────────────

    private System.Collections.IEnumerator RegisterButtons()
    {
        yield return null; // wait for UIDocument to build its visual tree

        var root = _document.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[ResolutionButtonController] rootVisualElement is null.");
            yield break;
        }

        var buttons = root.Query<Button>(className: _buttonClass).ToList();

        if (buttons.Count == 0)
        {
            Debug.LogWarning($"[ResolutionButtonController] No Buttons found with class '{_buttonClass}'.");
            yield break;
        }

        Debug.Log($"[ResolutionButtonController] Found {buttons.Count} button(s) with class '{_buttonClass}'.");

        foreach (var btn in buttons)
        {
            Debug.Log($"[ResolutionButtonController] Registering button: name='{btn.name}' text='{btn.text}'");
            btn.clicked += () => OnResolutionButtonClicked(btn);
        }
    }

    private void UnregisterButtons()
    {
        var root = _document?.rootVisualElement;
        if (root == null) return;

        // Easiest safe cleanup — re-query and unregister all clicked events
        var buttons = root.Query<Button>(className: _buttonClass).ToList();
        foreach (var btn in buttons)
            btn.clicked -= () => OnResolutionButtonClicked(btn);
    }

    // ── Click handler ─────────────────────────────────────────────────────────

    private void OnResolutionButtonClicked(Button btn)
    {
        Debug.Log($"[ResolutionButtonController] Button clicked: name='{btn.name}'");

        if (!int.TryParse(btn.name, out int percent))
        {
            Debug.LogWarning($"[ResolutionButtonController] Could not parse resolution from button name '{btn.name}'.");
            return;
        }

        ApplyResolution(percent);
    }

    // ── Resolution logic ──────────────────────────────────────────────────────

    private void ApplyResolution(int percent)
    {
        int width = Mathf.RoundToInt(_baseResolution.x * percent / 100f);
        int height = Mathf.RoundToInt(_baseResolution.y * percent / 100f);

        #if !UNITY_EDITOR
        IntPtr hWnd = WindowManager.GetWindowHandle();
        if (hWnd != IntPtr.Zero)
        {
            // SWP_NOZORDER | SWP_NOMOVE keeps the window exactly where it is in depth/position, only resizing
            Win32Interop.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, width, height, 
                Win32Interop.SWP_NOZORDER | Win32Interop.SWP_NOMOVE | Win32Interop.SWP_SHOWWINDOW);
        }
        #else
        Screen.SetResolution(width, height, FullScreenMode.Windowed);
        #endif

        Debug.Log($"[ResolutionButtonController] Resolution set to {width}x{height} ({percent}%)");
    }
}