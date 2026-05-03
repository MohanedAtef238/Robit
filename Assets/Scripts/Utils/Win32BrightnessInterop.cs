using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// Provides static helpers to get/set Windows monitor brightness
/// using the Dxva2.dll (Monitor Configuration API).
/// Works on most laptop displays and DDC/CI-capable external monitors.
public static class Win32BrightnessInterop
{
    // ── Structs ────────────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    // ── P/Invoke ───────────────────────────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
        IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

    [DllImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(
        IntPtr hMonitor, uint dwPhysicalMonitorArraySize,
        [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorBrightness(
        IntPtr hMonitor, out uint pdwMinimumBrightness,
        out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

    [DllImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

    [DllImport("Dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyPhysicalMonitors(
        uint dwPhysicalMonitorArraySize, [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    private const uint MONITOR_DEFAULTTOPRIMARY = 1;

    // ── Helpers ────────────────────────────────────────────────────────────
    private static bool TryGetPhysicalMonitor(out PHYSICAL_MONITOR monitor)
    {
        monitor = default;
        try
        {
            // Get the Unity window's primary monitor handle
            IntPtr hwnd = Win32Interop.GetActiveWindow();
            IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTOPRIMARY);
            if (hMonitor == IntPtr.Zero) return false;

            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) || count == 0)
                return false;

            var monitors = new PHYSICAL_MONITOR[count];
            if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors))
                return false;

            monitor = monitors[0];
            return true;
        }
        catch (Exception e)
        {
            RobitLogger.LogWarning($"[Win32BrightnessInterop] TryGetPhysicalMonitor failed: {e.Message}");
            return false;
        }
    }

    private static void ReleaseMonitor(ref PHYSICAL_MONITOR monitor)
    {
        try
        {
            var arr = new[] { monitor };
            DestroyPhysicalMonitors(1, arr);
        }
        catch { /* best effort */ }
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// Returns the current brightness as an int in [0, 100].
    /// Returns -1 if the API is unsupported on this display.
    public static int GetBrightness()
    {
        if (!TryGetPhysicalMonitor(out var monitor))
        {
            RobitLogger.LogWarning("[Win32BrightnessInterop] Could not acquire physical monitor. " +
                             "Brightness control may not be supported on this display.");
            return -1;
        }
        try
        {
            if (GetMonitorBrightness(monitor.hPhysicalMonitor, out uint min, out uint cur, out uint max))
            {
                // Normalize to 0–100 range
                if (max == min) return (int)cur;
                return Mathf.RoundToInt(((float)(cur - min) / (max - min)) * 100f);
            }
            RobitLogger.LogWarning("[Win32BrightnessInterop] GetMonitorBrightness returned false.");
            return -1;
        }
        finally
        {
            ReleaseMonitor(ref monitor);
        }
    }

    /// Sets the monitor brightness. Value should be in [0, 100].
    public static bool SetBrightness(int value)
    {
        value = Mathf.Clamp(value, 0, 100);

        if (!TryGetPhysicalMonitor(out var monitor))
        {
            RobitLogger.LogWarning("[Win32BrightnessInterop] Could not acquire physical monitor.");
            return false;
        }
        try
        {
            if (!GetMonitorBrightness(monitor.hPhysicalMonitor, out uint min, out _, out uint max))
            {
                RobitLogger.LogWarning("[Win32BrightnessInterop] GetMonitorBrightness failed, cannot set brightness.");
                return false;
            }

            // Map 0–100 to the monitor's actual min–max range
            uint mapped = (uint)(min + (max - min) * (value / 100f));
            bool ok = SetMonitorBrightness(monitor.hPhysicalMonitor, mapped);
            if (!ok)
                RobitLogger.LogWarning("[Win32BrightnessInterop] SetMonitorBrightness returned false.");
            return ok;
        }
        finally
        {
            ReleaseMonitor(ref monitor);
        }
    }

    /// Returns true if brightness control is supported on the primary monitor.
    public static bool IsSupported()
    {
        return GetBrightness() >= 0;
    }
}

