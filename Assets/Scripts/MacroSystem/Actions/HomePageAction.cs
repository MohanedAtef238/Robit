using UnityEngine;
using UnityEngine.Scripting;

/// Macro action that opens the Home Page dashboard.
public class HomePageAction : IMacroAction
{
    [Preserve]
    static HomePageAction() => MacroActionFactory.Register(MacroActionType.HomeDashboard, () => new HomePageAction());

    public string ActionId => "home_dashboard";
    public string DisplayName => "Home";

    public void Execute()
    {
        RobitLogger.Log($"[{nameof(HomePageAction)}] Executing HomePage Action via ViewCoordinator.");
        
        if (ViewCoordinator.Instance != null)
        {
            ViewCoordinator.Instance.ToggleHome();
        }
        else
        {
            RobitLogger.LogWarning($"[{nameof(HomePageAction)}] ViewCoordinator not found in scene!");
        }
    }
}
