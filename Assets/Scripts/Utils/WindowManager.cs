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

    // Cache the Unity window handle - set once at startup, never changes
    private static IntPtr unityHwnd = IntPtr.Zero;

    // Call this once at app startup to cache Unity's window handle
    public static void Initialize()
    {
        #if !UNITY_EDITOR
        if (unityHwnd == IntPtr.Zero)
        {
            unityHwnd = GetActiveWindow();
            Debug.Log($"[WindowManager] Initialized with Unity window handle: {unityHwnd}");
        }
        #endif
    }

    public static IntPtr GetWindowHandle()
    {
        // If not initialized, do it now
        if (unityHwnd == IntPtr.Zero)
        {
            Initialize();
        }
        return unityHwnd;
    }

    public static void MakeTransparent()
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;
        
        MARGINS margins = new MARGINS { cxLeftWidth = -1 };
        DwmExtendFrameIntoClientArea(hWnd, ref margins);
        
        SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);
        SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);
        
        Debug.Log("[WindowManager] Window set to transparent overlay mode");
        #endif
    }

    public static void MakeOpaque()
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;
        
        Debug.Log($"[WindowManager] MakeOpaque called with hWnd: {hWnd}");
        
        // Reset glass frame
        MARGINS margins = new MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 0 };
        DwmExtendFrameIntoClientArea(hWnd, ref margins);
        
        // Remove ALL extended styles
        SetWindowLong(hWnd, GWL_EXSTYLE, 0);
        
        // Restore full screen and remove topmost
        int screenWidth = Screen.currentResolution.width;
        int screenHeight = Screen.currentResolution.height;
        SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, screenWidth, screenHeight, SWP_SHOWWINDOW);
        
        Debug.Log($"[WindowManager] Window reset to opaque: {screenWidth}x{screenHeight}");
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

    public static void SetClickThrough(bool enabled)
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;
        
        if (enabled)
        {
            SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED | WS_EX_TRANSPARENT);
        }
        else
        {
            SetWindowLong(hWnd, GWL_EXSTYLE, WS_EX_LAYERED);
        }
        #endif
    }

    /// <summary>
    /// Brings the Unity window to the foreground and gives it focus.
    /// Call this when cursor enters UI area to ensure clicks are captured.
    /// </summary>
    public static void FocusWindow()
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;
        
        // Only attempt to focus if we're not already the foreground window
        if (GetForegroundWindow() != hWnd)
        {
            SetForegroundWindow(hWnd);
            SetActiveWindow(hWnd);
            Debug.Log("[WindowManager] Window brought to foreground");
        }
        #endif
    }
}
