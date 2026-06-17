using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Assign the Canvas or Panel that contains your loading background and firefly animations.")]
    public GameObject loadingOverlayVisuals;

    private void Awake()
    {
        // Establish an easy-to-access Singleton instance
        if (Instance == null)
        {
            Instance = this;
            // Optional: Keeps the loading manager alive across scenes if desired
            // DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Ensure the overlay starts turned off
        if (loadingOverlayVisuals != null)
        {
            loadingOverlayVisuals.SetActive(false);
        }
    }

    /// <summary>
    /// Call this from any script to safely transition scenes with a hard minimum visual hold time.
    /// </summary>
    public void TransitionToScene(string sceneName, float minimumHoldTime, System.Action preLoadCallback = null)
    {
        StartCoroutine(ExecuteTransitionFlow(sceneName, minimumHoldTime, preLoadCallback));
    }

    private IEnumerator ExecuteTransitionFlow(string sceneName, float holdTime, System.Action preLoadCallback)
    {
        UnityEngine.Debug.Log($"[TransitionManager] Initializing lock on screen visuals.");

        // 1. Immediately turn on the visual overlay & fireflies
        if (loadingOverlayVisuals != null)
        {
            loadingOverlayVisuals.SetActive(true);
        }

        // 2. Execute any data saves or setup needed BEFORE the countdown/scene shift
        preLoadCallback?.Invoke();

        // 3. Strict Frame Loop Cooldown: Absolutely hold execution here for the visual window
        float elapsed = 0f;
        while (elapsed < holdTime)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        UnityEngine.Debug.Log($"[TransitionManager] Minimum visual hold of {holdTime}s finished. Commencing scene load.");

        // 4. Safely load the next scene
        SceneManager.LoadScene(sceneName);
    }
}