using System.Runtime.InteropServices;
using UnityEngine;

public class PageDownAction : IMacroAction
{
    public string ActionId => "page_down";
    public string DisplayName => "Page Down";

    private const byte VK_NEXT = 0x22; // Page Down key
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, System.UIntPtr dwExtraInfo);

    public void Execute()
    {
#if !UNITY_EDITOR
        WindowManager.FocusWindowBehind();

        keybd_event(VK_NEXT, 0, 0, System.UIntPtr.Zero);
        keybd_event(VK_NEXT, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
#endif
        Debug.Log("[MacroButton] Executing: page_down");
    }
}
