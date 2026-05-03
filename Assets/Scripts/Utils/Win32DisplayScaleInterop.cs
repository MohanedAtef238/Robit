using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// Uses the undocumented DisplayConfigGetDeviceInfo / DisplayConfigSetDeviceInfo API 
/// the same mechanism Windows Settings uses internally so changes apply immediately without requiring a sign-out or restart.
/// Assumes a single active display. Supported values: 100 / 125 / 150 / 200 %.
public static class Win32DisplayScaleInterop
{
    // ── P/Invoke declarations ─────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(
        uint     flags,
        out uint numPathArrayElements,
        out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint                            flags,
        ref uint                        numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint                        numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        IntPtr                          currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(
        ref DISPLAYCONFIG_SOURCE_DPI_SCALE_GET requestPacket);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigSetDeviceInfo(
        ref DISPLAYCONFIG_SOURCE_DPI_SCALE_SET requestPacket);

    // Returns the DPI the system is currently running at.
    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    // ── Win32 constants ───────────────────────────────────────────────────────

    private const uint QDC_ONLY_ACTIVE_PATHS                   = 0x00000002;
    private const int  DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE = -3;
    private const int  DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE = -4;
    private const int  ERROR_SUCCESS                           = 0;

    // ── Win32 structures ──────────────────────────────────────────────────────

    // LUID  (8 bytes)
    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;  // 4
        public int  HighPart; // 4
    }

    // DISPLAYCONFIG_PATH_SOURCE_INFO  (20 bytes)
    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;   // 8
        public uint id;          // 4
        public uint modeInfoIdx; // 4  (union — first variant)
        public uint statusFlags; // 4
    }

    // DISPLAYCONFIG_PATH_TARGET_INFO  (48 bytes)
    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;              // 8
        public uint id;                     // 4
        public uint modeInfoIdx;            // 4
        public int  outputTechnology;       // 4
        public int  rotation;               // 4
        public int  scaling;                // 4
        public uint refreshRateNumerator;   // 4
        public uint refreshRateDenominator; // 4
        public int  scanLineOrdering;       // 4
        public int  targetAvailable;        // 4  (BOOL)
        public uint statusFlags;            // 4
    }

    // DISPLAYCONFIG_PATH_INFO  (72 bytes)
    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo; // 20
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo; // 48
        public uint                           flags;      //  4
    }

    // DISPLAYCONFIG_MODE_INFO  (64 bytes)
    // We never read the union contents, so 12 padding uints cover the 48-byte union.
    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_MODE_INFO
    {
        public int  infoType;  //  4
        public uint id;        //  4
        public LUID adapterId; //  8
        // 48-byte union padded with 12 × uint
        private uint _u0,  _u1,  _u2,  _u3,  _u4,  _u5,
                     _u6,  _u7,  _u8,  _u9,  _u10, _u11;
    }

    // DISPLAYCONFIG_DEVICE_INFO_HEADER  (20 bytes)
    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public int  type;      //  4
        public int  size;      //  4
        public LUID adapterId; //  8
        public uint id;        //  4
    }

    // DISPLAYCONFIG_SOURCE_DPI_SCALE_GET  (32 bytes)
    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_SOURCE_DPI_SCALE_GET
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header; // 20
        public int minScaleRel;                         //  4
        public int curScaleRel;                         //  4
        public int maxScaleRel;                         //  4
    }

    // DISPLAYCONFIG_SOURCE_DPI_SCALE_SET  (24 bytes)
    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_SOURCE_DPI_SCALE_SET
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header; // 20
        public int scaleRel;                            //  4
    }

    // ── DPI step table ────────────────────────────────────────────────────────

    // Windows' internal DPI percentage steps (matches what the Settings UI exposes).
    private static readonly int[] DpiStepPercents =
        { 100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500 };

    // Corresponding values returned by GetDpiForSystem() for each step.
    private static readonly int[] DpiForStep =
        { 96, 120, 144, 168, 192, 216, 240, 288, 336, 384, 432, 480 };

    // The four values our picker UI supports.
    private static readonly int[] SupportedPercents = { 100, 125, 150, 200 };

    // Cached recommended step index — computed once (before any in-process scale
    // change) while GetDpiForSystem() still reflects the actual system DPI.
    // After DisplayConfigSetDeviceInfo is called the process DPI goes stale and
    // GetDpiForSystem() keeps returning the launch-time value forever, so we must
    // never recompute this from the live DPI after the first change.
    private static int _recommendedStepIdx = int.MinValue;

    // ── Public API ────────────────────────────────────────────────────────────

    /// Returns the current system DPI scale as a percentage snapped to
    /// 100 / 125 / 150 / 200. Returns 100 on failure.
    public static int GetScalePercent()
    {
#if !(UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
        RobitLogger.LogWarning("[Win32DisplayScaleInterop] Only supported on Windows.");
        return 100;
#else
        try
        {
            // Read the live curScaleRel from the API — GetDpiForSystem() is stale
            // after any in-process scale change, so we cannot rely on it here.
            if (!TryGetActiveSource(out LUID adapterId, out uint sourceId))
                return 100;

            var getPacket = new DISPLAYCONFIG_SOURCE_DPI_SCALE_GET
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type      = DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE,
                    size      = Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DPI_SCALE_GET>(),
                    adapterId = adapterId,
                    id        = sourceId,
                }
            };
            if (DisplayConfigGetDeviceInfo(ref getPacket) != ERROR_SUCCESS)
                return 100;

            int stepIdx = EnsureRecommendedStepIdx(getPacket) + getPacket.curScaleRel;
            if (stepIdx >= 0 && stepIdx < DpiStepPercents.Length)
                return SnapToSupported(DpiStepPercents[stepIdx]);
        }
        catch (Exception ex)
        {
            RobitLogger.LogWarning($"[Win32DisplayScaleInterop] GetScalePercent: {ex.Message}");
        }
        return 100;
