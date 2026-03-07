using System;
using System.Runtime.InteropServices;
using UnityEngine.InputSystem;

/// <summary>
/// Centralised Win32 P/Invoke declarations used across the overlay system.
/// Eliminates duplicate DllImport / struct definitions scattered through
/// CaptureTextureRenderer, SyntheticInputInjector, and macro actions.
/// </summary>
public static class Win32Interop
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Window queries
    // ═══════════════════════════════════════════════════════════════════════════

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    // ═══════════════════════════════════════════════════════════════════════════
    // Window focus & Z-order (used by WindowManager)
    // ═══════════════════════════════════════════════════════════════════════════

    [DllImport("user32.dll")]
    public static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr SetActiveWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    public const uint GW_HWNDNEXT = 2;

    // ═══════════════════════════════════════════════════════════════════════════
    // Window positioning & Z-order (used by HolePunchController)
    // ═══════════════════════════════════════════════════════════════════════════

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    // SetWindowPos hWndInsertAfter constants
    public static readonly IntPtr HWND_TOPMOST   = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST  = new IntPtr(-2);
    public static readonly IntPtr HWND_TOP        = new IntPtr(0);
    public static readonly IntPtr HWND_BOTTOM     = new IntPtr(1);

    // SetWindowPos flags
    public const uint SWP_NOSIZE       = 0x0001;
    public const uint SWP_NOMOVE       = 0x0002;
    public const uint SWP_NOACTIVATE   = 0x0010;
    public const uint SWP_SHOWWINDOW   = 0x0040;
    public const uint SWP_NOZORDER     = 0x0004;
    public const uint SWP_FRAMECHANGED = 0x0020;

    // ShowWindow commands
    public const int SW_HIDE            = 0;
    public const int SW_SHOW            = 5;
    public const int SW_SHOWNOACTIVATE  = 4;
    public const int SW_RESTORE         = 9;

    // ═══════════════════════════════════════════════════════════════════════════
    // GDI Region functions (used for cutting holes in the Unity window)
    // ═══════════════════════════════════════════════════════════════════════════

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("gdi32.dll")]
    public static extern int CombineRgn(IntPtr hrgnDest, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int fnCombineMode);

    [DllImport("user32.dll")]
    public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    // CombineRgn modes
    public const int RGN_AND  = 1;
    public const int RGN_OR   = 2;
    public const int RGN_XOR  = 3;
    public const int RGN_DIFF = 4; // Subtract region 2 from region 1
    public const int RGN_COPY = 5;

    // ═══════════════════════════════════════════════════════════════════════════
    // Window style manipulation
    // ═══════════════════════════════════════════════════════════════════════════

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    public const int GWL_EXSTYLE = -20;
    public const int GWL_STYLE   = -16;

    public const uint WS_EX_LAYERED     = 0x00080000;
    public const uint WS_EX_TRANSPARENT = 0x00000020;
    public const uint WS_EX_TOOLWINDOW  = 0x00000080;
    public const uint WS_EX_TOPMOST     = 0x00000008;

    public const uint WS_POPUP     = 0x80000000;
    public const uint WS_VISIBLE   = 0x10000000;
    public const uint WS_CAPTION   = 0x00C00000;
    public const uint WS_BORDER    = 0x00800000;
    public const uint WS_THICKFRAME = 0x00040000;

    // ═══════════════════════════════════════════════════════════════════════════
    // Cursor / coordinate helpers
    // ═══════════════════════════════════════════════════════════════════════════

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    // ═══════════════════════════════════════════════════════════════════════════
    // SendInput — mouse & keyboard injection
    // ═══════════════════════════════════════════════════════════════════════════

    [Flags]
    public enum InputType : uint
    {
        Mouse    = 0,
        Keyboard = 1,
    }

    [Flags]
    public enum MouseEventFlags : uint
    {
        Move       = 0x0001,
        LeftDown   = 0x0002,
        LeftUp     = 0x0004,
        RightDown  = 0x0008,
        RightUp    = 0x0010,
        MiddleDown = 0x0020,
        MiddleUp   = 0x0040,
        Wheel      = 0x0800,
        Absolute   = 0x8000,
    }

    [Flags]
    public enum KeyEventFlags : uint
    {
        KeyDown = 0x0000,
        KeyUp   = 0x0002,
        Unicode = 0x0004,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public InputType type;
        public InputUnion union;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // ═══════════════════════════════════════════════════════════════════════════
    // keybd_event — used by macro actions for simple key combos
    // ═══════════════════════════════════════════════════════════════════════════

    public const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    // ── Common virtual-key codes used by macros ────────────────────────────────
    public const byte VK_TAB      = 0x09;
    public const byte VK_CONTROL  = 0x11;
    public const byte VK_MENU     = 0x12; // Alt
    public const byte VK_OEM_PLUS = 0xBB;
    public const byte VK_OEM_MINUS = 0xBD;

    // ═══════════════════════════════════════════════════════════════════════════
    // Unity Key → Win32 VK mapping (used by SyntheticInputInjector)
    // ═══════════════════════════════════════════════════════════════════════════

    public static int KeyToVirtualKey(Key key)
    {
        return key switch
        {
            // Letters
            Key.A => 0x41, Key.B => 0x42, Key.C => 0x43, Key.D => 0x44,
            Key.E => 0x45, Key.F => 0x46, Key.G => 0x47, Key.H => 0x48,
            Key.I => 0x49, Key.J => 0x4A, Key.K => 0x4B, Key.L => 0x4C,
            Key.M => 0x4D, Key.N => 0x4E, Key.O => 0x4F, Key.P => 0x50,
            Key.Q => 0x51, Key.R => 0x52, Key.S => 0x53, Key.T => 0x54,
            Key.U => 0x55, Key.V => 0x56, Key.W => 0x57, Key.X => 0x58,
            Key.Y => 0x59, Key.Z => 0x5A,

            // Numbers
            Key.Digit0 => 0x30, Key.Digit1 => 0x31, Key.Digit2 => 0x32,
            Key.Digit3 => 0x33, Key.Digit4 => 0x34, Key.Digit5 => 0x35,
            Key.Digit6 => 0x36, Key.Digit7 => 0x37, Key.Digit8 => 0x38,
            Key.Digit9 => 0x39,

            // Function keys
            Key.F1 => 0x70, Key.F2 => 0x71, Key.F3 => 0x72, Key.F4 => 0x73,
            Key.F5 => 0x74, Key.F6 => 0x75, Key.F7 => 0x76, Key.F8 => 0x77,
            Key.F9 => 0x78, Key.F10 => 0x79, Key.F11 => 0x7A, Key.F12 => 0x7B,

            // Modifiers
            Key.LeftShift   => 0xA0, Key.RightShift  => 0xA1,
            Key.LeftCtrl    => 0xA2, Key.RightCtrl   => 0xA3,
            Key.LeftAlt     => 0xA4, Key.RightAlt    => 0xA5,

            // Navigation
            Key.Enter       => 0x0D,
            Key.Escape      => 0x1B,
            Key.Space       => 0x20,
            Key.Tab         => 0x09,
            Key.Backspace   => 0x08,
            Key.Delete      => 0x2E,
            Key.Insert      => 0x2D,
            Key.Home        => 0x24,
            Key.End         => 0x23,
            Key.PageUp      => 0x21,
            Key.PageDown    => 0x22,
            Key.UpArrow     => 0x26,
            Key.DownArrow   => 0x28,
            Key.LeftArrow   => 0x25,
            Key.RightArrow  => 0x27,

            // Punctuation
            Key.Minus       => 0xBD,
            Key.Equals      => 0xBB,
            Key.LeftBracket => 0xDB,
            Key.RightBracket=> 0xDD,
            Key.Backslash   => 0xDC,
            Key.Semicolon   => 0xBA,
            Key.Quote       => 0xDE,
            Key.Comma       => 0xBC,
            Key.Period      => 0xBE,
            Key.Slash       => 0xBF,
            Key.Backquote   => 0xC0,

            // Numpad
            Key.Numpad0 => 0x60, Key.Numpad1 => 0x61, Key.Numpad2 => 0x62,
            Key.Numpad3 => 0x63, Key.Numpad4 => 0x64, Key.Numpad5 => 0x65,
            Key.Numpad6 => 0x66, Key.Numpad7 => 0x67, Key.Numpad8 => 0x68,
            Key.Numpad9 => 0x69,
            Key.NumpadMultiply => 0x6A,
            Key.NumpadPlus     => 0x6B,
            Key.NumpadMinus    => 0x6D,
            Key.NumpadPeriod   => 0x6E,
            Key.NumpadDivide   => 0x6F,
            Key.NumpadEnter    => 0x0D,

            // Special
            Key.CapsLock    => 0x14,
            Key.NumLock     => 0x90,
            Key.ScrollLock  => 0x91,
            Key.PrintScreen => 0x2C,
            Key.Pause       => 0x13,

            _ => 0, // unmapped
        };
    }
}
