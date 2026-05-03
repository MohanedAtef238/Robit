using UnityEngine;
using UnityEngine.UIElements;

/// Toggles the Home Page between the original purple/green palette
/// and the modern blue/grey palette at runtime.
/// Attach to the same GameObject as HomePageController.
public class HomeThemeController : MonoBehaviour
{
    public enum Theme { Classic, Modern }

    [SerializeField] private Theme activeTheme = Theme.Modern;
    [SerializeField] private StyleSheet classicSheet;   // slider.uss
    [SerializeField] private StyleSheet modernSheet;    // slider-modern.uss

    private VisualElement _root;
    private bool _initialized;

    // ── Classic palette (purple / green) ──
    private static readonly Color ClassicButtonBg         = new Color(125/255f, 101/255f, 182/255f, 0.65f);
    private static readonly Color ClassicLabelColor       = new Color(78/255f, 81/255f, 153/255f, 1f);
    private static readonly Color ClassicDialogueOutColor = new Color(153/255f, 78/255f, 117/255f, 1f);
    private static readonly Color ClassicBorderColor      = new Color(78/255f, 81/255f, 153/255f, 1f);
    private static readonly Color ClassicScaleLabelColor  = new Color(215/255f, 195/255f, 255/255f, 0.85f);
    private static readonly Color ClassicTintOverlay      = new Color(0f, 0f, 0f, 0.35f);

    // ── Modern palette (blue / grey) ──
    private static readonly Color ModernButtonBg         = new Color(70/255f, 90/255f, 120/255f, 0.65f);
    private static readonly Color ModernLabelColor       = new Color(70/255f, 100/255f, 145/255f, 1f);
    private static readonly Color ModernDialogueOutColor = new Color(80/255f, 110/255f, 150/255f, 1f);
    private static readonly Color ModernBorderColor      = new Color(70/255f, 100/255f, 145/255f, 1f);
    private static readonly Color ModernScaleLabelColor  = new Color(180/255f, 200/255f, 230/255f, 0.85f);
    private static readonly Color ModernTintOverlay      = new Color(0.05f, 0.08f, 0.14f, 0.30f);

    public void Initialize(VisualElement root)
    {
        _root = root;
        _initialized = true;
        ApplyTheme(activeTheme);
    }

    public void SetTheme(Theme theme)
    {
        activeTheme = theme;
        if (_initialized)
            ApplyTheme(theme);
    }

    public void ToggleTheme()
    {
        SetTheme(activeTheme == Theme.Classic ? Theme.Modern : Theme.Classic);
    }

    private struct ThemeProfile
    {
        public Color LabelColor;
        public Color DialogueOutColor;
        public Color BorderColor;
        public Color ScaleLabelColor;
        public Color TintOverlay;
    }

    private ThemeProfile GetProfile(Theme theme)
    {
        if (theme == Theme.Modern)
        {
            return new ThemeProfile
            {
                LabelColor = ModernLabelColor,
                DialogueOutColor = ModernDialogueOutColor,
                BorderColor = ModernBorderColor,
                ScaleLabelColor = ModernScaleLabelColor,
                TintOverlay = ModernTintOverlay
            };
        }
        return new ThemeProfile
        {
            LabelColor = ClassicLabelColor,
            DialogueOutColor = ClassicDialogueOutColor,
            BorderColor = ClassicBorderColor,
            ScaleLabelColor = ClassicScaleLabelColor,
            TintOverlay = ClassicTintOverlay
        };
    }

    private void ApplyTheme(Theme theme)
    {
        bool modern = theme == Theme.Modern;
        var profile = GetProfile(theme);

        // ── Swap USS stylesheet ──
        if (classicSheet != null && modernSheet != null)
        {
            if (modern)
            {
                if (_root.styleSheets.Contains(classicSheet))
                    _root.styleSheets.Remove(classicSheet);
                if (!_root.styleSheets.Contains(modernSheet))
                    _root.styleSheets.Add(modernSheet);
            }
            else
            {
                if (_root.styleSheets.Contains(modernSheet))
                    _root.styleSheets.Remove(modernSheet);
                if (!_root.styleSheets.Contains(classicSheet))
                    _root.styleSheets.Add(classicSheet);
            }
        }

        // ── Clear inline UXML button background-color so USS
        //    :hover / :active pseudo-classes can take effect. ──
        string[] buttons = { "soundDecBtn", "soundIncBtn", "brightnessDecBtn", "brightnessIncBtn", "zoom100Btn", "zoom125Btn", "zoom150Btn", "zoom200Btn" };
        foreach (var btnName in buttons) ClearBg(_root, btnName);

        // ── Clock / weather labels ──
        string[] labels = { "locationLabel", "digitalClockLabel", "digitalClockM", "celsiusLabel", "degreeLabel", "statusLabel" };
        foreach (var lblName in labels) SetColor(_root, lblName, profile.LabelColor);

        // ── Dialogue ──
        SetColor(_root, "outputDialogueLabel", profile.DialogueOutColor);
        SetColor(_root, "inputDialogueLabel",  profile.LabelColor);
        
        var field = _root.Q("inputDialogueField");
        if (field != null)
        {
            field.style.borderLeftColor  = profile.BorderColor;
            field.style.borderRightColor = profile.BorderColor;
            field.style.borderTopColor   = profile.BorderColor;
            field.style.borderBottomColor = profile.BorderColor;
        }

        // ── Display Scale heading ──
        var zoomPicker = _root.Q("zoom-picker");
        if (zoomPicker != null)
        {
            var heading = zoomPicker.Q<Label>();
            if (heading != null) heading.style.color = profile.ScaleLabelColor;
        }

        // ── Tint overlay ──
        var overlay = _root.Q("home-tint-overlay");
        if (overlay != null)
            overlay.style.backgroundColor = profile.TintOverlay;

        RobitLogger.Log($"[HomeTheme] Applied {theme} theme.");
    }

    private static void SetBg(VisualElement root, string name, Color c)
    {
        var el = root.Q(name);
        if (el != null) el.style.backgroundColor = c;
    }

    private static void ClearBg(VisualElement root, string name)
    {
        var el = root.Q(name);
        if (el != null) el.style.backgroundColor = StyleKeyword.Null;
    }

    private static void SetColor(VisualElement root, string name, Color c)
    {
        var el = root.Q(name);
        if (el != null) el.style.color = c;
    }
}

