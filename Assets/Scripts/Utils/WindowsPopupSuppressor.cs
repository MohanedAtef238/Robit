using System;
using System.Collections;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using UnityEngine;

/// Periodically scans for Windows volume/brightness/quick-settings popups
/// and hides them while active. Always tracks hidden windows via
/// HiddenWindowTracker so they can be restored on stop or crash.
public class WindowsPopupSuppressor : MonoBehaviour
{
    private Coroutine _pollRoutine;
    private bool _running;

    private Win32Interop.EnumWindowsProc _enumProc;
    private Guid _enumProcToken;
    private int _inCallback; // 0=idle, 1=busy
    private StringBuilder _classBuf = new StringBuilder(256);
    private StringBuilder _titleBuf = new StringBuilder(256);

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

        // Cache the delegate to prevent GC collection while P/Invoke is in-flight.
        // Even if the class is pinned, the delegate object itself can move.
        _enumProc = OnEnumWindow;
        _enumProcToken = UnityNativeBridge.Coordinator.TrackManagedObject(_enumProc);

        _pollRoutine = StartCoroutine(PollLoop());
        RobitLogger.Log("[PopupSuppressor] Started.");
    }

    public void StopSuppressing()
    {
        _running = false;
        if (_pollRoutine != null)
        {
            StopCoroutine(_pollRoutine);
            _pollRoutine = null;
        }

        // Drain phase: Wait for any active P/Invoke callback to complete before unpinning.
        var sw = Stopwatch.StartNew();
        while (Interlocked.CompareExchange(ref _inCallback, 0, 0) == 1 && sw.ElapsedMilliseconds < 200)
            Thread.Sleep(5);

        if (_enumProcToken != Guid.Empty)
        {
            UnityNativeBridge.Coordinator.Release(_enumProcToken);
            _enumProcToken = Guid.Empty;
        }
        _enumProc = null;

        HiddenWindowTracker.RestoreAll();
        RobitLogger.Log("[PopupSuppressor] Stopped — all hidden windows restored.");
    }

    private IEnumerator PollLoop()
    {
        while (_running)
        {
            // Use the pinned delegate instance
            Win32Interop.EnumWindows(_enumProc, IntPtr.Zero);
            yield return new WaitForSeconds(0.3f);
        }
    }

    private bool OnEnumWindow(IntPtr hWnd, IntPtr lParam)
    {
        try { UnityNativeBridge.Coordinator.EnterCallback(); }
        catch (InvalidOperationException)
        {
            // Shutdown started mid-enumeration — stop cleanly.
            RobitLogger.Log("[PopupSuppressor] Shutdown detected mid-enumeration — aborting.");
            return false;
        }

        Interlocked.Exchange(ref _inCallback, 1);
        try
        {
            if (!Win32Interop.IsWindowVisible(hWnd)) return true;

            // Check window class
            _classBuf.Clear();
            Win32Interop.RealGetWindowClass(hWnd, _classBuf, 256);
            string cls = _classBuf.ToString();

            foreach (var target in SuppressClasses)
            {
                if (cls.Equals(target, StringComparison.OrdinalIgnoreCase))
                {
                    HideWindow(hWnd, cls);
                    return true;
                }
            }

            // Check window title for fallback matching
            int len = Win32Interop.GetWindowTextLength(hWnd);
            if (len > 0)
            {
                _titleBuf.Clear();
                _titleBuf.EnsureCapacity(len + 1);
                Win32Interop.GetWindowText(hWnd, _titleBuf, len + 1);
                string title = _titleBuf.ToString();

                foreach (var keyword in SuppressTitleContains)
                {
                    if (title.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        HideWindow(hWnd, title);
                        return true;
                    }
                }
            }

            return true; // continue enumeration
        }
        catch (Exception e)
        {
            RobitLogger.LogWarning($"[PopupSuppressor] Error in EnumWindows callback: {e.Message}");
            return false; // abort enumeration on error
        }
        finally
        {
            Interlocked.Exchange(ref _inCallback, 0);
            UnityNativeBridge.Coordinator.ExitCallback();
        }
    }

    private static void HideWindow(IntPtr hwnd, string identifier)
    {
        HiddenWindowTracker.Track(hwnd);
        Win32Interop.ShowWindow(hwnd, Win32Interop.SW_HIDE);
        RobitLogger.Log($"[PopupSuppressor] Hid window: {identifier} (0x{hwnd:X})");
    }

    private void OnDestroy()
    {
        if (_running) StopSuppressing();
    }
}

