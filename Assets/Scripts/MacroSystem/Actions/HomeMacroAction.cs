using UnityEngine;
using UnityEngine.SceneManagement;

/// Navigates back to the main scene, resets transparency, and closes any running app.
public class HomeMacroAction : IMacroAction
{
    public string ActionId => "home";
    public string DisplayName => "Home";

    public void Execute()
    {
        Debug.Log("[MacroButton] Executing: home");

        if (AppLauncher.Instance != null)
        {
            AppLauncher.Instance.GoHome();
        }
        else
        {
            // Fallback if AppLauncher is missing (e.g. testing in isolation)
            WindowManager.MakeOpaque();
            SceneManager.LoadScene("MainScene");
        }
    }
}
