using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class WindowManager
{
    private static IntPtr unityHwnd = IntPtr.Zero;
    private static bool _acrylicActive;

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

    // Toggles WS_EX_TRANSPARENT while keeping WS_EX_LAYERED
    public static void SetClickThrough(bool enabled)
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;

        // While acrylic blur is active, WS_EX_LAYERED is intentionally stripped so the accent policy composites correctly over the DWM extended frame.
        if (_acrylicActive) return;
        
        uint currentStyle = GetExtendedStyle(hWnd);
        uint newStyle = currentStyle | Win32Interop.WS_EX_LAYERED;
        
        if (enabled)
            newStyle |= Win32Interop.WS_EX_TRANSPARENT;
        else
            newStyle &= ~Win32Interop.WS_EX_TRANSPARENT;
        
        SetExtendedStyle(hWnd, newStyle);
        Win32Interop.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, Win32Interop.SWP_NOSIZE | Win32Interop.SWP_NOMOVE | Win32Interop.SWP_FRAMECHANGED | Win32Interop.SWP_SHOWWINDOW);
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

    // Window enumeration for focusing background apps

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    /// this will find the first visible window behind Unity in Z-order and focuses it.
    /// Returns true if a window was found and focused.
    public static bool FocusWindowBehind()
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return false;

        // Walk Z-order starting from Unity's window
        IntPtr next = Win32Interop.GetWindow(hWnd, Win32Interop.GW_HWNDNEXT);
        while (next != IntPtr.Zero)
        {
            // Skip invisible windows and windows with no title (system windows)
            if (Win32Interop.IsWindowVisible(next) && Win32Interop.GetWindowTextLength(next) > 0 && next != hWnd)
            {
                Win32Interop.SetForegroundWindow(next);
                RobitLogger.Log($"[WindowManager] Focused window behind: {next}");
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

