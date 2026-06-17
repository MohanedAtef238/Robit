using UnityEngine;
using UnityEngine.UIElements;

public class GlobalButtonSound : MonoBehaviour
{
    [Header("Sound")]
    [Tooltip("The click sound to play for every button.")]
    [SerializeField] private AudioClip _clickSound;

    [Tooltip("Volume of the click sound.")]
    [Range(0f, 1f)]
    [SerializeField] private float _volume = 1f;

    [Header("Background Noise")]
    [Tooltip("Ambient clip to loop continuously in the background.")]
    [SerializeField] private AudioClip _backgroundClip;

    [Tooltip("Volume of the background loop.")]
    [Range(0f, 1f)]
    [SerializeField] private float _backgroundVolume = 0.5f;

    private AudioSource _audioSource;
    private AudioSource _backgroundSource;

    private void Awake()
    {
        // 1. Setup Audio Listener safely if one isn't present
        if (FindFirstObjectByType<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
            Debug.Log("[GlobalButtonSound] No AudioListener found in scene — added one automatically.");
        }

        // 2. Explicitly add the SFX AudioSource component to avoid MissingComponentExceptions
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;

        // 3. Explicitly add a separate Background AudioSource component
        _backgroundSource = gameObject.AddComponent<AudioSource>();
        _backgroundSource.playOnAwake = false;
        _backgroundSource.loop = true;
        _backgroundSource.volume = _backgroundVolume;

        // 4. Play background tracks safely
        if (_backgroundClip != null)
        {
            _backgroundSource.clip = _backgroundClip;
            _backgroundSource.Play();
        }
        else
        {
            Debug.LogWarning("[GlobalButtonSound] No background AudioClip assigned in the Inspector.");
        }
    }

    private void OnEnable()
    {
        // Listen globally to the UI Toolkit event system using TrickleDown/Bubbling
        var uiDocuments = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var doc in uiDocuments)
        {
            if (doc.rootVisualElement != null)
            {
                doc.rootVisualElement.RegisterCallback<NavigationSubmitEvent>(OnVisualElementSubmitted, TrickleDown.TrickleDown);
                doc.rootVisualElement.RegisterCallback<ClickEvent>(OnVisualElementClicked, TrickleDown.TrickleDown);
            }
        }
    }

    private void OnDisable()
    {
        var uiDocuments = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var doc in uiDocuments)
        {
            if (doc?.rootVisualElement != null)
            {
                doc.rootVisualElement.UnregisterCallback<NavigationSubmitEvent>(OnVisualElementSubmitted, TrickleDown.TrickleDown);
                doc.rootVisualElement.UnregisterCallback<ClickEvent>(OnVisualElementClicked, TrickleDown.TrickleDown);
            }
        }
    }

    private void OnVisualElementSubmitted(NavigationSubmitEvent evt)
    {
        if (evt.target is Button)
        {
            PlayClickSound();
        }
    }

    private void OnVisualElementClicked(ClickEvent evt)
    {
        if (evt.target is Button)
        {
            PlayClickSound();
        }
    }

    private void PlayClickSound()
    {
        if (_clickSound == null) return;
        _audioSource.PlayOneShot(_clickSound, _volume);
    }
}