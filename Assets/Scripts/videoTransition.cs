using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class IntroVideoController : MonoBehaviour
{
    [Header("Scene Management")]
    [Tooltip("The exact name of the scene to load after the video finishes.")]
    [SerializeField] private string nextSceneName;

    [Header("Settings")]
    [Tooltip("Can the user press a key/click to skip the video?")]
    [SerializeField] private bool allowSkip = true;

    private VideoPlayer _videoPlayer;
    private bool _isTransitioning = false;

    void Awake()
    {
        _videoPlayer = GetComponent<VideoPlayer>();
    }

    void Start()
    {
        if (_videoPlayer == null)
        {
            Debug.LogError("[IntroVideoController] No VideoPlayer component found on this GameObject.");
            return;
        }

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("[IntroVideoController] Next Scene Name is not assigned! App will not transition.");
        }

        // Subscribe to the event that triggers when the video reaches its end cleanly
        _videoPlayer.loopPointReached += OnVideoFinished;
    }

    void Update()
    {
        if (!allowSkip || _isTransitioning) return;

        // Check for click or keypress to skip using the New Input System cleanly if available
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TransitionToNextScene();
        }
        else if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            TransitionToNextScene();
        }
#else
        // Fallback for legacy input system
        if (Input.anyKeyDown)
        {
            TransitionToNextScene();
        }
#endif
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        Debug.Log("[IntroVideoController] Video finished playback natively.");
        TransitionToNextScene();
    }

    private void TransitionToNextScene()
    {
        // Guard clause to prevent double-loading if a user clicks right as the video ends
        if (_isTransitioning || string.IsNullOrEmpty(nextSceneName)) return;
        _isTransitioning = true;

        // Unsubscribe from event cleanly to avoid memory leaks
        if (_videoPlayer != null)
        {
            _videoPlayer.loopPointReached -= OnVideoFinished;
        }

        Debug.Log($"[IntroVideoController] Loading scene: {nextSceneName}");
        SceneManager.LoadScene(nextSceneName);
    }
}