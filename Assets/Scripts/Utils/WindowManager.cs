using System;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Diagnostics.CodeAnalysis;

// Excluded from code coverage because simulating Win32/COM errors within managed tests is highly dangerous and can irreparably crash or break the host Windows environment.
[ExcludeFromCodeCoverage]
public static class WindowManager
{
    private static IntPtr unityHwnd = IntPtr.Zero;
    private static IntPtr _lastKnownAppHwnd = IntPtr.Zero;
    private static bool _acrylicActive;
    private static Win32Interop.RECT _savedRect;  // pinned size before DPI change
    private static bool _hasSavedRect;

    /// The handle of the app that was in the foreground just before Unity last took focus.
    /// Used by KeyComboMacroAction to restore focus to the correct app.
    public static IntPtr LastKnownAppHwnd => _lastKnownAppHwnd;

    /// True while the DWM Acrylic blur-behind effect is active.
    public static bool IsAcrylicActive => _acrylicActive;

    public static void Initialize()
    {
        #if !UNITY_EDITOR
        if (unityHwnd == IntPtr.Zero)
        {
            unityHwnd = Win32Interop.GetActiveWindow();
            RobitLogger.Log($"[WindowManager] Initialized: {unityHwnd}");
        }
        #endif
    }

    public static IntPtr GetWindowHandle()
    {
        if (unityHwnd == IntPtr.Zero)
            Initialize();
        return unityHwnd;
    }

