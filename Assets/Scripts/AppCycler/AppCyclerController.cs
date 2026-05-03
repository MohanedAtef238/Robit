using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

/// <summary>
/// Controls the App Cycler dock: a bottom-center circle that peeks on hover,
/// splits into left/right arrows on click, and lets the user cycle through
/// desktop app shortcuts. Clicking the center app name launches it.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class AppCyclerController : MonoBehaviour
{
    // ── serialised tunables ──────────────────────────────────────────
    [Header("Peek Behaviour")]
    [Tooltip("How far down the dock is pushed when tucked (px). " +
             "Should be ~half the circle so only the top peeks out.")]

    [Header("Animation")]
    [SerializeField] private float splitDurationMs  = 300f;
    [SerializeField] private float labelFadeDelayMs = 100f;
    [SerializeField] private float mergeDurationMs  = 250f;
    [SerializeField] private float peekedScale      = 1.6f;

    // ── runtime refs ─────────────────────────────────────────────────
    private UIDocument    uiDoc;
    private VisualElement hitArea;
    private VisualElement dock;
    private Button        idleBtn;
    private VisualElement expanded;
    private Button        leftBtn;
    private Button        rightBtn;
    private Button        labelBtn;
    private Label         appNameLabel;

    // ── state ────────────────────────────────────────────────────────
    private List<ShortcutInfo> shortcuts = new();
    private ICyclerLogic cyclerLogic;
    private ICyclerStateManager stateManager;
    private bool shortcutsReady;

    // ── lifecycle ────────────────────────────────────────────────────

    IEnumerator Start()
    {
        // Try to grab cached shortcuts from AppLauncher (if MainScene already parsed them)
        if (AppLauncher.Instance != null && AppLauncher.Instance.CachedShortcuts.Count > 0)
        {
            shortcuts = AppLauncher.Instance.CachedShortcuts;
            shortcutsReady = true;
            RobitLogger.Log($"[AppCycler] Loaded {shortcuts.Count} cached shortcuts from AppLauncher.");
            yield break;
        }

        // Otherwise, parse desktop shortcuts ourselves
        var parser = FindFirstObjectByType<DesktopParser>();
        if (parser == null)
        {
            var go = new GameObject("DesktopParser_Cycler");
            parser = go.AddComponent<DesktopParser>();
        }

        float timeout = 15f;
        float elapsed = 0f;
        while (!parser.parsingComplete && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        if (parser.parsingComplete && parser.shortcuts.Count > 0)
        {
            shortcuts = parser.shortcuts;
            // Also cache them on AppLauncher if it exists
            if (AppLauncher.Instance != null)
                AppLauncher.Instance.CachedShortcuts = shortcuts;
        }

        shortcutsReady = true;
        RobitLogger.Log($"[AppCycler] Parsed {shortcuts.Count} desktop shortcuts.");
    }

    void OnEnable()
    {
        uiDoc = GetComponent<UIDocument>();
        if (uiDoc == null) { RobitLogger.LogError("[AppCycler] UIDocument component missing."); return; }

        // rootVisualElement is never null, but its UXML children are only populated after
        // UIDocument.OnEnable() runs. If Script Execution Order puts us before UIDocument,
        // the Q<> calls below would all return null. Defer one frame in that case.
        if (uiDoc.rootVisualElement?.Q<VisualElement>("cycler-hit-area") != null)
            BindUI();
        else
            StartCoroutine(BindWhenReady());
    }

    private System.Collections.IEnumerator BindWhenReady()
    {
        yield return null; // wait one frame
        if (uiDoc != null && uiDoc.rootVisualElement != null)
            BindUI();
        else
            RobitLogger.LogError("[AppCycler] UIDocument root still null after one frame — callbacks not registered.");
    }

    private void BindUI()
    {
        // Initialise logic objects FIRST so they are never null if callback registration
        // throws partway through (preventing Open() / OnIdleClicked() NullReferenceExceptions).
        cyclerLogic  = new CyclerLogic();
        stateManager = new CyclerStateManager();

        var root = uiDoc.rootVisualElement;

        hitArea      = root.Q<VisualElement>("cycler-hit-area");
        dock         = root.Q<VisualElement>("cycler-dock");
        idleBtn      = root.Q<Button>("cycler-idle-btn");
        expanded     = root.Q<VisualElement>("cycler-expanded");
        leftBtn      = root.Q<Button>("cycler-left");
        rightBtn     = root.Q<Button>("cycler-right");
        labelBtn     = root.Q<Button>("cycler-label-btn");
        appNameLabel = root.Q<Label>("cycler-app-name");

        // Fail fast with a clear message so Unity console shows exactly what is missing.
        if (hitArea == null || dock == null || idleBtn == null || expanded == null ||
            leftBtn == null || rightBtn == null || labelBtn == null || appNameLabel == null)
        {
            RobitLogger.LogError($"[AppCycler] BindUI failed — UXML element(s) missing: " +
                $"hitArea={hitArea != null}, dock={dock != null}, idleBtn={idleBtn != null}, " +
                $"expanded={expanded != null}, leftBtn={leftBtn != null}, rightBtn={rightBtn != null}, " +
                $"labelBtn={labelBtn != null}, appNameLabel={appNameLabel != null}");
            return;
        }

        RobitLogger.Log("[AppCycler] BindUI — all elements found, registering callbacks.");

        // Hover detection on the hit area — peek / tuck
        hitArea.RegisterCallback<PointerEnterEvent>(OnHitAreaEnter);
        hitArea.RegisterCallback<PointerLeaveEvent>(OnHitAreaLeave);

        // Use Button.clicked (full press+release gesture) — this matches the pre-HEAD behaviour
        // that worked reliably.  PointerDownEvent fires on press alone, which in a click-through
        // overlay can race with the WS_EX_TRANSPARENT toggle and miss the delivery window.
        idleBtn.clicked  += OnIdleClicked;
        leftBtn.clicked  += () => CycleSelection(-1);
        rightBtn.clicked += () => CycleSelection(+1);
        labelBtn.clicked += OnLabelClicked;

        // Clicking the dock/expanded background while open collapses the cycler.
        // evt.target check ensures arrow/label click bubbles do NOT accidentally collapse.
        dock.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (stateManager.CurrentState != CyclerState.Expanded) return;
            var t = evt.target as VisualElement;
            if (t == dock || t == expanded)
                MergeToIdle();
        });

        // UXML already sets the correct initial visual state (tucked class, expanded hidden).
        // SetTucked() is intentionally NOT called here because the state machine starts in
        // Tucked and TryTransition(Tucked→Tucked) fails, returning early and skipping visuals.
        FindFirstObjectByType<Transparency>()?.RefreshUIDocumentCache();
    }

    void OnDisable()
    {
        if (hitArea != null)
        {
            hitArea.UnregisterCallback<PointerEnterEvent>(OnHitAreaEnter);
            hitArea.UnregisterCallback<PointerLeaveEvent>(OnHitAreaLeave);
        }
    }

    // ── public API (called by AppCyclerAction) ──────────────────────

    /// Load the cached desktop shortcuts and open the cycler.
    public void Open()
    {
        if (!shortcutsReady || shortcuts.Count == 0)
        {
            RobitLogger.LogWarning("[AppCycler] Shortcuts not ready yet.");
            return;
        }

        cyclerLogic.Reset();
        Peek();
        OnIdleClicked();
    }

    /// Collapse to idle and tuck.
    public void Close()
    {
        MergeToIdle();
    }

    // ── hover peek / tuck ───────────────────────────────────────────

    private void OnHitAreaEnter(PointerEnterEvent _)
    {
        if (stateManager.CurrentState == CyclerState.Tucked)
            Peek();
    }

    private void OnHitAreaLeave(PointerLeaveEvent _)
    {
        // Only tuck if we haven't expanded yet
        if (stateManager.CurrentState == CyclerState.Peeked)
            SetTucked();
    }

    private void SetTucked()
    {
        if (!stateManager.TryTransition(CyclerState.Tucked)) return;
        dock.RemoveFromClassList("cycler-dock--peeked");
        dock.AddToClassList("cycler-dock--tucked");
        // Animate scale back to normal
        dock.style.scale = new Scale(Vector2.one);
        // Make sure expanded is hidden and idle is visible
        CollapseExpanded();
        idleBtn.style.display = DisplayStyle.Flex;
        idleBtn.style.scale = new Scale(Vector2.one);
        idleBtn.style.opacity = 1f;
    }

    private void Peek()
    {
        if (!stateManager.TryTransition(CyclerState.Peeked)) return;
        dock.RemoveFromClassList("cycler-dock--tucked");
        dock.AddToClassList("cycler-dock--peeked");
        // Animate scale up
        dock.style.scale = new Scale(new Vector2(peekedScale, peekedScale));
    }

    // ── idle → split ────────────────────────────────────────────────

    private void OnIdleClicked()
    {
        if (!shortcutsReady || shortcuts.Count == 0)
        {
            RobitLogger.LogWarning("[AppCycler] No desktop shortcuts available yet.");
            return;
        }

        // If the user tapped directly without a prior hover (e.g. the window was
        // click-through until this very frame), PointerEnterEvent may not have fired
        // and the state machine is still Tucked.  Bridge through Peeked automatically
        // so the Tucked → Peeked → Expanded path always succeeds on a direct click.
        if (stateManager.CurrentState == CyclerState.Tucked)
            Peek();

        cyclerLogic.Reset();
        if (!stateManager.TryTransition(CyclerState.Expanded)) return;

        // Ensure peeked so the dock is fully visible
        dock.RemoveFromClassList("cycler-dock--tucked");
        dock.AddToClassList("cycler-dock--peeked");

        // Hide idle circle
        TransitionElement(idleBtn, splitDurationMs);
        idleBtn.style.scale = new Scale(Vector2.zero);
        idleBtn.style.opacity = 0f;
        idleBtn.schedule.Execute(() => idleBtn.style.display = DisplayStyle.None)
            .StartingIn((long)splitDurationMs + 20);

        // Show expanded container
        expanded.style.display = DisplayStyle.Flex;

        // Start arrows in collapsed state, then split
        leftBtn.RemoveFromClassList("cycler-arrow--split");
        leftBtn.AddToClassList("cycler-arrow--collapsed");
        rightBtn.RemoveFromClassList("cycler-arrow--split");
        rightBtn.AddToClassList("cycler-arrow--collapsed");

        // Label starts hidden
        labelBtn.RemoveFromClassList("cycler-label-container--visible");
        labelBtn.AddToClassList("cycler-label-container--hidden");
        appNameLabel.text = shortcuts[cyclerLogic.SelectedIndex].Name;

        // Trigger split after one frame so transitions pick up the class change
        expanded.schedule.Execute(() =>
        {
            leftBtn.RemoveFromClassList("cycler-arrow--collapsed");
            leftBtn.AddToClassList("cycler-arrow--split");

            rightBtn.RemoveFromClassList("cycler-arrow--collapsed");
            rightBtn.AddToClassList("cycler-arrow--split");
        }).StartingIn(16);

        // Fade in label after a short delay
        expanded.schedule.Execute(() =>
        {
            labelBtn.RemoveFromClassList("cycler-label-container--hidden");
            labelBtn.AddToClassList("cycler-label-container--visible");
        }).StartingIn((long)labelFadeDelayMs);
    }

    // ── cycling ─────────────────────────────────────────────────────

    private void CycleSelection(int direction)
    {
        if (shortcuts.Count == 0) return;
        cyclerLogic.Cycle(direction, shortcuts.Count);
        appNameLabel.text = shortcuts[cyclerLogic.SelectedIndex].Name;
        RobitLogger.Log($"[AppCycler] Selected: {shortcuts[cyclerLogic.SelectedIndex].Name}");
    }

    // ── center click → switch & merge ───────────────────────────────

    private void OnLabelClicked()
    {
        if (shortcuts.Count == 0) return;

        var selected = shortcuts[cyclerLogic.SelectedIndex];
        RobitLogger.Log($"[AppCycler] Launching: {selected.Name} at {selected.TargetPath}");
        MergeToIdle();

        if (AppLauncher.Instance != null)
        {
            AppLauncher.Instance.LaunchApplication(selected.TargetPath, selected.WorkingDirectory);
        }
        else
        {
            // Fallback: launch the process directly when AppLauncher isn't available
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo(selected.TargetPath);
                if (!string.IsNullOrEmpty(selected.WorkingDirectory) && System.IO.Directory.Exists(selected.WorkingDirectory))
                    startInfo.WorkingDirectory = selected.WorkingDirectory;
                System.Diagnostics.Process.Start(startInfo);
            }
            catch (System.Exception e)
            {
                RobitLogger.LogError($"[AppCycler] Failed to launch '{selected.Name}': {e.Message}");
            }
        }
    }

    // ── merge (reverse split) ───────────────────────────────────────

    private void MergeToIdle()
    {
        // Collapse arrows back to center
        leftBtn.RemoveFromClassList("cycler-arrow--split");
        leftBtn.AddToClassList("cycler-arrow--collapsed");

        rightBtn.RemoveFromClassList("cycler-arrow--split");
        rightBtn.AddToClassList("cycler-arrow--collapsed");

        // Fade out label
        labelBtn.RemoveFromClassList("cycler-label-container--visible");
        labelBtn.AddToClassList("cycler-label-container--hidden");

        // After merge animation completes, show idle circle and tuck
        expanded.schedule.Execute(() =>
        {
            CollapseExpanded();

            idleBtn.style.display = DisplayStyle.Flex;
            // Start at zero, then animate in
            ClearTransitions(idleBtn);
            idleBtn.style.scale = new Scale(Vector2.zero);
            idleBtn.style.opacity = 0f;

            idleBtn.schedule.Execute(() =>
            {
                TransitionElement(idleBtn, splitDurationMs);
                idleBtn.style.scale = new Scale(Vector2.one);
                idleBtn.style.opacity = 1f;
                stateManager.TryTransition(CyclerState.Peeked);
            }).StartingIn(16);
        }).StartingIn((long)mergeDurationMs);
    }

    private void CollapseExpanded()
    {
        if (expanded != null) expanded.style.display = DisplayStyle.None;
        leftBtn?.RemoveFromClassList("cycler-arrow--split");
        leftBtn?.AddToClassList("cycler-arrow--collapsed");
        rightBtn?.RemoveFromClassList("cycler-arrow--split");
        rightBtn?.AddToClassList("cycler-arrow--collapsed");
        labelBtn?.RemoveFromClassList("cycler-label-container--visible");
        labelBtn?.AddToClassList("cycler-label-container--hidden");
    }

    // ── UITK transition helpers ─────────────────────────────────────

    private static void TransitionElement(VisualElement el, float durationMs)
    {
        el.style.transitionProperty = new List<StylePropertyName>
            { new("scale"), new("opacity") };
        el.style.transitionDuration = new List<TimeValue>
            { new(durationMs, TimeUnit.Millisecond) };
        el.style.transitionTimingFunction = new List<EasingFunction>
            { new(EasingMode.EaseOutCubic) };
        el.style.transitionDelay = new List<TimeValue>
            { new(0, TimeUnit.Millisecond) };
    }

    private static void ClearTransitions(VisualElement el)
    {
        el.style.transitionProperty       = StyleKeyword.Null;
        el.style.transitionDuration       = StyleKeyword.Null;
        el.style.transitionTimingFunction = StyleKeyword.Null;
        el.style.transitionDelay          = StyleKeyword.Null;
    }
}

