using UnityEngine;

/// Macro action that opens the Settings overlay.
public class SettingsAction : IMacroAction
{
    public string ActionId => "settings";
    public string DisplayName => "Settings";

    public void Execute()
    {
        var controller = Object.FindFirstObjectByType<SettingsOverlayController>();
        if (controller != null)
        {
            controller.Open();
        }
        else
        {
            RobitLogger.LogWarning("[SettingsAction] No SettingsOverlayController found in scene.");
        }
    }
}

