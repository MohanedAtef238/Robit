using UnityEngine;
using UnityEngine.UIElements;

/// Controls the dark-tinted settings overlay with an Exit button.
/// Clicking the backdrop (outside the card) or the Close button dismisses it.
/// The Exit button calls HiddenWindowTracker.RestoreAll() before quitting.
public class SettingsOverlayController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private VisualElement backdrop;
    private VisualElement card;
    private Button exitBtn;
    private Button closeBtn;

    private bool _isOpen;
    private bool _initialized;

    void Start()
    {
        if (uiDocument == null)
        {
            Debug.LogError("[SettingsOverlay] UIDocument not assigned.");
            return;
        }

        var root = uiDocument.rootVisualElement;

        backdrop = root.Q("settings-backdrop");
        card = root.Q("settings-card");
        exitBtn = root.Q<Button>("exitBtn");
        closeBtn = root.Q<Button>("settingsCloseBtn");

        if (backdrop == null || card == null)
        {
            Debug.LogError("[SettingsOverlay] Could not find required UI elements.");
            return;
        }

        // Wire buttons
        exitBtn?.RegisterCallback<ClickEvent>(_ => ExitApplication());
        closeBtn?.RegisterCallback<ClickEvent>(_ => Close());

        // Click on backdrop (outside card) to dismiss
        backdrop.RegisterCallback<ClickEvent>(evt =>
        {
            // Only dismiss if the click target is the backdrop itself, not the card
            if (evt.target == backdrop)
                Close();
        });

        // Ensure hidden on start
        backdrop.RemoveFromClassList("settings-backdrop--visible");
        backdrop.AddToClassList("settings-backdrop--hidden");

        _initialized = true;
    }

    /// Opens the settings overlay.
    public void Open()
    {
        if (!_initialized || _isOpen) return;
        _isOpen = true;

        // Show backdrop
        backdrop.RemoveFromClassList("settings-backdrop--hidden");
        backdrop.AddToClassList("settings-backdrop--visible");

        // Animate card in
        card.RemoveFromClassList("settings-card--enter");
        card.AddToClassList("settings-card--ready");

        WindowManager.MakeOpaque();

        Debug.Log("[SettingsOverlay] Opened.");
    }

    /// Closes the settings overlay.
    public void Close()
    {
        if (!_initialized || !_isOpen) return;
        _isOpen = false;

        // Hide card
        card.RemoveFromClassList("settings-card--ready");
        card.AddToClassList("settings-card--enter");

        // Hide backdrop
        backdrop.RemoveFromClassList("settings-backdrop--visible");
        backdrop.AddToClassList("settings-backdrop--hidden");

        // Check if the home page is still open — if not, restore transparency
        var homePage = FindFirstObjectByType<HomePageController>();
        if (homePage == null || !homePage.IsOpen)
            WindowManager.MakeTransparent();

        Debug.Log("[SettingsOverlay] Closed.");
    }

    /// Cleanly exits the application after restoring all hidden windows.
    private void ExitApplication()
    {
        Debug.Log("[SettingsOverlay] Exit requested — restoring hidden windows and quitting.");

        // Safety: restore any windows the suppressor hid
        HiddenWindowTracker.RestoreAll();

        // Also stop the suppressor if it's running
        var suppressor = FindFirstObjectByType<WindowsPopupSuppressor>();
        if (suppressor != null)
            suppressor.StopSuppressing();

        // Restore window transparency/state before quitting
        WindowManager.MakeOpaque();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public bool IsOpen => _isOpen;
}
