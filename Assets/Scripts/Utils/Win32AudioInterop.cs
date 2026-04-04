using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// Provides static helpers to get/set the Windows system master volume
/// using the Core Audio COM API (IAudioEndpointVolume).
/// Works on Windows Vista+ without any third-party packages.
public static class Win32AudioInterop
{
    // ── COM class & interface GUIDs (Microsoft-defined, universal) ─────────
    private static readonly Guid CLSID_MMDeviceEnumerator =
        new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");

    private static readonly Guid IID_IMMDeviceEnumerator =
        new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");

    private static readonly Guid IID_IAudioEndpointVolume =
        new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");

    // ── Enums ──────────────────────────────────────────────────────────────
    private enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
    private enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

    // ── COM Interfaces ─────────────────────────────────────────────────────
    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out uint channelCount);
        int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        int SetMasterVolumeLevel(float levelDB, ref Guid eventContext);
        int GetMasterVolumeLevel(out float levelDB);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
        int SetChannelVolumeLevel(uint channel, float levelDB, ref Guid eventContext);
        int GetChannelVolumeLevel(uint channel, out float levelDB);
        int GetChannelVolumeLevelScalar(uint channel, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        int GetVolumeStepInfo(out uint step, out uint stepCount);
        int VolumeStepUp(ref Guid eventContext);
        int VolumeStepDown(ref Guid eventContext);
        int QueryHardwareSupport(out uint hardwareSupportMask);
        int GetVolumeRange(out float minDB, out float maxDB, out float incDB);
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private static IAudioEndpointVolume GetEndpointVolume()
    {
        var type = Type.GetTypeFromCLSID(CLSID_MMDeviceEnumerator);
        if (type == null)
            throw new PlatformNotSupportedException("MMDeviceEnumerator COM class not found.");

        var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(type);
        enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);

        var iid = IID_IAudioEndpointVolume;
        device.Activate(ref iid, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out var iface);
        return (IAudioEndpointVolume)iface;
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// Returns the current system master volume as a float in [0, 1].
    public static float GetVolume()
    {
        try
        {
            var vol = GetEndpointVolume();
            vol.GetMasterVolumeLevelScalar(out float level);
            return level;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Win32AudioInterop] GetVolume failed: {e.Message}");
            return 0.5f;
        }
    }

    /// Sets the system master volume. Value should be in [0, 1].
    public static void SetVolume(float level)
    {
        try
        {
            level = Mathf.Clamp01(level);
            var vol = GetEndpointVolume();
            var guid = Guid.Empty;
            vol.SetMasterVolumeLevelScalar(level, ref guid);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Win32AudioInterop] SetVolume failed: {e.Message}");
        }
    }

    /// Returns true if the system audio is muted.
    public static bool GetMute()
    {
        try
        {
            var vol = GetEndpointVolume();
            vol.GetMute(out bool muted);
            return muted;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Win32AudioInterop] GetMute failed: {e.Message}");
            return false;
        }
    }

    /// Sets the system mute state.
    public static void SetMute(bool mute)
    {
        try
        {
            var vol = GetEndpointVolume();
            var guid = Guid.Empty;
            vol.SetMute(mute, ref guid);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Win32AudioInterop] SetMute failed: {e.Message}");
        }
    }
}
