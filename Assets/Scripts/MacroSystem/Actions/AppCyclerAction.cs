using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

/// Macro action that loads the App Launcher scene for selecting desktop shortcuts.
public class AppCyclerAction : IMacroAction
{
    [Preserve]
    static AppCyclerAction() => MacroActionFactory.Register(MacroActionType.AppCycler, () => new AppCyclerAction());

    public string ActionId => "app_cycler";
    public string DisplayName => "Switch Apps";

    public void Execute()
    {
        SceneManager.LoadScene("HomeScene");
    }
}

