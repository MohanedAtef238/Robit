using UnityEngine;
using UnityEngine.UIElements;

/// Main controller for the Home Page dashboard panel.
/// Manages open/close lifecycle, wires sub-controllers, and controls the popup suppressor.
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
    private WindowsPopupSuppressor _suppressor;

    private bool _isOpen;
    private bool _initialized;
    private bool _dialogueVisible;
    private bool _reminderExpanded;

    void Awake()
    {
        _clock = GetComponent<DesktopWidget>();
        _sliders = GetComponent<SlidersWidgetController>();
        _zoomPicker = GetComponent<ZoomPickerController>();
        _suppressor = GetComponent<WindowsPopupSuppressor>();
        if (_suppressor == null)
            _suppressor = gameObject.AddComponent<WindowsPopupSuppressor>();
    }

    void Start()
    {
        if (uiDocument == null)
        {
            Debug.LogError("[HomePageController] UIDocument not assigned.");
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
        SetContainerIgnore(root, "clockWidget");

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

        _initialized = true;
    }

    private static void SetContainerIgnore(VisualElement root, string name)
    {
        var el = root.Q(name);
        if (el != null) el.pickingMode = PickingMode.Ignore;
    }

    /// Opens the home page dashboard.
    public void Open()
    {
        if (!_initialized || _isOpen) return;
        _isOpen = true;

        // Refresh slider values from system each time we open
        _sliders?.RefreshFromSystem();

        // Show panel
        homePanel.style.display = DisplayStyle.Flex;

        // Show the tint overlay
        if (_tintOverlay != null)
            _tintOverlay.style.display = DisplayStyle.Flex;

        // Keep layered transparency but capture mouse clicks
        WindowManager.SetClickThrough(false);

        // Start suppressing Windows popups
        _suppressor.StartSuppressing();

        Debug.Log("[HomePageController] Opened.");
    }

    /// Closes the home page dashboard.
    public void Close()
    {
        if (!_initialized || !_isOpen) return;
        _isOpen = false;

        // Hide panel
        homePanel.style.display = DisplayStyle.None;

        // Hide tint overlay
        if (_tintOverlay != null)
            _tintOverlay.style.display = DisplayStyle.None;

        // Also hide the dialogue bubble
        HideDialogue();

        // Collapse reminder if expanded
        CollapseReminder();

        // Stop suppressor — restores any hidden windows
        _suppressor.StopSuppressing();

        // Restore transparent overlay
        WindowManager.MakeTransparent();

        Debug.Log("[HomePageController] Closed.");
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
}
