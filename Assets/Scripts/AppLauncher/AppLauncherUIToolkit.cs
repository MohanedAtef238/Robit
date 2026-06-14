using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

/// Self-contained App Launcher controller.
/// Owns its own UIDocument (AppLauncherView.uxml), creates/finds DesktopParser,
/// manages window state, and animates the launcher panel open/closed.
/// AppCyclerAction calls Toggle() — no HomePageController involvement.
[RequireComponent(typeof(UIDocument))]
public class AppLauncherUIToolkit : MonoBehaviour
{
    private const int   ItemsPerPage          = 8;
    private const float ExitStepDelay         = 0.03f;
    private const float ExitDuration          = 0.18f;
    private const float EnterStepDelay        = 0.045f;
    private const float PanelTransitionSeconds = 0.22f;

    /// Assign in Inspector: the DesktopCard.uxml template used for each app card.
    public VisualTreeAsset desktopCardTemplate;

    // ── Components ────────────────────────────────────────────────────────────
    private UIDocument   _uiDocument;
    private DesktopParser _parser;
    private Transparency  _transparency;

    // ── UI elements ───────────────────────────────────────────────────────────
    private VisualElement _docRoot;       // UIDocument.rootVisualElement  (display on/off)
    private VisualElement _launcherPanel; // launcher-panel                (animation classes)
    private VisualElement _cardsContainer;
    private Label         _statusText;
    private Label         _pageIndicator;
    private Button        _navLeft;
    private Button        _navRight;

    // ── State ─────────────────────────────────────────────────────────────────
    private List<ShortcutInfo> _shortcuts      = new();
    private IPaginationLogic   _pagination;
    private bool               _isTransitioning;
    private Coroutine          _spinnerCoroutine;
    private bool               _isOpen;
    private bool               _initialized;    // carousel is ready
    private bool               _ownedWindowState; // did we set acrylic/click-through?

    private static readonly string[] SpinnerFrames =
        { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };

