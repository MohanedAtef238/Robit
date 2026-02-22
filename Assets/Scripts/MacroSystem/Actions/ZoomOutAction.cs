using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class ZoomOutAction : IMacroAction
{
    public string ActionId => "zoom_out";
    public string DisplayName => "Zoom Out";

    private const byte VK_CONTROL = 0x11;
    private const byte VK_OEM_MINUS = 0xBD;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern void keybd_event(
        byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public void Execute()
    {
#if !UNITY_EDITOR
        WindowManager.FocusWindowBehind();

        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_OEM_MINUS, 0, 0, UIntPtr.Zero);
        keybd_event(VK_OEM_MINUS, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
#endif
        Debug.Log("[MacroButton] Executing: zoom_out");
    }
}
