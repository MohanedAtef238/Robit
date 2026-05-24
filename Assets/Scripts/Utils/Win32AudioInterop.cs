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

    private static IMMDeviceEnumerator _enumerator;
    private static IAudioEndpointVolume   _volume;
    private static readonly object        _comLock = new();

    private const int AUDCLNT_E_DEVICE_INVALIDATED = unchecked((int)0x88890004);

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
        int SetMasterVolumeLevel(float levelDB, ref Guid eventContext);
        int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        int GetMasterVolumeLevel(out float levelDB);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channel, float levelDB, ref Guid eventContext);
        int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
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

    private static IAudioEndpointVolume GetOrAcquireEndpointVolume()
    {
        if (_volume != null) return _volume;

        var type = Type.GetTypeFromCLSID(CLSID_MMDeviceEnumerator);
        if (type == null) throw new PlatformNotSupportedException("MMDeviceEnumerator not found.");

        _enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(type);
        _enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);

        var iid = IID_IAudioEndpointVolume;
        device.Activate(ref iid, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out var iface);
        _volume = (IAudioEndpointVolume)iface;

        // Clean up the intermediate device; Activate adds a reference to the resulting interface.
        Marshal.ReleaseComObject(device);
        return _volume;
    }

    private static void HandleStaleDeviceUnderLock()
    {
        if (_volume != null) Marshal.ReleaseComObject(_volume);
        if (_enumerator != null) Marshal.ReleaseComObject(_enumerator);
        _volume = null;
        _enumerator = null;
    }

    public static void Shutdown()
    {
        lock (_comLock)
        {
            HandleStaleDeviceUnderLock();
        }
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// Returns the current system master volume as a float in [0, 1].
    public static float GetVolume()
    {
        lock (_comLock)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var vol = GetOrAcquireEndpointVolume();
                    int hr = vol.GetMasterVolumeLevelScalar(out float level);
                    if (hr != AUDCLNT_E_DEVICE_INVALIDATED)
                        return level;

                    HandleStaleDeviceUnderLock();
                }
                catch (COMException e)
                {
                    RobitLogger.LogWarning($"[Win32AudioInterop] GetVolume COM error: {e.Message}");
                    HandleStaleDeviceUnderLock();
                }
                catch (Exception e)
                {
                    RobitLogger.LogWarning($"[Win32AudioInterop] GetVolume failed: {e.Message}");
                    return 0.5f;
                }
            }

            RobitLogger.LogWarning("[Win32AudioInterop] GetVolume: device still invalid after retry.");
            return 0.5f;
        }
    }

    /// Sets the system master volume. volume should be in [0, 1].
    public static void SetVolume(float volume)
    {
        lock (_comLock)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var vol = GetOrAcquireEndpointVolume();
                    var guid = Guid.Empty;
                    int hr = vol.SetMasterVolumeLevelScalar(Mathf.Clamp01(volume), ref guid);
                    if (hr != AUDCLNT_E_DEVICE_INVALIDATED)
                        return;

                    HandleStaleDeviceUnderLock();
                }
                catch (COMException e)
                {
                    RobitLogger.LogWarning($"[Win32AudioInterop] SetVolume COM error: {e.Message}");
                    HandleStaleDeviceUnderLock();
                }
                catch (Exception e)
                {
                    RobitLogger.LogWarning($"[Win32AudioInterop] SetVolume failed: {e.Message}");
                    return;
                }
            }
            RobitLogger.LogWarning("[Win32AudioInterop] SetVolume: device still invalid after retry.");
        }
    }

    /// Returns true if the system master volume is muted.
    public static bool GetMute()
    {
        lock (_comLock)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var vol = GetOrAcquireEndpointVolume();
                    int hr = vol.GetMute(out bool mute);
                    if (hr != AUDCLNT_E_DEVICE_INVALIDATED)
                        return mute;

                    HandleStaleDeviceUnderLock();
                }
                catch (COMException e)
                {
                    RobitLogger.LogWarning($"[Win32AudioInterop] GetMute COM error: {e.Message}");
                    HandleStaleDeviceUnderLock();
                }
                catch (Exception e)
                {
                    RobitLogger.LogWarning($"[Win32AudioInterop] GetMute failed: {e.Message}");
                    return false;
                }
            }

            RobitLogger.LogWarning("[Win32AudioInterop] GetMute: device still invalid after retry.");
            return false;
        }
    }

    /// Sets the system master volume mute state.
    public static void SetMute(bool mute)
    {
        lock (_comLock)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var vol = GetOrAcquireEndpointVolume();
                    var guid = Guid.Empty;
                    int hr = vol.SetMute(mute, ref guid);
                    if (hr != AUDCLNT_E_DEVICE_INVALIDATED)
                        return;

                    HandleStaleDeviceUnderLock();
                }
                catch (COMException e)
                {
                    RobitLogger.LogWarning($"[Win32AudioInterop] SetMute COM error: {e.Message}");
                    HandleStaleDeviceUnderLock();
                }
                catch (Exception e)
                {
                    RobitLogger.LogWarning($"[Win32AudioInterop] SetMute failed: {e.Message}");
                    return;
                }
            }
            RobitLogger.LogWarning("[Win32AudioInterop] SetMute: device still invalid after retry.");
        }
    }
}
