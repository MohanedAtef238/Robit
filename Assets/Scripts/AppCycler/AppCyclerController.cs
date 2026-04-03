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
    [SerializeField] private float splitDurationMs = 300f;
    [SerializeField] private float labelFadeDelayMs = 100f;
    [SerializeField] private float mergeDurationMs = 250f;

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
    private enum CyclerState { Tucked, Peeked, Expanded }
    private CyclerState state = CyclerState.Tucked;

    private List<ShortcutInfo> shortcuts = new();
    private int selectedIndex;
    private bool shortcutsReady;

    // ── lifecycle ────────────────────────────────────────────────────

    IEnumerator Start()
    {
        // Try to grab cached shortcuts from AppLauncher (if MainScene already parsed them)
        if (AppLauncher.Instance != null && AppLauncher.Instance.CachedShortcuts.Count > 0)
        {
            shortcuts = AppLauncher.Instance.CachedShortcuts;
            shortcutsReady = true;
            Debug.Log($"[AppCycler] Loaded {shortcuts.Count} cached shortcuts from AppLauncher.");
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
        Debug.Log($"[AppCycler] Parsed {shortcuts.Count} desktop shortcuts.");
    }

    void OnEnable()
    {
        uiDoc = GetComponent<UIDocument>();
        var root = uiDoc.rootVisualElement;

        hitArea      = root.Q<VisualElement>("cycler-hit-area");
        dock         = root.Q<VisualElement>("cycler-dock");
        idleBtn      = root.Q<Button>("cycler-idle-btn");
        expanded     = root.Q<VisualElement>("cycler-expanded");
        leftBtn      = root.Q<Button>("cycler-left");
        rightBtn     = root.Q<Button>("cycler-right");
        labelBtn     = root.Q<Button>("cycler-label-btn");
        appNameLabel = root.Q<Label>("cycler-app-name");

        // Hover detection on the hit area — peek / tuck
        hitArea.RegisterCallback<PointerEnterEvent>(OnHitAreaEnter);
        hitArea.RegisterCallback<PointerLeaveEvent>(OnHitAreaLeave);

        // Idle circle click → split open
        idleBtn.clicked += OnIdleClicked;

        // Arrow clicks → cycle
        leftBtn.clicked  += () => CycleSelection(-1);
        rightBtn.clicked += () => CycleSelection(+1);

        // Center label click → switch to app and merge back
        labelBtn.clicked += OnLabelClicked;

        SetTucked();
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
            Debug.LogWarning("[AppCycler] Shortcuts not ready yet.");
            return;
        }

        selectedIndex = 0;
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
        if (state == CyclerState.Tucked)
            Peek();
    }

    private void OnHitAreaLeave(PointerLeaveEvent _)
    {
        // Only tuck if we haven't expanded yet
        if (state == CyclerState.Peeked)
            SetTucked();
    }

    private void SetTucked()
    {
        state = CyclerState.Tucked;
        dock.RemoveFromClassList("cycler-dock--peeked");
        dock.AddToClassList("cycler-dock--tucked");
        // Make sure expanded is hidden and idle is visible
        CollapseExpanded();
        idleBtn.style.display = DisplayStyle.Flex;
        idleBtn.style.scale = new Scale(Vector2.one);
        idleBtn.style.opacity = 1f;
    }

    private void Peek()
    {
        state = CyclerState.Peeked;
        dock.RemoveFromClassList("cycler-dock--tucked");
        dock.AddToClassList("cycler-dock--peeked");
    }

    // ── idle → split ────────────────────────────────────────────────

    private void OnIdleClicked()
    {
        if (!shortcutsReady || shortcuts.Count == 0)
        {
            Debug.LogWarning("[AppCycler] No desktop shortcuts available yet.");
            return;
        }

        selectedIndex = 0;
        state = CyclerState.Expanded;

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
        appNameLabel.text = shortcuts[selectedIndex].Name;

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
        selectedIndex = ((selectedIndex + direction) % shortcuts.Count + shortcuts.Count) % shortcuts.Count;
        appNameLabel.text = shortcuts[selectedIndex].Name;
        Debug.Log($"[AppCycler] Selected: {shortcuts[selectedIndex].Name}");
    }

    // ── center click → switch & merge ───────────────────────────────

    private void OnLabelClicked()
    {
        if (shortcuts.Count == 0) return;

        var selected = shortcuts[selectedIndex];
        UnityEngine.Debug.Log($"[AppCycler] Launching: {selected.Name} at {selected.TargetPath}");
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
                UnityEngine.Debug.LogError($"[AppCycler] Failed to launch '{selected.Name}': {e.Message}");
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
                state = CyclerState.Peeked;
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
