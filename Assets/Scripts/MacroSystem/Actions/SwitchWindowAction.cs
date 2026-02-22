using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// Sends Alt+Tab to the OS to switch the foreground window.
public class SwitchWindowAction : IMacroAction
{
    public string ActionId => "switch_window";
    public string DisplayName => "Switch Window";

    private const byte VK_MENU = 0x12;  // Alt key
    private const byte VK_TAB = 0x09;

    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern void keybd_event( byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public void Execute()
    {
#if !UNITY_EDITOR
        WindowManager.FocusWindowBehind();

        keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
        keybd_event(VK_TAB, 0, 0, UIntPtr.Zero);
        keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
#endif
        Debug.Log("[MacroButton] Executing: switch_window");
    }
}
