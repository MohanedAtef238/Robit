using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HomeButton : MonoBehaviour
{
    void Start()
    {

        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(()=>StartCoroutine(GoHome()));
        }
    }

    // Disables click handler to prevent ghost exit, then safely transitions to Home
    IEnumerator  GoHome()
    {
        UIClickHandler myHandler = GetComponent<UIClickHandler>();
        if (myHandler != null) 
            myHandler.enabled = false;

        // Transparency transparency = FindFirstObjectByType<Transparency>();
        // if (transparency != null)
        //     transparency.SwitchToHomeMode();
        // else
        //     WindowManager.MakeOpaque();
        
        if (AppLauncher.Instance != null)
            AppLauncher.Instance.CloseCurrentApp();
        var op = SceneManager.LoadSceneAsync("MainScene", LoadSceneMode.Additive);
        while (!op.isDone) yield return null;

        var main = SceneManager.GetSceneByName("MainScene");
        if (main.IsValid() && main.isLoaded)
            SceneManager.SetActiveScene(main);
    }
}
