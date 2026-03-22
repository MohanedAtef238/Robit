using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnToDesktopAction : IMacroAction
{
    public string ActionId => "return_to_desktop";
    public string DisplayName => "Home";

    public void Execute()
    {
        // var launcher = AppLauncher.Instance;
        // if (launcher != null)
        // {
        //     launcher.ReturnToDesktop();
        // }
        // else
        // {
        //     Debug.LogWarning("[ReturnToDesktopAction] AppLauncher.Instance is null.");
        // }

        SceneManager.LoadScene("DemoScene");

        Debug.Log("[MacroButton] Executing: return_to_desktop");
    }
}