    /// Captures the Unity window's current pixel size and position.
    /// Call this BEFORE triggering a system DPI change.
    public static void SaveWindowSize()
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;
        if (Win32Interop.GetWindowRect(hWnd, out var rect))
        {
            _savedRect = rect;
            _hasSavedRect = true;
            RobitLogger.Log($"[WindowManager] Saved window rect: {rect.Left},{rect.Top} {rect.Right - rect.Left}x{rect.Bottom - rect.Top}");
        }
        #endif
    }

    /// Restores the Unity window to its saved pixel size and position.
    /// Call this AFTER a system DPI change so Windows doesn't rescale us.
    public static void RestoreWindowSize()
    {
        #if !UNITY_EDITOR
        if (!_hasSavedRect) return;
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;
        int w = _savedRect.Right  - _savedRect.Left;
        int h = _savedRect.Bottom - _savedRect.Top;
        // SWP_NOZORDER keeps topmost state intact; NOACTIVATE avoids stealing focus.
        Win32Interop.SetWindowPos(hWnd, Win32Interop.HWND_TOPMOST,
            _savedRect.Left, _savedRect.Top, w, h,
            Win32Interop.SWP_SHOWWINDOW | Win32Interop.SWP_NOACTIVATE);
        RobitLogger.Log($"[WindowManager] Restored window rect: {_savedRect.Left},{_savedRect.Top} {w}x{h}");
        #endif
    }

    private static uint GetExtendedStyle(IntPtr hWnd)
    {
        return Win32Interop.GetWindowLong(hWnd, Win32Interop.GWL_EXSTYLE);
    }

    private static void SetExtendedStyle(IntPtr hWnd, uint style)
    {
        Win32Interop.SetWindowLong(hWnd, Win32Interop.GWL_EXSTYLE, style);
    }

    // Sets WS_EX_LAYERED + WS_EX_TRANSPARENT and extends DWM frame
    public static void MakeTransparent()
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;
        
        Win32Interop.MARGINS margins = new Win32Interop.MARGINS { cxLeftWidth = -1 };
        Win32Interop.DwmExtendFrameIntoClientArea(hWnd, ref margins);
        
        uint currentStyle = GetExtendedStyle(hWnd);
        uint newStyle = currentStyle | Win32Interop.WS_EX_LAYERED | Win32Interop.WS_EX_TRANSPARENT;
        SetExtendedStyle(hWnd, newStyle);
        
        // Modern Windows 11 Visuals
        SetWindowCorners(Win32Interop.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND);
        SetDarkMode(true);
        
        Win32Interop.SetWindowPos(hWnd, Win32Interop.HWND_TOPMOST, 0, 0, 0, 0, Win32Interop.SWP_NOSIZE | Win32Interop.SWP_NOMOVE | Win32Interop.SWP_FRAMECHANGED);
        
        RobitLogger.Log("[WindowManager] Transparent");
        #endif
    }

    /// <summary>
    /// Enables or disables Windows DWM Acrylic blur-behind the window using official Windows 11 APIs.
    /// </summary>
    public static void SetAcrylicBlur(bool enabled)
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;

        _acrylicActive = enabled;

        if (enabled)
        {
            // Windows 11 Modern Acrylic
            SetSystemBackdrop(Win32Interop.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW);
        }
        else
        {
            SetSystemBackdrop(Win32Interop.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE);
        }
        
        RobitLogger.Log($"[WindowManager] Modern Acrylic blur {(enabled ? "enabled" : "disabled")}");
        #endif
    }

    /// <summary>
    /// Sets the Windows 11 System Backdrop (Mica/Acrylic).
    /// </summary>
    public static void SetSystemBackdrop(Win32Interop.DWM_SYSTEMBACKDROP_TYPE backdrop)
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;

        int value = (int)backdrop;
        Win32Interop.DwmSetWindowAttribute(hWnd, Win32Interop.DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, ref value, sizeof(int));
        #endif
    }

    /// <summary>
    /// Sets the Windows 11 Window Corner Preference (Rounded/Square).
    /// </summary>
    public static void SetWindowCorners(Win32Interop.DWM_WINDOW_CORNER_PREFERENCE preference)
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;

        int value = (int)preference;
        Win32Interop.DwmSetWindowAttribute(hWnd, Win32Interop.DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, ref value, sizeof(int));
        #endif
    }

    /// <summary>
    /// Toggles Immersive Dark Mode for the window.
    /// </summary>
    public static void SetDarkMode(bool enabled)
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;

        int value = enabled ? 1 : 0;
        Win32Interop.DwmSetWindowAttribute(hWnd, Win32Interop.DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        #endif
    }

    // Strips WS_EX_LAYERED + WS_EX_TRANSPARENT and resets DWM frame
    public static void MakeOpaque()
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;
        
        uint currentStyle = GetExtendedStyle(hWnd);
        uint newStyle = currentStyle & ~Win32Interop.WS_EX_LAYERED & ~Win32Interop.WS_EX_TRANSPARENT;
        SetExtendedStyle(hWnd, newStyle);
        
        Win32Interop.MARGINS margins = new Win32Interop.MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 0 };
        Win32Interop.DwmExtendFrameIntoClientArea(hWnd, ref margins);
        
        Win32Interop.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, Win32Interop.SWP_NOMOVE | Win32Interop.SWP_NOSIZE | Win32Interop.SWP_FRAMECHANGED);
        Win32Interop.ShowWindow(hWnd, Win32Interop.SW_RESTORE);
        Win32Interop.SetForegroundWindow(hWnd);
        Win32Interop.SetActiveWindow(hWnd);
        
        RobitLogger.Log("[WindowManager] Opaque");
        #endif
    }

    public static void SetOverlaySize()
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;
        
        int screenWidth = Screen.currentResolution.width;
        int screenHeight = Screen.currentResolution.height;
        int windowWidth = (int)(screenWidth * 0.7f);
        int windowHeight = (int)(screenHeight * 0.7f);
        
        Win32Interop.SetWindowPos(hWnd, Win32Interop.HWND_TOPMOST, 0, 0, windowWidth, windowHeight, Win32Interop.SWP_SHOWWINDOW);
        
        RobitLogger.Log($"[WindowManager] Window resized to: {windowWidth}x{windowHeight}");
        #endif
    }

    /// <summary>
    /// Resizes the window to cover the full monitor, keeping it topmost.
    /// Call from GazeCalibrationScene so calibration dots appear at physical screen positions.
    /// OverlayManager.Start() will call SetOverlaySize() when returning to OverlayScene.
    /// </summary>
    public static void MakeFullscreen()
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;

        int screenWidth = Screen.currentResolution.width;
        int screenHeight = Screen.currentResolution.height;

        Win32Interop.SetWindowPos(hWnd, Win32Interop.HWND_TOPMOST, 0, 0, screenWidth, screenHeight, Win32Interop.SWP_SHOWWINDOW);

        RobitLogger.Log($"[WindowManager] Window set to fullscreen: {screenWidth}x{screenHeight}");
        #endif
    }

    // Toggles WS_EX_TRANSPARENT while keeping WS_EX_LAYERED
    public static void SetClickThrough(bool enabled)
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;

        
        uint currentStyle = GetExtendedStyle(hWnd);
        uint newStyle = currentStyle | Win32Interop.WS_EX_LAYERED;
        
        if (enabled)
            newStyle |= Win32Interop.WS_EX_TRANSPARENT;
        else
            newStyle &= ~Win32Interop.WS_EX_TRANSPARENT;
        
        if (newStyle != currentStyle)
        {
            SetExtendedStyle(hWnd, newStyle);
            // SWP_NOACTIVATE prevents Windows from treating the style refresh as a window
            // activation event, which would otherwise consume the first click as an
            // "activate-this-window" click and never deliver it to Unity.
            Win32Interop.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, Win32Interop.SWP_NOSIZE | Win32Interop.SWP_NOMOVE | Win32Interop.SWP_FRAMECHANGED | Win32Interop.SWP_NOACTIVATE);
        }
        #endif
    }

    public static void FocusWindow()
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;
        
        if (Win32Interop.GetForegroundWindow() != hWnd)
        {
            Win32Interop.SetForegroundWindow(hWnd);
            Win32Interop.SetActiveWindow(hWnd);
        }
        #endif
    }

    /// Captures the current foreground window so FocusWindowBehind() can restore it reliably.
    /// Call this BEFORE calling FocusWindow() or SetClickThrough(false).
    public static void RecordForegroundApp()
    {
        #if !UNITY_EDITOR
        IntPtr fg = Win32Interop.GetForegroundWindow();
        // Only record if it's not our own window
        if (fg != IntPtr.Zero && fg != unityHwnd)
            _lastKnownAppHwnd = fg;
        #endif
    }



    /// Focuses the last-known app window (captured before Unity took foreground).
    /// Falls back to Z-order walk if the saved handle is stale.
    /// Returns true if a window was found and focused.
    public static bool FocusWindowBehind()
    {
        #if !UNITY_EDITOR
        // Fast path: use the handle we captured before Unity stole focus
        if (_lastKnownAppHwnd != IntPtr.Zero && _lastKnownAppHwnd != unityHwnd)
        {
            if (Win32Interop.IsWindowVisible(_lastKnownAppHwnd) && 
                Win32Interop.GetWindowTextLength(_lastKnownAppHwnd) > 0)
            {
                Win32Interop.SetForegroundWindow(_lastKnownAppHwnd);
                RobitLogger.Log($"[WindowManager] Focused last-known app: {_lastKnownAppHwnd}");
                return true;
            }
        }

        // Fallback: walk Z-order
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return false;

        IntPtr next = Win32Interop.GetWindow(hWnd, Win32Interop.GW_HWNDNEXT);
        while (next != IntPtr.Zero)
        {
            if (Win32Interop.IsWindowVisible(next) && Win32Interop.GetWindowTextLength(next) > 0 && next != hWnd)
            {
                Win32Interop.SetForegroundWindow(next);
                RobitLogger.Log($"[WindowManager] Focused window behind (fallback): {next}");
                return true;
            }
            next = Win32Interop.GetWindow(next, Win32Interop.GW_HWNDNEXT);
        }

        RobitLogger.LogWarning("[WindowManager] No visible window found behind Unity");
        return false;
        #else
        return false;
        #endif
    }
}

