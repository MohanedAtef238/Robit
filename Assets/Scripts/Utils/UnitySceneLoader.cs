using UnityEngine.SceneManagement;

public class UnitySceneLoader : ISceneLoader
{
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}

