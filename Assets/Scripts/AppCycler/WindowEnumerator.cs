using System;
using System.Collections.Generic;
using System.Text;
using static Win32Interop;

public struct WindowInfo
{
    public IntPtr Hwnd;
    public string Title;
    public uint ProcessId;
}

public static class WindowEnumerator
{
    /// Returns all visible, titled, non-tool windows excluding Unity's own window.
    public static List<WindowInfo> GetVisibleWindows()
    {
        var results = new List<WindowInfo>();
        IntPtr unityHwnd = WindowManager.GetWindowHandle();

        EnumWindows((hWnd, _) =>
        {
            if (hWnd == unityHwnd) return true;
            if (!IsWindowVisible(hWnd)) return true;

            int titleLen = GetWindowTextLength(hWnd);
            if (titleLen == 0) return true;

            // Skip tool windows (system tray, tooltips, etc.)
            uint exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0) return true;

            var sb = new StringBuilder(titleLen + 1);
            GetWindowText(hWnd, sb, sb.Capacity);

            GetWindowThreadProcessId(hWnd, out uint pid);

            results.Add(new WindowInfo
            {
                Hwnd = hWnd,
                Title = sb.ToString(),
                ProcessId = pid
            });

            return true;
        }, IntPtr.Zero);

        return results;
    }
}
