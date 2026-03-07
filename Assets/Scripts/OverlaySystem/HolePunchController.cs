using System;
using System.Diagnostics;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using static Win32Interop;

/// <summary>
/// Manages the "Hole-Punch" overlay for external applications.
/// 
/// When activated:
///   1. Launches (or finds) the target application (e.g., Chrome)
///   2. Cuts a transparent, click-through hole in the Unity window
///      at the exact screen position of the CaptureDisplay RawImage
///   3. Positions the real app window behind that hole
///   4. Continuously tracks the panel's position each frame
///
/// When deactivated:
///   1. Restores the Unity window to its full solid region
///   2. Hides the external app window
/// </summary>
[AddComponentMenu("Mock OS/Hole Punch Controller")]
public class HolePunchController : MonoBehaviour
{
    [Header("Target Application")]
    [Tooltip("Process name to find (e.g. 'chrome'). If not running, we'll launch it.")]
    public string targetProcessName = "chrome";

    [Tooltip("Full path to the executable. Used if the process isn't already running.")]
    public string executablePath = "chrome";

    [Header("References")]
    [Tooltip("The RawImage panel where the app should appear behind.")]
    public RectTransform panelRect;

    [Tooltip("The camera rendering this Canvas (for world-space → screen conversion).")]
    public Camera renderCamera;