    public bool IsOpen => _isOpen;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _uiDocument   = GetComponent<UIDocument>();
        _transparency = FindFirstObjectByType<Transparency>();
    }

    private IEnumerator Start()
    {
        // UIDocument populates rootVisualElement on its first frame.
        yield return null;

        _docRoot = _uiDocument?.rootVisualElement;
        if (_docRoot == null)
        {
            RobitLogger.LogError("[AppLauncherUIToolkit] UIDocument rootVisualElement is null.");
            yield break;
        }

        _launcherPanel = _docRoot.Q("launcher-panel");
        if (!BindUIElements()) yield break;

        // Start hidden — panel slides in when Toggle()/Open() is called.
        _docRoot.style.display = DisplayStyle.None;

        // Eagerly find or create DesktopParser and begin scanning in the background
        // so the carousel is ready by the time the user opens the launcher.
        _parser = FindFirstObjectByType<DesktopParser>();
        if (_parser == null)
        {
            var go = new GameObject("DesktopParser");
            _parser = go.AddComponent<DesktopParser>();
            RobitLogger.Log("[AppLauncherUIToolkit] Created DesktopParser.");
        }

        // Show spinner in status label while waiting
        _statusText.text  = SpinnerFrames[0] + "  Scanning shortcuts...";
        _spinnerCoroutine = StartCoroutine(AnimateSpinner());

        yield return StartCoroutine(WaitForParser());

        if (_spinnerCoroutine != null) { StopCoroutine(_spinnerCoroutine); _spinnerCoroutine = null; }

        if (!_parser.parsingComplete)
        {
            if (_statusText != null) _statusText.text = "ERROR: Desktop parsing timed out.";
            yield break;
        }

        _shortcuts = new List<ShortcutInfo>(_parser.shortcuts);
        RobitLogger.Log($"[AppLauncherUIToolkit] Ready — {_shortcuts.Count} shortcuts.");

        yield return StartCoroutine(BuildCarousel());
        _initialized = true;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// Toggle: closes if open, opens if closed. Called by AppCyclerAction.
    public void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    /// Show the launcher panel. Takes over window state if the home page is not open.
    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;

        // Only acquire window state when the home page isn't already managing it.
        var homeCtrl = FindFirstObjectByType<HomePageController>();
        if (homeCtrl == null || !homeCtrl.IsOpen)
        {
            WindowManager.SetAcrylicBlur(true);
            WindowManager.SetClickThrough(false);
            WindowManager.FocusWindow();
            _transparency?.PausePolling();
            _ownedWindowState = true;
        }

        StartCoroutine(ShowPanel());
    }

    /// Animate the launcher panel out and restore window state if we owned it.
    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        StartCoroutine(HidePanel());
    }

    // ── Panel animation ───────────────────────────────────────────────────────

    private IEnumerator ShowPanel()
    {
        _launcherPanel?.RemoveFromClassList("view-hidden");
        _launcherPanel?.RemoveFromClassList("view-visible");
        _docRoot.style.display = DisplayStyle.Flex;

        yield return null; // let display:flex resolve before triggering transition

        _launcherPanel?.AddToClassList("view-visible");
    }

    private IEnumerator HidePanel()
    {
        _launcherPanel?.RemoveFromClassList("view-visible");
        _launcherPanel?.AddToClassList("view-hidden");

        yield return new WaitForSeconds(PanelTransitionSeconds);

        _launcherPanel?.RemoveFromClassList("view-hidden");
        _docRoot.style.display = DisplayStyle.None;

        if (_ownedWindowState)
        {
            WindowManager.SetAcrylicBlur(false);
            _transparency?.ResumePolling();
            _ownedWindowState = false;
        }
    }

    // ── Binding ───────────────────────────────────────────────────────────────

    private bool BindUIElements()
    {
        _statusText     = _docRoot.Q<Label>("status-text");
        _cardsContainer = _docRoot.Q<VisualElement>("cards-container");
        _pageIndicator  = _docRoot.Q<Label>("page-indicator");
        _navLeft        = _docRoot.Q<Button>("nav-left");
        _navRight       = _docRoot.Q<Button>("nav-right");

        if (_statusText == null || _cardsContainer == null || _pageIndicator == null
            || _navLeft == null || _navRight == null)
        {
            RobitLogger.LogError("[AppLauncherUIToolkit] Missing required elements in AppLauncherView.uxml.");
            return false;
        }

        _navLeft.clicked  += OnNavLeft;
        _navRight.clicked += OnNavRight;
        _navLeft.SetEnabled(false);
        _navRight.SetEnabled(false);

        _pageIndicator.style.display = DisplayStyle.None;
        _pagination = new PaginationLogic(ItemsPerPage);
        return true;
    }

    // ── Parser ────────────────────────────────────────────────────────────────

    private IEnumerator WaitForParser()
    {
        const float timeout = 15f;
        float elapsed = 0f;
        while (!_parser.parsingComplete && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
    }

    private IEnumerator AnimateSpinner()
    {
        int frame = 0;
        while (true)
        {
            if (_statusText != null)
                _statusText.text = $"{SpinnerFrames[frame]}  Scanning shortcuts...";
            frame = (frame + 1) % SpinnerFrames.Length;
            yield return new WaitForSeconds(0.1f);
        }
    }

    // ── Carousel ──────────────────────────────────────────────────────────────

    private IEnumerator BuildCarousel()
    {
        if (_shortcuts.Count == 0)
        {
            if (_statusText != null)
                _statusText.text = "No shortcuts found on the desktop.";
            yield break;
        }

        _statusText.style.display    = DisplayStyle.None;
        _pageIndicator.style.display = DisplayStyle.Flex;
        _pagination.CurrentPage      = 0;
        _cardsContainer.Clear();

        var cards = CreateCards(_pagination.CurrentPage);
        foreach (var c in cards) { _cardsContainer.Add(c); c.AddToClassList("card-hidden"); }

        yield return null;

        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].RemoveFromClassList("card-hidden");
            cards[i].AddToClassList("card-visible");
            yield return new WaitForSeconds(EnterStepDelay);
        }

        RefreshNav();
    }

    private void TryChangePage(int delta)
    {
        if (_isTransitioning) return;
        if (!_pagination.CanChangePage(delta, _shortcuts.Count)) return;
        StartCoroutine(PageTransition(_pagination.CurrentPage + delta));
    }

    private IEnumerator PageTransition(int target)
    {
        _isTransitioning = true;
        _navLeft.SetEnabled(false);
        _navRight.SetEnabled(false);

        var existing = new List<VisualElement>();
        foreach (VisualElement c in _cardsContainer.Children()) existing.Add(c);

        for (int i = existing.Count - 1; i >= 0; i--)
        {
            existing[i].RemoveFromClassList("card-visible");
            existing[i].AddToClassList("card-hidden");
            yield return new WaitForSeconds(ExitStepDelay);
        }

        yield return new WaitForSeconds(ExitDuration);
        _cardsContainer.Clear();
        _pagination.CurrentPage = target;

        var next = CreateCards(_pagination.CurrentPage);
        foreach (var c in next) { _cardsContainer.Add(c); c.AddToClassList("card-hidden"); }

        RefreshNav();
        yield return null;

        for (int i = 0; i < next.Count; i++)
        {
            next[i].RemoveFromClassList("card-hidden");
            next[i].AddToClassList("card-visible");
            yield return new WaitForSeconds(EnterStepDelay);
        }

        _isTransitioning = false;
        RefreshNav();
    }

    private List<VisualElement> CreateCards(int page)
    {
        var result = new List<VisualElement>();
        if (desktopCardTemplate == null)
        {
            RobitLogger.LogError("[AppLauncherUIToolkit] desktopCardTemplate not assigned in Inspector.");
            return result;
        }

        var range = _pagination.GetPageRange(_shortcuts.Count, page);
        for (int i = range.start; i < range.end; i++)
        {
            var info = _shortcuts[i];
            var card = desktopCardTemplate.Instantiate();
            card.style.flexShrink = 0;

            var title = card.Q<Label>("title");
            if (title != null) title.text = info.Name.ToUpper();

            var icon = card.Q<VisualElement>("icon-container");
            if (icon != null)
            {
                var tex = info.Icon != null ? info.Icon : Resources.Load<Texture2D>("Icons/application");
                if (tex != null)
                {
                    icon.style.backgroundImage = new StyleBackground(tex);
                    icon.style.backgroundSize  = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
                }
            }

            var tracker = card.Q<VisualElement>("tracker");
            if (tracker != null)
                tracker.RegisterCallback<PointerDownEvent>(_ => OnCardClick(info.TargetPath, info.WorkingDirectory));

            var container = card.Q<VisualElement>("card-container");
            result.Add(container ?? card);
        }
        return result;
    }

    private void RefreshNav()
    {
        int total = _pagination.GetPageCount(_shortcuts.Count);
        _pageIndicator.text = $"{_pagination.CurrentPage + 1} / {total}";
        _navLeft.SetEnabled(!_isTransitioning && _pagination.CurrentPage > 0);
        _navRight.SetEnabled(!_isTransitioning && _pagination.CurrentPage < total - 1);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private void OnCardClick(string path, string workingDir)
    {
        RobitLogger.Log($"[AppLauncherUIToolkit] Launching: {path}");
        Close();
        AppLauncher.Instance?.LaunchApplication(path, workingDir);
    }

    private void OnNavLeft()  => TryChangePage(-1);
    private void OnNavRight() => TryChangePage(1);

    private void OnDestroy()
    {
        if (_navLeft  != null) _navLeft.clicked  -= OnNavLeft;
        if (_navRight != null) _navRight.clicked -= OnNavRight;
    }
}
