using UnityEngine;
using UnityEngine.UIElements;
using System.Diagnostics;
using System.Runtime.InteropServices;

public class InputToggleController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private Button mickButton;
    private Button keyboardButton;

    // Importing the user32.dll to simulate key presses reliably
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

    private const byte VK_LWIN = 0x5B;
    private const byte VK_CONTROL = 0x11;
    private const byte VK_H = 0x48;
    private const byte VK_O = 0x4F;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        mickButton = root.Q<Button>("mick");
        keyboardButton = root.Q<Button>("keyboard");

        // Set initial sizes
        mickButton.AddToClassList("btn-focus");
        keyboardButton.AddToClassList("btn-sub");

        mickButton.clicked += OnMickClicked;
        keyboardButton.clicked += OnKeyboardClicked;
    }

    private void OnMickClicked()
    {
        SwapSizes(isMickFocus: true);
        TriggerWindowsVoice();
    }

    private void OnKeyboardClicked()
    {
        SwapSizes(isMickFocus: false);
        TriggerOnScreenKeyboard();
    }

    private void SwapSizes(bool isMickFocus)
    {
        if (isMickFocus)
        {
            mickButton.RemoveFromClassList("btn-sub");
            mickButton.AddToClassList("btn-focus");

            keyboardButton.RemoveFromClassList("btn-focus");
            keyboardButton.AddToClassList("btn-sub");
        }
        else
        {
            keyboardButton.RemoveFromClassList("btn-sub");
            keyboardButton.AddToClassList("btn-focus");

            mickButton.RemoveFromClassList("btn-focus");
            mickButton.AddToClassList("btn-sub");
        }
    }

    private void TriggerWindowsVoice()
    {
        UnityEngine.Debug.Log("Triggering Windows Voice-to-Text (Win+H)");
        keybd_event(VK_LWIN, 0, 0, 0);
        keybd_event(VK_H, 0, 0, 0);
        keybd_event(VK_H, 0, KEYEVENTF_KEYUP, 0);
        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
    }

    private void TriggerOnScreenKeyboard()
    {
        UnityEngine.Debug.Log("Triggering On-Screen Keyboard (Win+Ctrl+O)");
        keybd_event(VK_LWIN, 0, 0, 0);
        keybd_event(VK_CONTROL, 0, 0, 0);
        keybd_event(VK_O, 0, 0, 0);
        keybd_event(VK_O, 0, KEYEVENTF_KEYUP, 0);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
        keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, 0);
    }
}