#endif
    }

    /// Applies the requested DPI scale immediately (no sign-out needed).
    /// percent must be one of: 100, 125, 150, 200.
    /// Returns true on success.
    public static bool SetScalePercent(int percent)
    {
#if !(UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
        RobitLogger.LogWarning("[Win32DisplayScaleInterop] Only supported on Windows.");
        return false;
#else
        if (Array.IndexOf(SupportedPercents, percent) < 0)
        {
            RobitLogger.LogWarning($"[Win32DisplayScaleInterop] Unsupported scale: {percent}%");
            return false;
        }

        try
        {
            // 1. Enumerate active display paths and grab the first source.
            if (!TryGetActiveSource(out LUID adapterId, out uint sourceId))
            {
                RobitLogger.LogError("[Win32DisplayScaleInterop] No active display source found.");
                return false;
            }

            // 2. Read the current relative DPI state so we can derive the
            //    display's recommended step index.
            var getPacket = new DISPLAYCONFIG_SOURCE_DPI_SCALE_GET
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type      = DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE,
                    size      = Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DPI_SCALE_GET>(),
                    adapterId = adapterId,
                    id        = sourceId,
                }
            };
            int rc = DisplayConfigGetDeviceInfo(ref getPacket);
            if (rc != ERROR_SUCCESS)
            {
                RobitLogger.LogError($"[Win32DisplayScaleInterop] GET DPI scale failed (error {rc}).");
                return false;
            }

            // 3. Get the cached recommended step index.
            //    After any in-process scale change GetDpiForSystem() is stale, so we
            //    compute this once (before the first change) and reuse it every call.
            int recommendedStepIdx = EnsureRecommendedStepIdx(getPacket);

            // 4. Compute scaleRel for the requested percentage and clamp to
            //    the range the display actually supports.
            int targetStepIdx = IndexOfPercent(percent);
            if (targetStepIdx < 0)
            {
                RobitLogger.LogError($"[Win32DisplayScaleInterop] {percent}% not found in DPI step table.");
                return false;
            }
            int scaleRel = targetStepIdx - recommendedStepIdx;
            scaleRel = Math.Max(getPacket.minScaleRel, Math.Min(getPacket.maxScaleRel, scaleRel));

            // 5. Apply — takes effect immediately, no restart required.
            var setPacket = new DISPLAYCONFIG_SOURCE_DPI_SCALE_SET
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type      = DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE,
                    size      = Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DPI_SCALE_SET>(),
                    adapterId = adapterId,
                    id        = sourceId,
                },
                scaleRel = scaleRel,
            };
            int setRc = DisplayConfigSetDeviceInfo(ref setPacket);
            if (setRc != ERROR_SUCCESS)
            {
                RobitLogger.LogError($"[Win32DisplayScaleInterop] SET DPI scale failed (error {setRc}).");
                return false;
            }

            RobitLogger.Log($"[Win32DisplayScaleInterop] Scale set to {percent}% (scaleRel={scaleRel}).");
            return true;
        }
        catch (Exception ex)
        {
            RobitLogger.LogError($"[Win32DisplayScaleInterop] SetScalePercent: {ex.Message}");
            return false;
        }
#endif
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static bool TryGetActiveSource(out LUID adapterId, out uint sourceId)
    {
        adapterId = default;
        sourceId  = 0;

        int rc = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS,
                                             out uint numPaths,
                                             out uint numModes);
        if (rc != ERROR_SUCCESS || numPaths == 0) return false;

        var paths = new DISPLAYCONFIG_PATH_INFO[numPaths];
        var modes = new DISPLAYCONFIG_MODE_INFO[numModes];
        rc = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS,
                                ref numPaths, paths,
                                ref numModes, modes,
                                IntPtr.Zero);
        if (rc != ERROR_SUCCESS) return false;

        adapterId = paths[0].sourceInfo.adapterId;
        sourceId  = paths[0].sourceInfo.id;
        return true;
    }

    private static int IndexOfDpi(int dpi)
    {
        for (int i = 0; i < DpiForStep.Length; i++)
            if (DpiForStep[i] == dpi) return i;
        return -1;
    }

    private static int IndexOfPercent(int percent)
    {
        for (int i = 0; i < DpiStepPercents.Length; i++)
            if (DpiStepPercents[i] == percent) return i;
        return -1;
    }

    /// Returns and caches the display's recommended DPI step index.
    /// Must be called before any scale change while GetDpiForSystem() is still accurate.
    private static int EnsureRecommendedStepIdx(DISPLAYCONFIG_SOURCE_DPI_SCALE_GET getPacket)
    {
        if (_recommendedStepIdx != int.MinValue) return _recommendedStepIdx;
        // First call only — process DPI has not been changed yet, so this is reliable.
        uint liveDpi = GetDpiForSystem();
        int currentStepIdx = IndexOfDpi((int)liveDpi);
        if (currentStepIdx < 0) currentStepIdx = 0;
        _recommendedStepIdx = currentStepIdx - getPacket.curScaleRel;
        return _recommendedStepIdx;
    }

    private static int SnapToSupported(int percent)
    {
        int best = SupportedPercents[0];
        int bestDist = Math.Abs(percent - best);
        for (int i = 1; i < SupportedPercents.Length; i++)
        {
            int dist = Math.Abs(percent - SupportedPercents[i]);
            if (dist < bestDist) { bestDist = dist; best = SupportedPercents[i]; }
        }
        return best;
    }
}

