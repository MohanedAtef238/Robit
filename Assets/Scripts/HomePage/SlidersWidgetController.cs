using UnityEngine;
using UnityEngine.UIElements;

public class SlidersWidgetController : MonoBehaviour
{
    [Header("Sun Scale Settings")]
    [SerializeField] private float minSunScale = 0.6f;
    [SerializeField] private float maxSunScale = 1.4f;

    [Header("Sound Icon Sprites")]
    [SerializeField] private Texture2D soundMuted;   // 0%
    [SerializeField] private Texture2D soundLow;     // 1-33%
    [SerializeField] private Texture2D soundMedium;  // 34-66%
    [SerializeField] private Texture2D soundHigh;    // 67-100%

    private Slider soundSlider;
    private Slider brightnessSlider;
    private Label soundValueLabel;
    private Label brightnessValueLabel;
    private Button soundDecBtn, soundIncBtn;
    private Button brightnessDecBtn, brightnessIncBtn;
    private VisualElement _sunIconEl;
    private VisualElement _soundIconEl; // The speaker icon element

    private bool _initialized;
    private bool _brightnessSupported = true;

    public void Initialize(VisualElement root)
    {
        soundSlider = root.Q<Slider>("soundSlider");
        brightnessSlider = root.Q<Slider>("brightnessSlider");
        soundValueLabel = root.Q<Label>("soundValueLabel");
        brightnessValueLabel = root.Q<Label>("brightnessValueLabel");

        _sunIconEl = root.Q<VisualElement>("sunIcon");
        _soundIconEl = root.Q<VisualElement>("soundIcon"); // Find the speaker icon

        if (_sunIconEl != null)
        {
            _sunIconEl.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
        }

        // Initialize Sound
        float sysVolume = Win32AudioInterop.GetVolume();
        float volPercent = sysVolume * 100f;
        soundSlider.value = volPercent;
        UpdateValueLabel(soundValueLabel, volPercent);
        UpdateSoundIcon(volPercent); // Set initial speaker icon

        // Initialize Brightness
        int sysBrightness = Win32BrightnessInterop.GetBrightness();
        if (sysBrightness < 0)
        {
            _brightnessSupported = false;
            brightnessSlider.value = 50;
            brightnessSlider.SetEnabled(false);
            if (brightnessValueLabel != null) brightnessValueLabel.text = "N/A";
        }
        else
        {
            brightnessSlider.value = sysBrightness;
            UpdateValueLabel(brightnessValueLabel, sysBrightness);
            UpdateSunIcon(sysBrightness);
        }

        SetupButtons(root);

        // Value Changed Callbacks
        soundSlider.RegisterValueChangedCallback(evt =>
        {
            Win32AudioInterop.SetVolume(evt.newValue / 100f);
            UpdateValueLabel(soundValueLabel, evt.newValue);
            UpdateSoundIcon(evt.newValue); // Update speaker icon on slide
        });

        brightnessSlider.RegisterValueChangedCallback(evt =>
        {
            if (!_brightnessSupported) return;
            Win32BrightnessInterop.SetBrightness(Mathf.RoundToInt(evt.newValue));
            UpdateValueLabel(brightnessValueLabel, evt.newValue);
            UpdateSunIcon(evt.newValue);
        });

        _initialized = true;
    }

    private void UpdateSoundIcon(float value)
    {
        if (_soundIconEl == null) return;

        Texture2D targetTexture;

        if (value <= 0f)
            targetTexture = soundMuted;
        else if (value <= 33f)
            targetTexture = soundLow;
        else if (value <= 66f)
            targetTexture = soundMedium;
        else
            targetTexture = soundHigh;

        if (targetTexture != null)
            _soundIconEl.style.backgroundImage = new StyleBackground(targetTexture);
    }

    private void UpdateSunIcon(float value)
    {
        if (_sunIconEl == null) return;
        float t = value / 100f;
        float currentScale = Mathf.Lerp(minSunScale, maxSunScale, t);
        _sunIconEl.style.scale = new StyleScale(new Scale(new Vector3(currentScale, currentScale, 1f)));
    }

    private void SetupButtons(VisualElement root)
    {
        soundDecBtn = root.Q<Button>("soundDecBtn");
        soundIncBtn = root.Q<Button>("soundIncBtn");
        brightnessDecBtn = root.Q<Button>("brightnessDecBtn");
        brightnessIncBtn = root.Q<Button>("brightnessIncBtn");

        soundDecBtn?.RegisterCallback<ClickEvent>(_ => soundSlider.value -= 5f);
        soundIncBtn?.RegisterCallback<ClickEvent>(_ => soundSlider.value += 5f);

        brightnessDecBtn?.RegisterCallback<ClickEvent>(_ => {
            if (_brightnessSupported) brightnessSlider.value -= 5f;
        });
        brightnessIncBtn?.RegisterCallback<ClickEvent>(_ => {
            if (_brightnessSupported) brightnessSlider.value += 5f;
        });
    }

    private static void UpdateValueLabel(Label label, float value)
    {
        if (label != null) label.text = $"{Mathf.RoundToInt(value)}%";
    }

    public void RefreshFromSystem()
    {
        if (!_initialized) return;

        float vol = Win32AudioInterop.GetVolume();
        float volPercent = vol * 100f;
        soundSlider.SetValueWithoutNotify(volPercent);
        UpdateValueLabel(soundValueLabel, volPercent);
        UpdateSoundIcon(volPercent); // Sync icon on refresh

        if (_brightnessSupported)
        {
            int br = Win32BrightnessInterop.GetBrightness();
            if (br >= 0)
            {
                brightnessSlider.SetValueWithoutNotify(br);
                UpdateValueLabel(brightnessValueLabel, br);
                UpdateSunIcon(br);
            }
        }
    }
}