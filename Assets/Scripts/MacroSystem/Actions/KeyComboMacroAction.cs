using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/// <summary>
/// Base class for macro actions that send a keyboard shortcut.
/// Handles focus-switching to the window behind Unity and a configurable
/// delay so the OS has time to complete the focus change before keystrokes
/// are injected.
///
/// Subclasses only need to declare:
///   - ActionId / DisplayName  (identity)
///   - Modifiers               (e.g. Ctrl, Alt, Win — can be empty)
///   - MainKey                 (the action key: F, Z, Left, PrtScrn …)
///   - FocusBehind             (override to false for Win+L etc.)
/// </summary>
public abstract class KeyComboMacroAction : IMacroAction
{
    // ── Identity (override in each subclass) ────────────────────────
    public abstract string ActionId { get; }
    public abstract string DisplayName { get; }

    // ── Key definition ──────────────────────────────────────────────
    /// Modifier keys pressed before the main key (Ctrl, Alt, Win, Shift …).
    /// Return an empty array for single-key actions like PageUp/PageDown.
    protected abstract byte[] Modifiers { get; }

    /// The primary action key.
    protected abstract byte MainKey { get; }

    /// Set to false for shortcuts that should NOT switch focus first
    /// (e.g. Win+L lock screen).
    protected virtual bool FocusBehind => true;

    // ── Shared plumbing ─────────────────────────────────────────────
    /// How long (ms) to wait after calling FocusWindowBehind() before
    /// sending keystrokes. Gives the OS time to actually switch focus.
    private const int FocusDelayMs = 200;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    // ── Common virtual key constants for subclasses ─────────────────
    protected const byte VK_CONTROL  = 0x11;
    protected const byte VK_MENU     = 0x12;  // Alt
    protected const byte VK_SHIFT    = 0x10;
    protected const byte VK_LWIN     = 0x5B;

    public void Execute()
    {
#if !UNITY_EDITOR
        if (FocusBehind)
            WindowManager.FocusWindowBehind();

        // Fire keystrokes on a background thread so the main thread
        // isn't blocked during the focus-delay.
        ThreadPool.QueueUserWorkItem(_ =>
        {
            if (FocusBehind)
                Thread.Sleep(FocusDelayMs);

            byte[] mods = Modifiers;
            byte   main = MainKey;

            // Press modifiers in order
            for (int i = 0; i < mods.Length; i++)
                keybd_event(mods[i], 0, 0, UIntPtr.Zero);

            // Press & release main key
            keybd_event(main, 0, 0,                UIntPtr.Zero);
            keybd_event(main, 0, KEYEVENTF_KEYUP,  UIntPtr.Zero);

            // Release modifiers in reverse order
            for (int i = mods.Length - 1; i >= 0; i--)
                keybd_event(mods[i], 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        });
#endif
        Debug.Log($"[MacroButton] Executing: {ActionId}");
    }
}
