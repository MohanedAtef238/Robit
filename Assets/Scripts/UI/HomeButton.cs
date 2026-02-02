using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HomeButton : MonoBehaviour
{
    void Start()
    {

        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(GoHome);
        }
    }

    // Disables click handler to prevent ghost exit, then safely transitions to Home
    void GoHome()
    {
        UIClickHandler myHandler = GetComponent<UIClickHandler>();
        if (myHandler != null) 
            myHandler.enabled = false;

        Transparency transparency = FindFirstObjectByType<Transparency>();
        if (transparency != null)
            transparency.SwitchToHomeMode();
        else
            WindowManager.MakeOpaque();
        
        if (AppLauncher.Instance != null)
            AppLauncher.Instance.CloseCurrentApp();
        
        SceneManager.LoadScene("MainScene");
    }
}
