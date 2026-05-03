using UnityEngine;
using UnityEngine.Scripting;

/// Macro action that opens the Settings overlay.
public class SettingsAction : IMacroAction
{
    [Preserve]
    static SettingsAction() => MacroActionFactory.Register(MacroActionType.Settings, () => new SettingsAction());

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