    // ── State ──────────────────────────────────────────────────────────────────
    private IntPtr unityHwnd = IntPtr.Zero;
    private IntPtr appHwnd   = IntPtr.Zero;
    private bool isActive    = false;
    private Process appProcess = null;
    private Coroutine findWindowCoroutine = null;

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Start the hole-punch overlay. Launches the app if needed,
    /// finds its window, and begins tracking.
    /// </summary>
    public void Activate()
    {
        if (isActive) return;

        #if UNITY_EDITOR
        UnityEngine.Debug.Log("[HolePunchController] Hole-punch only works in builds. Skipping.");
        return;
        #endif

        #pragma warning disable CS0162
        isActive = true;

        // Cache Unity's own window handle
        unityHwnd = GetActiveWindow();
        if (unityHwnd == IntPtr.Zero)
        {
            UnityEngine.Debug.LogError("[HolePunchController] Could not get Unity window handle.");
            isActive = false;
            return;
        }

        // Make Unity topmost so it stays above the target app
        SetWindowPos(unityHwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        // Launch or find the target app
        findWindowCoroutine = StartCoroutine(FindOrLaunchApp());
        #pragma warning restore CS0162
    }

    /// <summary>
    /// Stop the hole-punch overlay. Restores Unity's window and hides the app.
    /// </summary>
    public void Deactivate()
    {
        if (!isActive) return;
        isActive = false;

        if (findWindowCoroutine != null)
        {
            StopCoroutine(findWindowCoroutine);
            findWindowCoroutine = null;
        }

        // Restore Unity window region to full (null = no clipping)
        if (unityHwnd != IntPtr.Zero)
        {
            SetWindowRgn(unityHwnd, IntPtr.Zero, true);

            // Remove topmost flag
            SetWindowPos(unityHwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        // Hide the external app
        if (appHwnd != IntPtr.Zero)
        {
            ShowWindow(appHwnd, SW_HIDE);
            appHwnd = IntPtr.Zero;
        }
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Update()
    {
        if (!isActive || appHwnd == IntPtr.Zero || unityHwnd == IntPtr.Zero) return;
        if (panelRect == null) return;

        UpdateHolePunch();
    }

    void OnDestroy()
    {
        if (isActive)
            Deactivate();
    }

    void OnApplicationQuit()
    {
        if (isActive)
            Deactivate();
    }

    // ── Core Logic ─────────────────────────────────────────────────────────────

    private void UpdateHolePunch()
    {
        // Get the panel's four corners in screen space
        Vector3[] worldCorners = new Vector3[4];
        panelRect.GetWorldCorners(worldCorners);

        Camera cam = renderCamera != null ? renderCamera : Camera.main;
        if (cam == null) return;

        // Convert world corners to screen pixels
        Vector2 screenMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 screenMax = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < 4; i++)
        {
            Vector3 sp = cam.WorldToScreenPoint(worldCorners[i]);
            screenMin.x = Mathf.Min(screenMin.x, sp.x);
            screenMin.y = Mathf.Min(screenMin.y, sp.y);
            screenMax.x = Mathf.Max(screenMax.x, sp.x);
            screenMax.y = Mathf.Max(screenMax.y, sp.y);
        }

        // Unity screen coordinates: (0,0) = bottom-left
        // Win32 screen coordinates: (0,0) = top-left
        // We need to get Unity's window rect to convert
        GetWindowRect(unityHwnd, out RECT unityRect);
        int unityW = unityRect.Right - unityRect.Left;
        int unityH = unityRect.Bottom - unityRect.Top;

        // Account for title bar / border offset
        RECT clientRect;
        GetClientRect(unityHwnd, out clientRect);
        int clientW = clientRect.Right - clientRect.Left;
        int clientH = clientRect.Bottom - clientRect.Top;
        int borderX = (unityW - clientW) / 2;
        int borderTop = unityH - clientH - borderX; // title bar height

        // Convert Unity screen coords to Win32 client-area coords
        // Unity: bottom-left origin. Win32: top-left origin.
        int holeLeft   = Mathf.RoundToInt(screenMin.x) + borderX;
        int holeRight  = Mathf.RoundToInt(screenMax.x) + borderX;
        int holeTop    = (clientH - Mathf.RoundToInt(screenMax.y)) + borderTop;
        int holeBottom = (clientH - Mathf.RoundToInt(screenMin.y)) + borderTop;

        // Clamp to valid range
        holeLeft   = Mathf.Max(0, holeLeft);
        holeTop    = Mathf.Max(0, holeTop);
        holeRight  = Mathf.Min(unityW, holeRight);
        holeBottom = Mathf.Min(unityH, holeBottom);

        if (holeRight <= holeLeft || holeBottom <= holeTop) return;

        // 1) Cut the hole in Unity's window
        IntPtr fullRgn = CreateRectRgn(0, 0, unityW, unityH);
        IntPtr holeRgn = CreateRectRgn(holeLeft, holeTop, holeRight, holeBottom);
        CombineRgn(fullRgn, fullRgn, holeRgn, RGN_DIFF);
        SetWindowRgn(unityHwnd, fullRgn, true);
        // Note: Windows takes ownership of fullRgn after SetWindowRgn, don't delete it
        DeleteObject(holeRgn);

        // 2) Position the app window behind the hole
        int appX = unityRect.Left + holeLeft;
        int appY = unityRect.Top + holeTop;
        int appW = holeRight - holeLeft;
        int appH = holeBottom - holeTop;

        SetWindowPos(appHwnd, unityHwnd, // Place directly behind Unity
            appX, appY, appW, appH,
            SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    // ── App Discovery ──────────────────────────────────────────────────────────

    private IEnumerator FindOrLaunchApp()
    {
        // First, try to find an already-running instance
        appHwnd = FindWindowByProcessName(targetProcessName);

        if (appHwnd == IntPtr.Zero)
        {
            // Launch the app
            try
            {
                UnityEngine.Debug.Log($"[HolePunchController] Launching '{executablePath}'...");
                appProcess = Process.Start(executablePath);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[HolePunchController] Failed to launch: {e.Message}");
                isActive = false;
                yield break;
            }

            // Wait for the window to appear (up to 10 seconds)
            float elapsed = 0f;
            float timeout = 10f;

            while (elapsed < timeout)
            {
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;

                appHwnd = FindWindowByProcessName(targetProcessName);
                if (appHwnd != IntPtr.Zero) break;
            }

            if (appHwnd == IntPtr.Zero)
            {
                UnityEngine.Debug.LogError($"[HolePunchController] Timed out waiting for '{targetProcessName}' window.");
                isActive = false;
                yield break;
            }
        }

        UnityEngine.Debug.Log($"[HolePunchController] Found '{targetProcessName}' window: {appHwnd}");

        // Strip the window chrome (title bar, borders) for a cleaner embed
        StripWindowChrome(appHwnd);

        // Show the window without stealing focus from Unity
        ShowWindow(appHwnd, SW_SHOWNOACTIVATE);

        // Refocus Unity
        SetForegroundWindow(unityHwnd);
    }

    private void StripWindowChrome(IntPtr hwnd)
    {
        uint style = GetWindowLong(hwnd, GWL_STYLE);
        style &= ~WS_CAPTION;
        style &= ~WS_THICKFRAME;
        style &= ~WS_BORDER;
        SetWindowLong(hwnd, GWL_STYLE, style);

        // Force Windows to redraw the frame
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_NOACTIVATE);
    }

    private IntPtr FindWindowByProcessName(string procName)
    {
        Process[] procs = Process.GetProcessesByName(procName);
        foreach (var proc in procs)
        {
            try
            {
                IntPtr hwnd = FindVisibleWindow(proc.Id);
                if (hwnd != IntPtr.Zero) return hwnd;
            }
            catch { /* process may have exited */ }
        }
        return IntPtr.Zero;
    }

    private IntPtr FindVisibleWindow(int processId)
    {
        IntPtr found = IntPtr.Zero;
        uint targetPid = (uint)processId;

        EnumWindows((hWnd, lParam) =>
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == targetPid && IsWindowVisible(hWnd) && GetWindowTextLength(hWnd) > 0)
            {
                found = hWnd;
                return false; // stop
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }
}
