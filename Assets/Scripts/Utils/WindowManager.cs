using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class WindowManager
{
    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const int SW_SHOW = 5;
    private const int SW_RESTORE = 9;
    
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
    [DllImport("user32.dll")]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("Dwmapi.dll")]
    private static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern IntPtr SetActiveWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private static IntPtr unityHwnd = IntPtr.Zero;

    public static void Initialize()
    {
        #if !UNITY_EDITOR
        if (unityHwnd == IntPtr.Zero)
        {
            unityHwnd = GetActiveWindow();
            Debug.Log($"[WindowManager] Initialized: {unityHwnd}");
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
        return GetWindowLong(hWnd, GWL_EXSTYLE);
    }

    private static void SetExtendedStyle(IntPtr hWnd, uint style)
    {
        SetWindowLong(hWnd, GWL_EXSTYLE, style);
    }

    // Sets WS_EX_LAYERED + WS_EX_TRANSPARENT and extends DWM frame
    public static void MakeTransparent()
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;
        
        MARGINS margins = new MARGINS { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(hWnd, ref margins);
        
        uint currentStyle = GetExtendedStyle(hWnd);
        uint newStyle = currentStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT;
        SetExtendedStyle(hWnd, newStyle);
        SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_FRAMECHANGED);
        
        Debug.Log("[WindowManager] Transparent");
        #endif
    }

    // Strips WS_EX_LAYERED + WS_EX_TRANSPARENT and resets DWM frame
    public static void MakeOpaque()
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;
        
        uint currentStyle = GetExtendedStyle(hWnd);
        uint newStyle = currentStyle & ~WS_EX_LAYERED & ~WS_EX_TRANSPARENT;
        SetExtendedStyle(hWnd, newStyle);
        
        MARGINS margins = new MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 0 };
        DwmExtendFrameIntoClientArea(hWnd, ref margins);
        
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);
        ShowWindow(hWnd, SW_RESTORE);
        SetForegroundWindow(hWnd);
        SetActiveWindow(hWnd);
        
        Debug.Log("[WindowManager] Opaque");
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
        
        SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, windowWidth, windowHeight, SWP_SHOWWINDOW);
        
        Debug.Log($"[WindowManager] Window resized to: {windowWidth}x{windowHeight}");
        #endif
    }

    // Toggles WS_EX_TRANSPARENT while keeping WS_EX_LAYERED
    public static void SetClickThrough(bool enabled)
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;
        
        uint currentStyle = GetExtendedStyle(hWnd);
        uint newStyle = currentStyle | WS_EX_LAYERED;
        
        if (enabled)
            newStyle |= WS_EX_TRANSPARENT;
        else
            newStyle &= ~WS_EX_TRANSPARENT;
        
        SetExtendedStyle(hWnd, newStyle);
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        #endif
    }

    public static void FocusWindow()
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;
        
        if (GetForegroundWindow() != hWnd)
        {
            SetForegroundWindow(hWnd);
            SetActiveWindow(hWnd);
        }
        #endif
    }
}
