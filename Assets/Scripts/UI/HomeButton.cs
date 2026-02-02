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

    void GoHome()
    {
        // First disable the Transparency script so it stops processing
        Transparency transparency = FindObjectOfType<Transparency>();
        if (transparency != null)
        {
            transparency.DisableTransparency();
        }
        
        // Reset window to normal opaque state before switching scenes
        WindowManager.MakeOpaque();
        
        if (AppLauncher.Instance != null)
        {
            AppLauncher.Instance.CloseCurrentApp();
        }
        
        SceneManager.LoadScene("MainScene");
    }
}
