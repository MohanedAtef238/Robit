using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(UIDocument))]
public class AppLauncherUIToolkit : MonoBehaviour
{
    private const int ItemsPerPage = 8;
    private const float ExitStepDelay = 0.03f;
    private const float ExitDuration = 0.18f;
    private const float EnterStepDelay = 0.045f;

    public DesktopParser desktopParser;
    public VisualTreeAsset desktopCardTemplate;

    private UIDocument uiDocument;
    private VisualElement rootParams;
    private VisualElement cardsContainer;
    private Label statusText;
    private Label pageIndicator;
    private Button navLeft;
    private Button navRight;
    private Button backBtn;

    private List<ShortcutInfo> allShortcuts = new List<ShortcutInfo>();
    private IPaginationLogic pagination;
    private bool isTransitioning;
    private bool panelVisible;
    private Coroutine spinnerCoroutine;

    private static readonly string[] SpinnerFrames =
        { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };

    // ── Public panel control ───────────────────────────────────────────────────

    public bool IsPanelVisible => panelVisible;

    public void Show()
    {
        if (rootParams == null) return;
        rootParams.style.display = DisplayStyle.Flex;
        panelVisible = true;
    }

    public void Hide()
    {
        if (rootParams == null) return;
        rootParams.style.display = DisplayStyle.None;
        panelVisible = false;
    }

    public void Toggle()
    {
        if (panelVisible) Hide(); else Show();
    }

