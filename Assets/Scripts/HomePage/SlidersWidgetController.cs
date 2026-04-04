using UnityEngine;
using UnityEngine.UIElements;

/// Controls the volume and brightness sliders on the Home Page.
/// Uses Win32 COM interop for system volume and Dxva2 for monitor brightness.
public class SlidersWidgetController : MonoBehaviour
{
    private Slider soundSlider;
    private Slider brightnessSlider;
    private Label soundValueLabel;
    private Label brightnessValueLabel;
    private Button soundDecBtn, soundIncBtn;
    private Button brightnessDecBtn, brightnessIncBtn;

    private bool _initialized;
    private bool _brightnessSupported = true;

    public void Initialize(VisualElement root)
    {
        soundSlider = root.Q<Slider>("soundSlider");
        brightnessSlider = root.Q<Slider>("brightnessSlider");
        soundValueLabel = root.Q<Label>("soundValueLabel");
        brightnessValueLabel = root.Q<Label>("brightnessValueLabel");

        // Seed sliders with current system values
        float sysVolume = Win32AudioInterop.GetVolume();
        soundSlider.value = sysVolume * 100f;
        UpdateValueLabel(soundValueLabel, soundSlider.value);

        int sysBrightness = Win32BrightnessInterop.GetBrightness();
        if (sysBrightness < 0)
        {
            _brightnessSupported = false;
            brightnessSlider.value = 50;
            brightnessSlider.SetEnabled(false);
            if (brightnessValueLabel != null) brightnessValueLabel.text = "N/A";
            Debug.LogWarning("[SlidersWidget] Brightness control not supported on this display.");
        }
        else
        {
            brightnessSlider.value = sysBrightness;
            UpdateValueLabel(brightnessValueLabel, sysBrightness);
        }

        // Wire arrow buttons (±5 per click)
        soundDecBtn = root.Q<Button>("soundDecBtn");
        soundIncBtn = root.Q<Button>("soundIncBtn");
        brightnessDecBtn = root.Q<Button>("brightnessDecBtn");
        brightnessIncBtn = root.Q<Button>("brightnessIncBtn");

        soundDecBtn?.RegisterCallback<ClickEvent>(_ =>
            soundSlider.value = Mathf.Clamp(soundSlider.value - 5f, 0f, 100f));
        soundIncBtn?.RegisterCallback<ClickEvent>(_ =>
            soundSlider.value = Mathf.Clamp(soundSlider.value + 5f, 0f, 100f));
        brightnessDecBtn?.RegisterCallback<ClickEvent>(_ => {
            if (_brightnessSupported)
                brightnessSlider.value = Mathf.Clamp(brightnessSlider.value - 5f, 0f, 100f);
        });
        brightnessIncBtn?.RegisterCallback<ClickEvent>(_ => {
            if (_brightnessSupported)
                brightnessSlider.value = Mathf.Clamp(brightnessSlider.value + 5f, 0f, 100f);
        });

        // Register value-change callbacks
        soundSlider.RegisterValueChangedCallback(evt =>
        {
            Win32AudioInterop.SetVolume(evt.newValue / 100f);
            UpdateValueLabel(soundValueLabel, evt.newValue);
        });

        brightnessSlider.RegisterValueChangedCallback(evt =>
        {
            if (!_brightnessSupported) return;
            Win32BrightnessInterop.SetBrightness(Mathf.RoundToInt(evt.newValue));
            UpdateValueLabel(brightnessValueLabel, evt.newValue);
        });

        _initialized = true;
    }

    /// Refreshes slider positions from current system state.
    /// Called each time the home page is opened.
    public void RefreshFromSystem()
    {
        if (!_initialized) return;

        float vol = Win32AudioInterop.GetVolume();
        soundSlider.SetValueWithoutNotify(vol * 100f);
        UpdateValueLabel(soundValueLabel, soundSlider.value);

        if (_brightnessSupported)
        {
            int br = Win32BrightnessInterop.GetBrightness();
            if (br >= 0)
            {
                brightnessSlider.SetValueWithoutNotify(br);
                UpdateValueLabel(brightnessValueLabel, br);
            }
        }
    }

    private static void UpdateValueLabel(Label label, float value)
    {
        if (label != null) label.text = $"{Mathf.RoundToInt(value)}%";
    }
}
