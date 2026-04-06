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

    private enum AccentState
    {
        ACCENT_DISABLED                   = 0,
        ACCENT_ENABLE_GRADIENT            = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND          = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND   = 4,
        ACCENT_INVALID_STATE              = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int         AccentFlags;
        public uint        GradientColor;  // AABBGGRR
        public int         AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int    Attribute;
        public IntPtr Data;
        public int    SizeOfData;
    }

    private const int WCA_ACCENT_POLICY = 19;

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
    private static extern int SetWindowCompositionAttribute(IntPtr hWnd, ref WindowCompositionAttributeData data);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern IntPtr SetActiveWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private static IntPtr unityHwnd = IntPtr.Zero;
    private static bool _acrylicActive;

    /// True while the DWM Acrylic blur-behind effect is active.
    public static bool IsAcrylicActive => _acrylicActive;

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

    /// <summary>
    /// Enables or disables Windows DWM Acrylic blur-behind the window.
    /// When enabled, WS_EX_LAYERED is stripped so the accent policy composites
    /// over the DWM extended frame (black pixels = glass). MakeTransparent()
    /// restores the layered style after the accent is disabled.
    /// </summary>
    /// <param name="enabled">True to enable acrylic blur, false to disable.</param>
    /// <param name="tintColor">AABBGGRR tint blended into the blur
    /// (default 0x08000000 ≈ 3 % opaque black for a light frosted-glass look).</param>
    public static void SetAcrylicBlur(bool enabled, uint tintColor = 0x08000000)
    {
        #if !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        if (hWnd == IntPtr.Zero) return;

        _acrylicActive = enabled;

        if (enabled)
        {
            // Strip WS_EX_LAYERED so the accent policy composites correctly
            // over the DWM extended frame. MakeTransparent() restores it later.
            uint style = GetExtendedStyle(hWnd);
            SetExtendedStyle(hWnd, style & ~WS_EX_LAYERED & ~WS_EX_TRANSPARENT);
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0,
                         SWP_NOSIZE | SWP_NOMOVE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        }

        var accent = new AccentPolicy
        {
            AccentState   = enabled ? AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND
                                    : AccentState.ACCENT_DISABLED,
            AccentFlags   = 2,           // draw full-window acrylic
            GradientColor = enabled ? tintColor : 0,
            AnimationId   = 0
        };

        int accentSize = Marshal.SizeOf(accent);
        IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute  = WCA_ACCENT_POLICY,
                Data       = accentPtr,
                SizeOfData = accentSize
            };
            SetWindowCompositionAttribute(hWnd, ref data);
            Debug.Log($"[WindowManager] Acrylic blur {(enabled ? "enabled" : "disabled")}");
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
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

        // While acrylic blur is active, WS_EX_LAYERED is intentionally stripped so the accent policy composites correctly over the DWM extended frame.
        if (_acrylicActive) return;
        
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

    // Window enumeration for focusing background apps

    private const uint GW_HWNDNEXT = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

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
        IntPtr next = GetWindow(hWnd, GW_HWNDNEXT);
        while (next != IntPtr.Zero)
        {
            // Skip invisible windows and windows with no title (system windows)
            if (IsWindowVisible(next) && GetWindowTextLength(next) > 0 && next != hWnd)
            {
                SetForegroundWindow(next);
                Debug.Log($"[WindowManager] Focused window behind: {next}");
                return true;
            }
            next = GetWindow(next, GW_HWNDNEXT);
        }

        Debug.LogWarning("[WindowManager] No visible window found behind Unity");
        return false;
        #else
        return false;
        #endif
    }
}