    private void SetupWindow()
    {
        WindowManager.Initialize();
        WindowManager.SetAcrylicBlur(true);
        WindowManager.SetClickThrough(false);
        WindowManager.FocusWindow();
        FindFirstObjectByType<Transparency>()?.RefreshUIDocumentCache();

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
        }
    }

    private bool BindUIElements()
    {
        uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            RobitLogger.LogError("[AppLauncherUIToolkit] UIDocument or root is null");
            return false;
        }

        rootParams = uiDocument.rootVisualElement;
        statusText = rootParams.Q<Label>("status-text");
        cardsContainer = rootParams.Q<VisualElement>("cards-container");
        pageIndicator = rootParams.Q<Label>("page-indicator");
        navLeft = rootParams.Q<Button>("nav-left");
        navRight = rootParams.Q<Button>("nav-right");

        if (statusText == null || cardsContainer == null || pageIndicator == null || navLeft == null || navRight == null)
        {
            RobitLogger.LogError("[AppLauncherUIToolkit] UXML structure is missing required elements.");
            return false;
        }

        navLeft.clicked += OnNavLeftClicked;
        navRight.clicked += OnNavRightClicked;
        navLeft.SetEnabled(false);
        navRight.SetEnabled(false);

        backBtn = rootParams.Q<Button>("back-btn");
        if (backBtn != null)
            backBtn.RegisterCallback<PointerDownEvent>(OnBackClicked, TrickleDown.TrickleDown);

        pageIndicator.style.display = DisplayStyle.None;
        pagination = new PaginationLogic(ItemsPerPage);
        return true;
    }

    private void InitializeDesktopParser()
    {
        if (desktopParser == null)
        {
            desktopParser = FindFirstObjectByType<DesktopParser>();
            if (desktopParser == null)
            {
                RobitLogger.Log("[AppLauncherUIToolkit] DesktopParser not found in scene. Creating a new one automatically.");
                GameObject parserObj = new GameObject("DesktopParserInstance");
                desktopParser = parserObj.AddComponent<DesktopParser>();
            }
        }

        if (AppLauncher.Instance == null)
        {
            new GameObject("AppLauncher").AddComponent<AppLauncher>();
        }
    }

    private IEnumerator Start()
    {
        SetupWindow();

        // UIDocument populates its visual tree on the first layout pass, which
        // happens at the end of the first frame. Querying before that yields nulls
        // and BindUIElements returns false, silently aborting the entire coroutine.
        yield return null;

        if (!BindUIElements()) yield break;

        // Show the panel immediately so the user sees it loading
        Show();
        statusText.text = "⠋  Scanning shortcuts...";
        spinnerCoroutine = StartCoroutine(AnimateSpinner());

        InitializeDesktopParser();

        yield return StartCoroutine(WaitForDesktopParser());

        if (!desktopParser.parsingComplete) yield break;

        RobitLogger.Log($"[AppLauncherUIToolkit] DesktopParser finished with {desktopParser.shortcuts.Count} shortcuts");
        allShortcuts = new List<ShortcutInfo>(desktopParser.shortcuts);

        if (spinnerCoroutine != null) { StopCoroutine(spinnerCoroutine); spinnerCoroutine = null; }

        yield return StartCoroutine(InitializeCarousel());
    }

    private IEnumerator WaitForDesktopParser()
    {
        float timeout = 10f;
        float elapsed = 0f;

        while (!desktopParser.parsingComplete && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        if (!desktopParser.parsingComplete)
        {
            statusText.text = "ERROR: Desktop parsing timed out.";
            RobitLogger.LogError("[AppLauncherUIToolkit] DesktopParser did not complete in time");
        }
    }

    private IEnumerator AnimateSpinner()
    {
        int frame = 0;
        while (true)
        {
            if (statusText != null)
                statusText.text = $"{SpinnerFrames[frame]}  Scanning shortcuts...";
            frame = (frame + 1) % SpinnerFrames.Length;
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator InitializeCarousel()
    {
        if (allShortcuts.Count == 0)
        {
            statusText.text = "No shortcuts (.lnk files) were found on the User or Public desktops.";
            RobitLogger.Log(statusText.text);
            yield break;
        }

        statusText.style.display = DisplayStyle.None;
        pageIndicator.style.display = DisplayStyle.Flex;
        pagination.CurrentPage = 0;
        cardsContainer.Clear();

        List<VisualElement> pageCards = CreatePageElements(pagination.CurrentPage);
        for (int i = 0; i < pageCards.Count; i++)
        {
            cardsContainer.Add(pageCards[i]);
            pageCards[i].AddToClassList("card-hidden");
        }

        yield return null;

        for (int i = 0; i < pageCards.Count; i++)
        {
            pageCards[i].RemoveFromClassList("card-hidden");
            pageCards[i].AddToClassList("card-visible");
            yield return new WaitForSeconds(EnterStepDelay);
        }

        UpdateNavigation();
    }

    private void TryChangePage(int delta)
    {
        if (isTransitioning)
        {
            return;
        }

        if (!pagination.CanChangePage(delta, allShortcuts.Count))
        {
            return;
        }

        StartCoroutine(TransitionToPage(pagination.CurrentPage + delta));
    }

    private IEnumerator TransitionToPage(int targetPage)
    {
        isTransitioning = true;
        navLeft.SetEnabled(false);
        navRight.SetEnabled(false);

        List<VisualElement> existingCards = new List<VisualElement>();
        foreach (VisualElement child in cardsContainer.Children())
        {
            existingCards.Add(child);
        }

        for (int i = existingCards.Count - 1; i >= 0; i--)
        {
            existingCards[i].RemoveFromClassList("card-visible");
            existingCards[i].AddToClassList("card-hidden");
            yield return new WaitForSeconds(ExitStepDelay);
        }

        yield return new WaitForSeconds(ExitDuration);

        cardsContainer.Clear();
        pagination.CurrentPage = targetPage;

        List<VisualElement> nextCards = CreatePageElements(pagination.CurrentPage);
        for (int i = 0; i < nextCards.Count; i++)
        {
            cardsContainer.Add(nextCards[i]);
            nextCards[i].AddToClassList("card-hidden");
        }

        UpdateNavigation();
        yield return null;

        for (int i = 0; i < nextCards.Count; i++)
        {
            nextCards[i].RemoveFromClassList("card-hidden");
            nextCards[i].AddToClassList("card-visible");
            yield return new WaitForSeconds(EnterStepDelay);
        }

        isTransitioning = false;
        UpdateNavigation();
    }

    private List<VisualElement> CreatePageElements(int pageIndex)
    {
        List<VisualElement> pageCards = new List<VisualElement>();

        if (desktopCardTemplate == null)
        {
            RobitLogger.LogError("[AppLauncherUIToolkit] Desktop Card Template is not assigned.");
            return pageCards;
        }

        var range = pagination.GetPageRange(allShortcuts.Count, pageIndex);
        int startIndex = range.start;
        int endIndex = range.end;

        for (int i = startIndex; i < endIndex; i++)
        {
            ShortcutInfo shortcut = allShortcuts[i];

            TemplateContainer cardInstance = desktopCardTemplate.Instantiate();
            cardInstance.style.flexShrink = 0;

            Label titleObj = cardInstance.Q<Label>("title");
            if (titleObj != null)
            {
                titleObj.text = shortcut.Name.ToUpper();
            }

            VisualElement iconContainer = cardInstance.Q<VisualElement>("icon-container");
            if (iconContainer != null && shortcut.Icon != null)
            {
                iconContainer.style.backgroundImage = new StyleBackground(shortcut.Icon);
                iconContainer.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            }

            VisualElement triggerTracker = cardInstance.Q<VisualElement>("tracker");
            if (triggerTracker != null)
            {
                triggerTracker.RegisterCallback<PointerDownEvent>(_ =>
                {
                    OnAppCardClick(shortcut.TargetPath, shortcut.WorkingDirectory);
                });
            }

            VisualElement container = cardInstance.Q<VisualElement>("card-container");
            pageCards.Add(container ?? cardInstance);
        }

        return pageCards;
    }

    private void UpdateNavigation()
    {
        int pageCount = pagination.GetPageCount(allShortcuts.Count);
        pageIndicator.text = $"{pagination.CurrentPage + 1} / {pageCount}";
        navLeft.SetEnabled(!isTransitioning && pagination.CurrentPage > 0);
        navRight.SetEnabled(!isTransitioning && pagination.CurrentPage < pageCount - 1);
    }

    void OnAppCardClick(string path, string workingDirectory)
    {
        RobitLogger.Log($"[AppLauncherUIToolkit] Launching '{path}'");
        AppLauncher.Instance.LaunchApplication(path, workingDirectory);
    }

    private void OnNavLeftClicked() => TryChangePage(-1);
    private void OnNavRightClicked() => TryChangePage(1);
    private void OnBackClicked(PointerDownEvent evt)
    {
        WindowManager.SetAcrylicBlur(false);
        UnityEngine.SceneManagement.SceneManager.LoadScene("OverlayScene");
    }

    private void OnDestroy()
    {
        if (navLeft != null) navLeft.clicked -= OnNavLeftClicked;
        if (navRight != null) navRight.clicked -= OnNavRightClicked;
        if (backBtn != null) backBtn.UnregisterCallback<PointerDownEvent>(OnBackClicked, TrickleDown.TrickleDown);
    }
}

