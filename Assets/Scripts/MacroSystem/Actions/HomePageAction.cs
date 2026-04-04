using UnityEngine;

/// Macro action that opens the Home Page dashboard.
public class HomePageAction : IMacroAction
{
    public string ActionId => "home_dashboard";
    public string DisplayName => "Home";

    public void Execute()
    {
        var controller = Object.FindFirstObjectByType<HomePageController>();
        if (controller != null)
        {
            if (controller.IsOpen)
                controller.Close();
            else
                controller.Open();
        }
        else
        {
            Debug.LogWarning("[HomePageAction] No HomePageController found in scene.");
        }
    }
}
