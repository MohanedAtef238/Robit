using UnityEngine;
using UnityEngine.Scripting;

/// Macro action that toggles the App Launcher carousel.
/// Mirrors HomePageAction — finds AppLauncherUIToolkit and calls Toggle().
public class AppCyclerAction : IMacroAction
{
    [Preserve]
    static AppCyclerAction() => MacroActionFactory.Register(MacroActionType.AppCycler, () => new AppCyclerAction());

    public string ActionId    => "app_cycler";
    public string DisplayName => "Switch Apps";

    public void Execute()
    {
        var launcher = Object.FindFirstObjectByType<AppLauncherUIToolkit>();
        if (launcher != null)
            launcher.Toggle();
        else
            RobitLogger.LogWarning("[AppCyclerAction] AppLauncherUIToolkit not found in scene.");
    }
}
