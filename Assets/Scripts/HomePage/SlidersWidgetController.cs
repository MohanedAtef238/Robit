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
    private Slider brightnessSlider;          // may be null — UI layout is button-only
    private Label soundValueLabel;
    private Label brightnessValueLabel;
    private Button soundDecBtn, soundIncBtn;
    private Button brightnessDecBtn, brightnessIncBtn;
    private VisualElement _sunIconEl;
    private VisualElement _soundIconEl; // The speaker icon element

    private bool _initialized;
    private bool _brightnessSupported;   // true = DDC/CI hardware API available
    private int  _currentBrightness = 100; // internal tracker when no slider exists

    public void Initialize(VisualElement root)
    {
        soundSlider          = root.Q<Slider>("soundSlider");
        brightnessSlider     = root.Q<Slider>("brightnessSlider"); // null in button-only layouts
        soundValueLabel      = root.Q<Label>("soundValueLabel");
        brightnessValueLabel = root.Q<Label>("brightnessValueLabel");

        _sunIconEl   = root.Q<VisualElement>("sunIcon");
        _soundIconEl = root.Q<VisualElement>("soundIcon");

        if (_sunIconEl != null)
            _sunIconEl.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));

        // ── Sound ────────────────────────────────────────────────────────────
        float sysVolume  = Win32AudioInterop.GetVolume();
        float volPercent = sysVolume * 100f;
        if (soundSlider != null) soundSlider.value = volPercent;
        UpdateValueLabel(soundValueLabel, volPercent);
        UpdateSoundIcon(volPercent);

        // ── Brightness ───────────────────────────────────────────────────────
        // Try hardware API first; fall back to a software-only value tracker.
        int sysBrightness = Win32BrightnessInterop.GetBrightness();
        if (sysBrightness >= 0)
        {
            _brightnessSupported = true;
            _currentBrightness   = sysBrightness;
        }
        else
        {
            _brightnessSupported = false;
            _currentBrightness   = 100; // visual default
            RobitLogger.LogWarning("[SlidersWidgetController] Hardware brightness API unavailable. " +
                                   "Brightness buttons will update the display only.");
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.value = _currentBrightness;
            brightnessSlider.SetEnabled(_brightnessSupported);
        }
        UpdateValueLabel(brightnessValueLabel, _currentBrightness);
        UpdateSunIcon(_currentBrightness);

        SetupButtons(root);

        // ── Callbacks ────────────────────────────────────────────────────────
        if (soundSlider != null)
        {
            soundSlider.RegisterValueChangedCallback(evt =>
            {
                Win32AudioInterop.SetVolume(evt.newValue / 100f);
                UpdateValueLabel(soundValueLabel, evt.newValue);
                UpdateSoundIcon(evt.newValue);
            });
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.RegisterValueChangedCallback(evt =>
            {
                _currentBrightness = Mathf.RoundToInt(evt.newValue);
                if (_brightnessSupported)
                    Win32BrightnessInterop.SetBrightness(_currentBrightness);
                UpdateValueLabel(brightnessValueLabel, evt.newValue);
                UpdateSunIcon(evt.newValue);
            });
        }

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
        soundDecBtn      = root.Q<Button>("soundDecBtn");
        soundIncBtn      = root.Q<Button>("soundIncBtn");
        brightnessDecBtn = root.Q<Button>("brightnessDecBtn");
        brightnessIncBtn = root.Q<Button>("brightnessIncBtn");

        soundDecBtn?.RegisterCallback<ClickEvent>(_ => { if (soundSlider != null) soundSlider.value -= 5f; });
        soundIncBtn?.RegisterCallback<ClickEvent>(_ => { if (soundSlider != null) soundSlider.value += 5f; });

        // Brightness buttons update the internal tracker and call the API directly.
        // This works even when there is no slider element in the layout.
        brightnessDecBtn?.RegisterCallback<ClickEvent>(_ => AdjustBrightness(-10));
        brightnessIncBtn?.RegisterCallback<ClickEvent>(_ => AdjustBrightness(+10));
    }

    private void AdjustBrightness(int delta)
    {
        _currentBrightness = Mathf.Clamp(_currentBrightness + delta, 0, 100);

        // Drive the slider if present (triggers its own callback)
        if (brightnessSlider != null)
        {
            brightnessSlider.SetValueWithoutNotify(_currentBrightness);
        }

        // Push to hardware if supported
        if (_brightnessSupported)
            Win32BrightnessInterop.SetBrightness(_currentBrightness);

        // Always update label + sun icon so the UI is responsive
        UpdateValueLabel(brightnessValueLabel, _currentBrightness);
        UpdateSunIcon(_currentBrightness);
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
        if (soundSlider != null) soundSlider.SetValueWithoutNotify(volPercent);
        UpdateValueLabel(soundValueLabel, volPercent);
        UpdateSoundIcon(volPercent); // Sync icon on refresh

        if (_brightnessSupported && brightnessSlider != null)
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