
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

/// Main controller for the Home Page dashboard panel.
/// Manages open/close lifecycle, wires sub-controllers, controls the popup suppressor,
/// and handles the View Apps / Back toggle between settings and the app-launcher carousel.
public class HomePageController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement homePanel;
    private VisualElement dialogueElement;
    private VisualElement _tintOverlay;
    private VisualElement _reminderIcon;
    private VisualElement _reminderElement;
    private Button closeBtn;

    private DesktopWidget _clock;
    private SlidersWidgetController _sliders;
    private ZoomPickerController _zoomPicker;
    private HomeBackgroundBlurController _homeBlur;
    private HomeThemeController _theme;
    private WindowsPopupSuppressor _suppressor;
    private Transparency _transparency;

    private VisualElement _settingsView;
    private Button _viewAppsBtn;

    // View-switching state
    private bool _isOpen;
    private bool _initialized;
    private bool _dialogueVisible;
    private bool _reminderExpanded;

    void Awake()
    {
        _clock = GetComponent<DesktopWidget>();
        _sliders = GetComponent<SlidersWidgetController>();
        _zoomPicker = GetComponent<ZoomPickerController>();
        _homeBlur = GetComponent<HomeBackgroundBlurController>();
        _theme = GetComponent<HomeThemeController>();
        _suppressor = GetComponent<WindowsPopupSuppressor>();

        if (_zoomPicker == null)
        {
            _zoomPicker = gameObject.AddComponent<ZoomPickerController>();
            RobitLogger.LogWarning("[HomePageController] ZoomPickerController was missing on HomePageUI and was added at runtime.");
        }

        if (_homeBlur == null)
            _homeBlur = gameObject.AddComponent<HomeBackgroundBlurController>();

        if (_suppressor == null)
            _suppressor = gameObject.AddComponent<WindowsPopupSuppressor>();

        if (_theme == null)
            _theme = gameObject.AddComponent<HomeThemeController>();

        _transparency = FindFirstObjectByType<Transparency>();
    }

    void Start()
    {
        if (uiDocument == null)
        {
            RobitLogger.LogError("[HomePageController] UIDocument not assigned.");
            return;
        }

        var root = uiDocument.rootVisualElement;

        // Support both HomePage.uxml (has home-panel element) and
        // NewUXMLTemplate.uxml (no wrapper — use the root itself).
        homePanel = root.Q("home-panel") ?? root;
        closeBtn = root.Q<Button>("homeCloseBtn");

        // Wire close button (may be null in DemoScene layout)
        closeBtn?.RegisterCallback<ClickEvent>(_ => Close());

        // Initialize sub-controllers
        _sliders?.Initialize(root);
        _zoomPicker?.Initialize(root);
        _theme?.Initialize(root);

        // Hide dialogue bubble by default — shown via robit interaction
        dialogueElement = root.Q("dialogue");
        if (dialogueElement != null)
            dialogueElement.style.display = DisplayStyle.None;

        // --- Pass-through picking on non-interactive containers ---
        // Prevents full-screen Sound element and other containers from
        // blocking raycasts to the 3D scene behind the UI.
        root.pickingMode = PickingMode.Ignore;
        SetContainerIgnore(root, "Sound");
        SetContainerIgnore(root, "signal");
        SetContainerIgnore(root, "brightness");
        SetContainerIgnore(root, "clock-tab");
        SetContainerIgnore(root, "clock-panel");
        SetContainerIgnore(root, "settingsView");

        // Restore interactivity on all buttons/sliders inside settingsView
        // (they were silenced when we set pickingMode=Ignore on the container).
        var settingsViewEl = root.Q("settingsView");
        if (settingsViewEl != null) RestoreInteractivePickingInside(settingsViewEl);

        // --- Semi-transparent tint overlay (replaces MakeOpaque) ---
        _tintOverlay = new VisualElement();
        _tintOverlay.name = "home-tint-overlay";
        _tintOverlay.pickingMode = PickingMode.Ignore;
        _tintOverlay.style.position = Position.Absolute;
        _tintOverlay.style.left = 0;
        _tintOverlay.style.top = 0;
        _tintOverlay.style.right = 0;
        _tintOverlay.style.bottom = 0;
        _tintOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
        _tintOverlay.style.display = DisplayStyle.None;
        homePanel.Insert(0, _tintOverlay);

        // --- Minimize reminder to a small notepad icon ---
        _reminderElement = root.Q("reminder");
        if (_reminderElement != null)
        {
            _reminderElement.style.display = DisplayStyle.None;

            _reminderIcon = new VisualElement();
            _reminderIcon.name = "reminder-icon";
            _reminderIcon.pickingMode = PickingMode.Position;
            _reminderIcon.style.position = Position.Absolute;
            _reminderIcon.style.width = 80;
            _reminderIcon.style.height = 80;
            _reminderIcon.style.top = 180;
            _reminderIcon.style.left = 1090;
            _reminderIcon.style.backgroundImage = _reminderElement.style.backgroundImage;
            _reminderIcon.style.unityBackgroundImageTintColor =
              _reminderElement.style.unityBackgroundImageTintColor;
            _reminderIcon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            _reminderIcon.RegisterCallback<ClickEvent>(_ => ToggleReminder());
            homePanel.Add(_reminderIcon);
        }

        // Ensure panel starts hidden
        homePanel.style.display = DisplayStyle.None;

        // --- View Apps button — delegates entirely to AppLauncherUIToolkit ---
        _settingsView = root.Q("settingsView");
        _viewAppsBtn = root.Q<Button>("viewAppsBtn");
        if (_viewAppsBtn != null)
        {
            _viewAppsBtn.pickingMode = PickingMode.Position;
            _viewAppsBtn.clicked += ToggleAppView;
        }

        _initialized = true;
    }

    private static void SetContainerIgnore(VisualElement root, string name)
    {
        var el = root.Q(name);
        if (el != null) el.pickingMode = PickingMode.Ignore;
    }

    /// Recursively restores PickingMode.Position on all Button and Slider elements
    /// that were silenced by an ancestor having PickingMode.Ignore.
    private static void RestoreInteractivePickingInside(VisualElement container)
    {
        foreach (var child in container.Children())
        {
            if (child is Button || child is Slider || child is Toggle || child is TextField)
                child.pickingMode = PickingMode.Position;
            RestoreInteractivePickingInside(child);
        }
    }

    /// Opens the home page dashboard.
    public void Open()
    {
        if (!_initialized || _isOpen) return;
        _isOpen = true;

        // Blur the background scene while Home is open.
        _homeBlur?.SetBlurActive(true);

        // Refresh slider values from system each time we open
        _sliders?.RefreshFromSystem();

        // Show panel
        homePanel.style.display = DisplayStyle.Flex;

        // Show the tint overlay
        if (_tintOverlay != null)
            _tintOverlay.style.display = DisplayStyle.Flex;

        // Keep layered transparency but capture mouse clicks
        if (_transparency != null)
            _transparency.PausePolling();
        else
            WindowManager.SetClickThrough(false);

        // Enable DWM Acrylic blur behind the window
        WindowManager.SetAcrylicBlur(true);

        // Start suppressing Windows popups
        _suppressor.StartSuppressing();

        var bubbleAnim = GetComponent<UIBubbleEntry>();
        if (bubbleAnim != null)
        {
            bubbleAnim.ReplayAnimation();
        }

        RobitLogger.Log("[HomePageController] Opened.");
    }

    /// Closes the home page dashboard.
    public void Close()
    {
        if (!_initialized || !_isOpen) return;
        _isOpen = false;

        _homeBlur?.SetBlurActive(false);

        // Hide panel
        homePanel.style.display = DisplayStyle.None;

        // Hide tint overlay
        if (_tintOverlay != null)
            _tintOverlay.style.display = DisplayStyle.None;

        // Reset bubble animations
        var bubbleAnim = GetComponent<UIBubbleEntry>();
        if (bubbleAnim != null)
        {
            bubbleAnim.ResetAnimation();
        }

        // Also hide the dialogue bubble
        HideDialogue();

        // Reset to settings view if app view was open
        ResetToSettingsView();

        // Collapse reminder if expanded
        CollapseReminder();

        // Stop suppressor — restores any hidden windows
        _suppressor.StopSuppressing();

        // Disable DWM Acrylic blur (before restoring layered style)
        WindowManager.SetAcrylicBlur(false);

        // Restore transparent overlay
        if (_transparency != null)
            _transparency.ResumePolling();
        else
            WindowManager.MakeTransparent();

        RobitLogger.Log("[HomePageController] Closed.");
    }

    public bool IsOpen => _isOpen;

    /// Toggles the dialogue speech bubble visibility.
    /// Called by RobitInteraction when the HomePage is open.
    public void ToggleDialogue()
    {
        if (!_initialized || !_isOpen || dialogueElement == null) return;
        _dialogueVisible = !_dialogueVisible;
        dialogueElement.style.display = _dialogueVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void HideDialogue()
    {
        _dialogueVisible = false;
        if (dialogueElement != null)
            dialogueElement.style.display = DisplayStyle.None;
    }

    private void ToggleReminder()
    {
        if (_reminderElement == null) return;
        _reminderExpanded = !_reminderExpanded;
        _reminderElement.style.display = _reminderExpanded ? DisplayStyle.Flex : DisplayStyle.None;
        if (_reminderIcon != null)
            _reminderIcon.style.display = _reminderExpanded ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void CollapseReminder()
    {
        _reminderExpanded = false;
        if (_reminderElement != null)
            _reminderElement.style.display = DisplayStyle.None;
        if (_reminderIcon != null)
            _reminderIcon.style.display = DisplayStyle.Flex;
    }

    // ── View Apps ─────────────────────────────────────────────────────────────

    /// Called by the "View Apps" button — delegates entirely to AppLauncherUIToolkit.
    private void ToggleAppView()
    {
        var launcher = FindFirstObjectByType<AppLauncherUIToolkit>();
        if (launcher != null)
            launcher.Toggle();
        else
            RobitLogger.LogWarning("[HomePageController] AppLauncherUIToolkit not found in scene.");
    }

    private void ResetToSettingsView()
    {
        if (_settingsView != null) _settingsView.style.display = DisplayStyle.Flex;
        if (_viewAppsBtn != null) _viewAppsBtn.text = "View Apps";
    }

}