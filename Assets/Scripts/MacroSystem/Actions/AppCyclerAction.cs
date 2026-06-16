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
        RobitLogger.Log($"[{nameof(AppCyclerAction)}] Executing AppCycler Action via ViewCoordinator.");
        
        if (ViewCoordinator.Instance != null)
        {
            ViewCoordinator.Instance.ToggleAppCycler();
        }
        else
        {
            RobitLogger.LogWarning($"[{nameof(AppCyclerAction)}] ViewCoordinator not found in scene!");
        }
    }
}
