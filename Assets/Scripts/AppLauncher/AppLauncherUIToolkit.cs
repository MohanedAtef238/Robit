using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(UIDocument))]
public class AppLauncherUIToolkit : MonoBehaviour
{
    private const int ItemsPerPage = 12;
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
    private int currentPage;
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

    IEnumerator Start()
    {
        WindowManager.Initialize();

        // HomeScene needs an interactive (non-transparent) window
        // OverlayScene leaves it in click-through mode before loading us.
        WindowManager.SetClickThrough(false);
        WindowManager.SetAcrylicBlur(true);

        // Ensure the camera is transparent so we can see the Acrylic
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
        }

        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[AppLauncherUIToolkit] UIDocument or root is null");
            yield break;
        }

        rootParams = uiDocument.rootVisualElement;
        statusText = rootParams.Q<Label>("status-text");
        cardsContainer = rootParams.Q<VisualElement>("cards-container");
        pageIndicator = rootParams.Q<Label>("page-indicator");
        navLeft = rootParams.Q<Button>("nav-left");
        navRight = rootParams.Q<Button>("nav-right");

        if (statusText == null || cardsContainer == null || pageIndicator == null || navLeft == null || navRight == null)
        {
            Debug.LogError("[AppLauncherUIToolkit] UXML structure is missing required elements.");
            yield break;
        }

        navLeft.clicked += () => TryChangePage(-1);
        navRight.clicked += () => TryChangePage(1);
        navLeft.SetEnabled(false);
        navRight.SetEnabled(false);

        backBtn = rootParams.Q<Button>("back-btn");
        if (backBtn != null)
            backBtn.clicked += () => UnityEngine.SceneManagement.SceneManager.LoadScene("OverlayScene");
        pageIndicator.style.display = DisplayStyle.None;

        // Show the panel immediately so the user sees it loading
        Show();
        statusText.text = "⠋  Scanning shortcuts...";
        spinnerCoroutine = StartCoroutine(AnimateSpinner());

        if (desktopParser == null)
        {
            desktopParser = FindFirstObjectByType<DesktopParser>();
            if (desktopParser == null)
            {
                Debug.Log("[AppLauncherUIToolkit] DesktopParser not found in scene. Creating a new one automatically.");
                GameObject parserObj = new GameObject("DesktopParserInstance");
                desktopParser = parserObj.AddComponent<DesktopParser>();
            }
        }

        if (AppLauncher.Instance == null)
        {
            new GameObject("AppLauncher").AddComponent<AppLauncher>();
        }

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
            Debug.LogError("[AppLauncherUIToolkit] DesktopParser did not complete in time");
            yield break;
        }

        Debug.Log($"[AppLauncherUIToolkit] DesktopParser finished with {desktopParser.shortcuts.Count} shortcuts after {elapsed:F2}s");
        allShortcuts = new List<ShortcutInfo>(desktopParser.shortcuts);

        if (spinnerCoroutine != null) { StopCoroutine(spinnerCoroutine); spinnerCoroutine = null; }

        // Populate the carousel — panel already visible, stays visible.
        yield return StartCoroutine(InitializeCarousel());
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
            Debug.Log(statusText.text);
            yield break;
        }

        statusText.style.display = DisplayStyle.None;
        pageIndicator.style.display = DisplayStyle.Flex;
        currentPage = 0;
        cardsContainer.Clear();

        List<VisualElement> pageCards = CreatePageElements(currentPage);
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

        int targetPage = currentPage + delta;
        if (targetPage < 0 || targetPage >= GetPageCount())
        {
            return;
        }

        StartCoroutine(TransitionToPage(targetPage));
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
        currentPage = targetPage;

        List<VisualElement> nextCards = CreatePageElements(currentPage);
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
        int startIndex = pageIndex * ItemsPerPage;
        int endIndex = Mathf.Min(startIndex + ItemsPerPage, allShortcuts.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            ShortcutInfo shortcut = allShortcuts[i];

            if (desktopCardTemplate == null)
            {
                Debug.LogError("[AppLauncherUIToolkit] Desktop Card Template is not assigned.");
                break;
            }

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
                triggerTracker.RegisterCallback<ClickEvent>(_ =>
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
        int pageCount = GetPageCount();
        pageIndicator.text = $"{currentPage + 1} / {pageCount}";
        navLeft.SetEnabled(!isTransitioning && currentPage > 0);
        navRight.SetEnabled(!isTransitioning && currentPage < pageCount - 1);
    }

    private int GetPageCount()
    {
        return Mathf.Max(1, Mathf.CeilToInt(allShortcuts.Count / (float)ItemsPerPage));
    }

    void OnAppCardClick(string path, string workingDirectory)
    {
        Debug.Log($"[AppLauncherUIToolkit] Launching '{path}'");
        AppLauncher.Instance.LaunchApplication(path, workingDirectory);
    }
}
