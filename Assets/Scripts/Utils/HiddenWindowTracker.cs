using System;
using System.Collections.Generic;
using UnityEngine;

/// Tracks windows hidden by our popup suppressor so they can be restored
/// on exit, crash, or when the suppressor stops. Hooks Application.quitting
/// as a safety net — even if the app is killed, the hook fires first.
public static class HiddenWindowTracker
{
    private static readonly List<IntPtr> _hiddenByUs = new();
    private static bool _hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        if (!_hooked)
        {
            Application.quitting += RestoreAll;
            _hooked = true;
        }
        _hiddenByUs.Clear();
    }

    /// Call this BEFORE calling ShowWindow(hwnd, SW_HIDE).
    public static void Track(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        if (!_hiddenByUs.Contains(hwnd))
            _hiddenByUs.Add(hwnd);
    }

    /// Restores every window we hid back to visible and clears the list.
    public static void RestoreAll()
    {
        for (int i = _hiddenByUs.Count - 1; i >= 0; i--)
        {
            try
            {
                Win32Interop.ShowWindow(_hiddenByUs[i], Win32Interop.SW_SHOW);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HiddenWindowTracker] Failed to restore 0x{_hiddenByUs[i]:X}: {e.Message}");
            }
        }

        if (_hiddenByUs.Count > 0)
            Debug.Log($"[HiddenWindowTracker] Restored {_hiddenByUs.Count} hidden window(s).");

        _hiddenByUs.Clear();
    }

    public static int TrackedCount => _hiddenByUs.Count;
}
