using System;
using System.Diagnostics;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using static Win32Interop;

/// <summary>
/// Manages the "Hole-Punch" overlay for external applications.
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

    private IntPtr unityHwnd = IntPtr.Zero;
    private IntPtr appHwnd   = IntPtr.Zero;
    private bool isActive    = false;
    private Process appProcess = null;
    private Coroutine findWindowCoroutine = null;

    public void Activate()
    {
        if (isActive) return;

        #if UNITY_EDITOR
        UnityEngine.Debug.Log("[HolePunchController] Hole-punch only works in builds. Skipping.");
        return;
        #endif

        #pragma warning disable CS0162
        isActive = true;

        unityHwnd = GetActiveWindow();
        if (unityHwnd == IntPtr.Zero)
        {
            UnityEngine.Debug.LogError("[HolePunchController] Could not get Unity window handle.");
            isActive = false;
            return;
        }

        SetWindowPos(unityHwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        findWindowCoroutine = StartCoroutine(FindOrLaunchApp());
        #pragma warning restore CS0162
    }

    public void Deactivate()
    {
        if (!isActive) return;
        isActive = false;

        if (findWindowCoroutine != null)
        {
            StopCoroutine(findWindowCoroutine);
            findWindowCoroutine = null;
        }

        if (unityHwnd != IntPtr.Zero)
        {
            SetWindowRgn(unityHwnd, IntPtr.Zero, true);

            SetWindowPos(unityHwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        if (appHwnd != IntPtr.Zero)
        {
            ShowWindow(appHwnd, SW_MINIMIZE);
        }
    }

    /// <summary>
    /// Forcibly closes the tracked target application process.
    /// </summary>
    public void CloseTargetApp()
    {
        if (appProcess != null && !appProcess.HasExited)
        {
            try
            {
                appProcess.CloseMainWindow();
                appProcess.Dispose();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[HolePunchController] Failed to close process gracefully: {e.Message}");
            }
            finally
            {
                appProcess = null;
            }
        }
        
        appHwnd = IntPtr.Zero;
        isActive = false;
    }

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

    private void UpdateHolePunch()
    {
        Vector3[] worldCorners = new Vector3[4];
        panelRect.GetWorldCorners(worldCorners);

        Camera cam = renderCamera != null ? renderCamera : Camera.main;
        if (cam == null) return;

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

        GetWindowRect(unityHwnd, out RECT unityRect);
        int unityW = unityRect.Right - unityRect.Left;
        int unityH = unityRect.Bottom - unityRect.Top;

        RECT clientRect;
        GetClientRect(unityHwnd, out clientRect);
        int clientW = clientRect.Right - clientRect.Left;
        int clientH = clientRect.Bottom - clientRect.Top;
        int borderX = (unityW - clientW) / 2;
        int borderTop = unityH - clientH - borderX; 

        int holeLeft   = Mathf.RoundToInt(screenMin.x) + borderX;
        int holeRight  = Mathf.RoundToInt(screenMax.x) + borderX;
        int holeTop    = (clientH - Mathf.RoundToInt(screenMax.y)) + borderTop;
        int holeBottom = (clientH - Mathf.RoundToInt(screenMin.y)) + borderTop;

        holeLeft   = Mathf.Max(0, holeLeft);
        holeTop    = Mathf.Max(0, holeTop);
        holeRight  = Mathf.Min(unityW, holeRight);
        holeBottom = Mathf.Min(unityH, holeBottom);

        if (holeRight <= holeLeft || holeBottom <= holeTop) return;

        IntPtr fullRgn = CreateRectRgn(0, 0, unityW, unityH);
        IntPtr holeRgn = CreateRectRgn(holeLeft, holeTop, holeRight, holeBottom);
        CombineRgn(fullRgn, fullRgn, holeRgn, RGN_DIFF);
        SetWindowRgn(unityHwnd, fullRgn, true);
        DeleteObject(holeRgn);

        int appX = unityRect.Left + holeLeft;
        int appY = unityRect.Top + holeTop;
        int appW = holeRight - holeLeft;
        int appH = holeBottom - holeTop;

        SetWindowPos(appHwnd, unityHwnd, 
            appX, appY, appW, appH,
            SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private IEnumerator FindOrLaunchApp()
    {
        appHwnd = FindWindowByProcessName(targetProcessName);

        if (appHwnd == IntPtr.Zero)
        {
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

        StripWindowChrome(appHwnd);
        ShowWindow(appHwnd, SW_SHOWNOACTIVATE);
        SetForegroundWindow(unityHwnd);
    }

    private void StripWindowChrome(IntPtr hwnd)
    {
        uint style = GetWindowLong(hwnd, GWL_STYLE);
        style &= ~WS_CAPTION;
        style &= ~WS_THICKFRAME;
        style &= ~WS_BORDER;
        SetWindowLong(hwnd, GWL_STYLE, style);

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
            catch { }
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
                return false; 
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }
}
