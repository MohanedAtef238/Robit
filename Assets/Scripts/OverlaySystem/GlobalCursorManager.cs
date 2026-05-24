using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages the global Windows cursor for the entire OS session.
///
/// Strategy:
///   - OCR_NORMAL (arrow)  → firefly_closed  — idle state, non-interactive areas
///   - OCR_HAND   (hand)   → firefly_open    — automatically shown by Windows/browsers
///                                              when hovering over links, buttons, etc.
///
/// By overriding both at startup, we get automatic hover behaviour everywhere:
/// Chrome links, Desktop icons, Taskbar, Unity UI elements — no per-element
/// code is needed. Windows routes cursor selection naturally.
/// </summary>
public class GlobalCursorManager : MonoBehaviour
{
    public static GlobalCursorManager Instance;

    [Header("Cursor Files (must be inside StreamingAssets)")]
    public string defaultCursorFileName = "firefly_closed.cur";
    public string hoverCursorFileName   = "firefly_open.cur";

    // ── Additional system cursor IDs to override so apps like VS Code,
    //    File Explorer, etc. also show the firefly when they use a pointer cursor.
    //    Windows uses different IDs depending on context:
    //    OCR_NORMAL = 32512  (standard arrow)
    //    OCR_HAND   = 32649  (pointer / link hover)
    //    The two above cover ~95% of all hover situations.

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (UnityEngine.Object.FindFirstObjectByType<GlobalCursorManager>() != null)
            return;

        var go = new UnityEngine.GameObject("GlobalCursorManager");
        go.AddComponent<GlobalCursorManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        ApplyFireflyCursors();
    }

    /// <summary>
    /// Applies both firefly cursor states to the OS.
    /// Call once at startup. Windows will then swap between them automatically
    /// based on what is under the cursor across ALL applications.
    /// </summary>
    public void ApplyFireflyCursors()
    {
        string defaultPath = Path.Combine(Application.streamingAssetsPath, "Cursor_icons", defaultCursorFileName);
        string hoverPath   = Path.Combine(Application.streamingAssetsPath, "Cursor_icons", hoverCursorFileName);

        // Cache the handles so hover switches have zero file I/O overhead
        _defaultHandle = Win32Interop.LoadCursorFromFile(defaultPath);
        _hoverHandle   = Win32Interop.LoadCursorFromFile(hoverPath);

        if (_defaultHandle == IntPtr.Zero)
            RobitLogger.LogError($"[GlobalCursorManager] Failed to load default cursor: {defaultPath}");
        if (_hoverHandle == IntPtr.Zero)
            RobitLogger.LogError($"[GlobalCursorManager] Failed to load hover cursor: {hoverPath}");

        // Primary cursors
        if (_defaultHandle != IntPtr.Zero)
            Win32Interop.SetSystemCursor(_defaultHandle, Win32Interop.OCR_NORMAL);
        if (_hoverHandle != IntPtr.Zero)
            Win32Interop.SetSystemCursor(_hoverHandle, Win32Interop.OCR_HAND);

        // Extended cursors
        ApplyCursor("ibeam.cur", Win32Interop.OCR_IBEAM);
        ApplyCursor("wait.cur", Win32Interop.OCR_WAIT);
        ApplyCursor("no.cur", Win32Interop.OCR_NO);
        ApplyCursor("appstarting.cur", Win32Interop.OCR_APPSTARTING);
        ApplyCursor("sizeall.cur", Win32Interop.OCR_SIZEALL);
        ApplyCursor("help.cur", Win32Interop.OCR_HELP);

        // Resizing cursors (rotated variants)
        ApplyCursor("resize3.cur", Win32Interop.OCR_SIZEWE);
        ApplyCursor("resize1.cur", Win32Interop.OCR_SIZENS);
        ApplyCursor("resize4.cur", Win32Interop.OCR_SIZENWSE);
        ApplyCursor("resize2.cur", Win32Interop.OCR_SIZENESW);

        RobitLogger.Log("[GlobalCursorManager] All custom cursors applied.");
    }

    private void ApplyCursor(string fileName, uint cursorId)
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Cursor_icons", fileName);
        if (File.Exists(path))
        {
            IntPtr handle = Win32Interop.LoadCursorFromFile(path);
            if (handle != IntPtr.Zero)
            {
                Win32Interop.SetSystemCursor(handle, cursorId);
            }
        }
    }

    /// <summary>
    /// Instantly switches to the hover (firefly_open) cursor using the cached handle.
    /// Safe to call every frame — no file I/O.
    /// </summary>
    public void SetHoverCursor()
    {
        if (_hoverHandle != IntPtr.Zero)
            Win32Interop.SetSystemCursor(_hoverHandle, Win32Interop.OCR_NORMAL);
    }

    /// <summary>
    /// Instantly switches back to the default (firefly_closed) cursor using the cached handle.
    /// Safe to call every frame — no file I/O.
    /// </summary>
    public void SetDefaultCursor()
    {
        if (_defaultHandle != IntPtr.Zero)
            Win32Interop.SetSystemCursor(_defaultHandle, Win32Interop.OCR_NORMAL);
    }

    /// <summary>
    /// Registers firefly hover cursor callbacks on a VisualElement.
    /// Call this once per interactive element — no per-frame cost.
    /// Example: GlobalCursorManager.AttachTo(myButton);
    /// </summary>
    public static void AttachTo(VisualElement element)
    {
        if (element == null) return;
        element.RegisterCallback<PointerEnterEvent>(_ => Instance?.SetHoverCursor(), TrickleDown.NoTrickleDown);
        element.RegisterCallback<PointerLeaveEvent>(_ => Instance?.SetDefaultCursor(), TrickleDown.NoTrickleDown);
    }

    /// <summary>
    /// Restores all Windows cursors to the user's saved theme.
    /// MUST be called before the application exits. Already wired to OnApplicationQuit.
    /// </summary>
    public void RestoreSystemCursors()
    {
        Win32Interop.SystemParametersInfo(Win32Interop.SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        RobitLogger.Log("[GlobalCursorManager] Windows system cursors restored.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Internal
    // ──────────────────────────────────────────────────────────────────────────

    private IntPtr _defaultHandle = IntPtr.Zero;
    private IntPtr _hoverHandle   = IntPtr.Zero;

    private static void SetSystemCursorFromFile(string absoluteFilePath, uint cursorId)
    {
        if (!File.Exists(absoluteFilePath))
        {
            RobitLogger.LogError($"[GlobalCursorManager] Cursor file not found: {absoluteFilePath}");
            return;
        }

        IntPtr handle = Win32Interop.LoadCursorFromFile(absoluteFilePath);
        if (handle == IntPtr.Zero)
        {
            RobitLogger.LogError($"[GlobalCursorManager] LoadCursorFromFile failed for: {absoluteFilePath}");
            return;
        }

        Win32Interop.SetSystemCursor(handle, cursorId);
    }

    private void OnApplicationQuit()
    {
        RestoreSystemCursors();
    }
}

