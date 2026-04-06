using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using UnityEngine;

/// Static helpers to read and write the Windows system DPI scaling level.
/// Reads/writes HKCU\Control Panel\Desktop\LogPixels and broadcasts
/// WM_SETTINGCHANGE so the shell updates immediately. A sign-out/sign-in
/// cycle is required for all running apps to fully adopt the new DPI, but
/// newly launched apps and the taskbar respond right away.
public static class Win32DisplayScaleInterop
{
    private const int    HWND_BROADCAST   = 0xFFFF;
    private const uint   WM_SETTINGCHANGE = 0x001A;
    private const uint   SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr   hWnd,
        uint     Msg,
        UIntPtr  wParam,
        string   lParam,
        uint     fuFlags,
        uint     uTimeout,
        out UIntPtr lpdwResult);

    // Maps percentage → logical DPI value
    private static readonly int[] Percentages = { 100, 125, 150, 200 };
    private static readonly int[] DpiValues   = {  96, 120, 144, 192 };

    private const string RegPathDesktop = @"Control Panel\Desktop";
    private const string RegPathWindowMetrics = @"Control Panel\Desktop\WindowMetrics";
    private const string RegKeyLogPixels = "LogPixels";
    private const string RegKeyWin8DpiScaling = "Win8DpiScaling";
    private const string RegKeyDesktopDpiOverride = "DesktopDPIOverride";

    /// Returns the currently configured scale percentage (100/125/150/200).
    /// Returns 100 if the registry key is absent or unrecognised.
    public static int GetScalePercent()
    {
#if !(UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
        Debug.LogWarning("[Win32DisplayScaleInterop] GetScalePercent is only supported on Windows.");
        return 100;
#else
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPathDesktop, writable: false);
            if (key == null) return 100;
            var value = key.GetValue(RegKeyLogPixels);
            if (value == null) return 100;
            int dpi = Convert.ToInt32(value);
            for (int i = 0; i < DpiValues.Length; i++)
                if (DpiValues[i] == dpi) return Percentages[i];
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Win32DisplayScaleInterop] GetScalePercent failed: {ex.Message}");
        }
        return 100;
#endif
    }

    /// Sets the system DPI scaling to the given percentage (100/125/150/200).
    /// Returns true if the registry was written successfully.
    public static bool SetScalePercent(int percent)
    {
#if !(UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
        Debug.LogWarning("[Win32DisplayScaleInterop] SetScalePercent is only supported on Windows.");
        return false;
#else
        int dpi = PercentToDpi(percent);
        if (dpi < 0)
        {
            Debug.LogWarning($"[Win32DisplayScaleInterop] Unsupported scale: {percent}%");
            return false;
        }

        int desktopOverride = PercentToDesktopOverride(percent);

        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RegPathDesktop, writable: true)
                             ?? Registry.CurrentUser.CreateSubKey(RegPathDesktop))
            {
                key.SetValue(RegKeyLogPixels, dpi, RegistryValueKind.DWord);
                key.SetValue(RegKeyWin8DpiScaling, percent == 100 ? 0 : 1, RegistryValueKind.DWord);
                key.SetValue(RegKeyDesktopDpiOverride, desktopOverride, RegistryValueKind.DWord);
            }

            using (var metricsKey = Registry.CurrentUser.OpenSubKey(RegPathWindowMetrics, writable: true)
                                    ?? Registry.CurrentUser.CreateSubKey(RegPathWindowMetrics))
            {
                metricsKey.SetValue(RegKeyAppliedDpi, dpi, RegistryValueKind.DWord);
            }

            // Broadcast WM_SETTINGCHANGE so Explorer and the taskbar pick it up
            SendMessageTimeout(
                new IntPtr(HWND_BROADCAST),
                WM_SETTINGCHANGE,
                UIntPtr.Zero,
                "Desktop",
                SMTO_ABORTIFHUNG,
                3000,
                out _);

            Debug.Log($"[Win32DisplayScaleInterop] DPI set to {dpi} ({percent}%). " +
                      "Some apps require sign out/in or restart to fully apply scale changes.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Win32DisplayScaleInterop] SetScalePercent failed: {ex.Message}");
            return false;
        }
#endif
    }

    private static int PercentToDpi(int percent)
    {
        for (int i = 0; i < Percentages.Length; i++)
            if (Percentages[i] == percent) return DpiValues[i];
        return -1;
    }

    private const string RegKeyAppliedDpi = "AppliedDPI";

    private static int PercentToDesktopOverride(int percent)
    {
        return percent switch
        {
            100 => 0,
            125 => -1,
            150 => -2,
            200 => -4,
            _ => 0
        };
    }
}
