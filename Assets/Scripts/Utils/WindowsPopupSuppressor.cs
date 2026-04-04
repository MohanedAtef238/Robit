using System;
using System.Collections;
using System.Text;
using UnityEngine;

/// Periodically scans for Windows volume/brightness/quick-settings popups
/// and hides them while active. Always tracks hidden windows via
/// HiddenWindowTracker so they can be restored on stop or crash.
public class WindowsPopupSuppressor : MonoBehaviour
{
    private Coroutine _pollRoutine;
    private bool _running;

    // Window classes / titles to suppress
    private static readonly string[] SuppressClasses =
    {
        "SndVolSSO",                              // Win10 volume popup
        "NativeHWNDHost",                         // Win10 brightness/quick actions
        "TopLevelWindowForOverflowXamlIsland",    // Win11 Quick Settings panel
    };

    private static readonly string[] SuppressTitleContains =
    {
        "Quick Settings",
        "Volume Mixer",
    };

    public void StartSuppressing()
    {
        if (_running) return;
        _running = true;
        _pollRoutine = StartCoroutine(PollLoop());
        Debug.Log("[PopupSuppressor] Started.");
    }

    public void StopSuppressing()
    {
        _running = false;
        if (_pollRoutine != null)
        {
            StopCoroutine(_pollRoutine);
            _pollRoutine = null;
        }
        HiddenWindowTracker.RestoreAll();
        Debug.Log("[PopupSuppressor] Stopped — all hidden windows restored.");
    }

    private IEnumerator PollLoop()
    {
        var classBuf = new StringBuilder(256);
        var titleBuf = new StringBuilder(256);

        while (_running)
        {
            Win32Interop.EnumWindows((hwnd, _) =>
            {
                if (!Win32Interop.IsWindowVisible(hwnd)) return true;

                // Check window class
                classBuf.Clear();
                Win32Interop.RealGetWindowClass(hwnd, classBuf, 256);
                string cls = classBuf.ToString();

                foreach (var target in SuppressClasses)
                {
                    if (cls.Equals(target, StringComparison.OrdinalIgnoreCase))
                    {
                        HideWindow(hwnd, cls);
                        return true;
                    }
                }

                // Check window title for fallback matching
                int len = Win32Interop.GetWindowTextLength(hwnd);
                if (len > 0)
                {
                    titleBuf.Clear();
                    titleBuf.EnsureCapacity(len + 1);
                    Win32Interop.GetWindowText(hwnd, titleBuf, len + 1);
                    string title = titleBuf.ToString();

                    foreach (var keyword in SuppressTitleContains)
                    {
                        if (title.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            HideWindow(hwnd, title);
                            return true;
                        }
                    }
                }

                return true; // continue enumeration
            }, IntPtr.Zero);

            yield return new WaitForSeconds(0.3f);
        }
    }

    private static void HideWindow(IntPtr hwnd, string identifier)
    {
        HiddenWindowTracker.Track(hwnd);
        Win32Interop.ShowWindow(hwnd, Win32Interop.SW_HIDE);
        Debug.Log($"[PopupSuppressor] Hid window: {identifier} (0x{hwnd:X})");
    }

    private void OnDestroy()
    {
        if (_running) StopSuppressing();
    }
}
