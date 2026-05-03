using UnityEngine;
using UnityEngine.Scripting;

/// Macro action that opens the App Cycler dock for cycling through desktop windows.
public class AppCyclerAction : IMacroAction
{
    [Preserve]
    static AppCyclerAction() => MacroActionFactory.Register(MacroActionType.AppCycler, () => new AppCyclerAction());

    public string ActionId => "app_cycler";
    public string DisplayName => "Switch Apps";

    public void Execute()
    {
        var controller = Object.FindFirstObjectByType<AppCyclerController>();
        if (controller != null)
        {
            controller.Open();
        }
        else
        {
            RobitLogger.LogWarning("[AppCyclerAction] No AppCyclerController found in scene.");
        }
    }
}

