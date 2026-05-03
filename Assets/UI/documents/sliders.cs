using UnityEngine;
using UnityEngine.UIElements;

public class SettingsSliders : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private Slider soundSlider;
    private Slider brightnessSlider;

    void Start()
    {
        var root = uiDocument.rootVisualElement;

        // Get sliders
        soundSlider = root.Q<Slider>("soundSlider");
        brightnessSlider = root.Q<Slider>("brightnessSlider");

        // Hook up events
        soundSlider.RegisterValueChangedCallback(evt => UpdateSound(evt.newValue));
        brightnessSlider.RegisterValueChangedCallback(evt => UpdateBrightness(evt.newValue));

        // Initialize with current settings
        UpdateSound(soundSlider.value);
        UpdateBrightness(brightnessSlider.value);
    }

    // -----------------------------
    // Sound
    // -----------------------------
    private void UpdateSound(float value)
    {
        // Example: update AudioListener volume
        AudioListener.volume = value;

        // Optional: debug
        RobitLogger.Log($"Sound volume: {value}");
    }

    // -----------------------------
    // Brightness
    // -----------------------------
    private void UpdateBrightness(float value)
    {
        // Example: adjust global brightness (via post-processing or lighting)
        // Here we'll simulate via ambient light
        RenderSettings.ambientLight = Color.white * value;

        // Optional: debug
        RobitLogger.Log($"Brightness: {value}");
    }
}
