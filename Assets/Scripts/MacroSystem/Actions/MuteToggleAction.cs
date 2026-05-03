using UnityEngine;

public class MuteToggleAction : IMacroAction
{
    public string ActionId => "mute_toggle";
    public string DisplayName => "Mute";

    public void Execute()
    {
        bool muted = Win32AudioInterop.GetMute();
        Win32AudioInterop.SetMute(!muted);
        RobitLogger.Log($"[MacroButton] Executing: mute_toggle → {(!muted ? "muted" : "unmuted")}");
    }
}

