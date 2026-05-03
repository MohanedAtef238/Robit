using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

public class ReturnToDesktopAction : IMacroAction
{
    [Preserve]
    static ReturnToDesktopAction() => MacroActionFactory.Register(MacroActionType.ReturnToDesktop, () => new ReturnToDesktopAction());

    public string ActionId => "return_to_desktop";
    public string DisplayName => "Desktop";

    public void Execute()
    {
        var launcher = Object.FindFirstObjectByType<AppLauncher>();
        if (launcher != null)
            launcher.GoHome();
        else
            RobitLogger.LogWarning("[ReturnToDesktopAction] No AppLauncher found.");
    }
}

