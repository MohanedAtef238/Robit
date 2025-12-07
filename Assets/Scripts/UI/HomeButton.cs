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
        // Reset window to normal opaque state before switching scenes, otherwise the UI elements break "" very important to keep this feature working properly "" 
        WindowManager.MakeOpaque();
        
        if (AppLauncher.Instance != null)
        {
            AppLauncher.Instance.CloseCurrentApp();
        }
        
        SceneManager.LoadScene("MainScene");
    }
}
