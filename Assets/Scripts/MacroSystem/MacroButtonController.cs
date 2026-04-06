using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MacroButtonController : MonoBehaviour
{
    // ── Group definitions ───────────────────────────────────────────
    // Each group has a display name and exactly 3 action types.
    [System.Serializable]
    public struct MacroGroup
    {
        public string name;
        public MacroActionType action0;
        public MacroActionType action1;
        public MacroActionType action2;
    }

    private static readonly MacroGroup[] Groups = new[]
    {
        new MacroGroup { name = "Navigate",  action0 = MacroActionType.Back,          action1 = MacroActionType.Forward,       action2 = MacroActionType.Refresh },
        new MacroGroup { name = "Tabs",      action0 = MacroActionType.NewTab,        action1 = MacroActionType.CloseTab,      action2 = MacroActionType.None },
        new MacroGroup { name = "Read",      action0 = MacroActionType.ZoomIn,        action1 = MacroActionType.ZoomOut,       action2 = MacroActionType.Screenshot },
        new MacroGroup { name = "Scroll",    action0 = MacroActionType.PageUp,        action1 = MacroActionType.PageDown,      action2 = MacroActionType.None },
        new MacroGroup { name = "Snap",      action0 = MacroActionType.SnapLeft,      action1 = MacroActionType.SnapRight,     action2 = MacroActionType.MaximizeRestore },
        new MacroGroup { name = "Window",    action0 = MacroActionType.Minimize,      action1 = MacroActionType.CloseWindow,   action2 = MacroActionType.Undo },
        new MacroGroup { name = "Edit",      action0 = MacroActionType.Redo,          action1 = MacroActionType.MuteToggle,    action2 = MacroActionType.FindOnPage },
        new MacroGroup { name = "System",    action0 = MacroActionType.HomeDashboard, action1 = MacroActionType.AppCycler,     action2 = MacroActionType.Settings },
    };

    private const int SlotsPerPage = 3;

    // ── Inspector tunables ──────────────────────────────────────────
    [Header("Arc  (200 start, 340 end)")]
    [SerializeField] private float arcStartAngle = 200f;
    [SerializeField] private float arcEndAngle   = 340f;
    [SerializeField] private float radialRadius  = 10f;

    [Header("Carousel Animation")]
    [SerializeField] private float inDurationMs  = 300f;
    [SerializeField] private float outDurationMs = 180f;
    [SerializeField] private float staggerMs     = 60f;

    [Header("Group Label Animation")]
    [SerializeField] private float labelFadeInMs    = 250f;
    [SerializeField] private float labelHoldMs      = 900f;
    [SerializeField] private float labelFadeOutMs   = 350f;
    [SerializeField] private float labelOffsetY     = -55f;

    [Header("Button Name Tooltip")]
    [SerializeField] private float tooltipFadeInMs  = 200f;
    [SerializeField] private float tooltipHoldMs    = 800f;
    [SerializeField] private float tooltipFadeOutMs = 300f;
    [SerializeField] private float tooltipOffsetY   = -42f;
    [SerializeField] private float tooltipDelayMs   = 150f;

    [Header("Button Sizing")]
    [SerializeField] private float buttonSize   = 70f;
    [SerializeField] private float buttonMargin = 12f;
    [SerializeField] private float iconSize     = 34f;

    [Header("Wheel Arrow")]
    [SerializeField] private float wheelArrowSize = 44f;

    [Header("Container Layout")]
    [SerializeField] private float containerWidth        = 180f;
    [SerializeField] private float containerPaddingTB    = 16f;
    [SerializeField] private float containerPaddingLR    = 12f;
    [SerializeField] private float containerBorderRadius = 16f;

    // ── Runtime references ──────────────────────────────────────────
    private UIDocument      uiDocument;
    private VisualElement   macroContainer;
    private VisualElement   buttonGrid;
    private Label           groupLabel;
    private Button          prevBtn;
    private Button          nextBtn;
    private MacroButton[]   slotButtons = new MacroButton[SlotsPerPage];
    private IInputProvider  inputProvider;

    // ── State ───────────────────────────────────────────────────────
    private int       _groupIndex;
    private Vector2   anchorPanelPos;
    private bool      _open;
    private IVisualElementScheduledItem _labelFadeOutHandle;
    private Action[] _slotIconRefreshCbs = new Action[SlotsPerPage];
    private Label[]  _slotTooltips = new Label[SlotsPerPage];
    private IVisualElementScheduledItem[] _tooltipFadeHandles = new IVisualElementScheduledItem[SlotsPerPage];

    private float MacroHalfSize => buttonSize     * 0.5f;
    private float ArrowHalfSize => wheelArrowSize * 0.5f;

    // ── Lifecycle ───────────────────────────────────────────────────
    void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[MacroButtonController] UIDocument or root is null");
            return;
        }

        inputProvider = new PointerInputProvider();
        var root = uiDocument.rootVisualElement;
        macroContainer = root.Q<VisualElement>("macro-container");
        buttonGrid     = root.Q<VisualElement>("button-grid");
        groupLabel     = root.Q<Label>("group-name-label");

        // Grab the 3 reusable slot buttons
        for (int i = 0; i < SlotsPerPage; i++)
        {
            slotButtons[i] = root.Q<MacroButton>($"BtnSlot{i}");
            if (slotButtons[i] == null)
                Debug.LogWarning($"[MacroButtonController] BtnSlot{i} not found in UXML");
        }

        prevBtn = root.Q<Button>("BtnWheelPrev");
        nextBtn = root.Q<Button>("BtnWheelNext");
        if (prevBtn != null) prevBtn.clicked += OnPrevClicked;
        if (nextBtn != null) nextBtn.clicked += OnNextClicked;

        // Create per-slot tooltip labels
        for (int i = 0; i < SlotsPerPage; i++)
        {
            var tip = new Label();
            tip.AddToClassList("button-tooltip");
            tip.style.position = Position.Absolute;
            tip.style.opacity = 0f;
            tip.style.display = DisplayStyle.None;
            tip.pickingMode = PickingMode.Ignore;
            buttonGrid.Add(tip);
            _slotTooltips[i] = tip;
        }

        // Register hover callbacks so tooltips re-appear on mouse-over
        for (int i = 0; i < SlotsPerPage; i++)
        {
            int captured = i;
            slotButtons[i]?.RegisterCallback<PointerEnterEvent>(_ => OnSlotHoverEnter(captured));
            slotButtons[i]?.RegisterCallback<PointerLeaveEvent>(_ => OnSlotHoverLeave(captured));
        }

        ApplyLayout();
        HideImmediate();
        Debug.Log($"[MacroButtonController] Ready -- {Groups.Length} groups, {Groups.Length * SlotsPerPage} total macros");
    }

    void OnDisable()
    {
        for (int i = 0; i < SlotsPerPage; i++)
        {
            slotButtons[i]?.Unbind();
            if (slotButtons[i] != null && _slotIconRefreshCbs[i] != null)
            {
                slotButtons[i].clicked -= _slotIconRefreshCbs[i];
                _slotIconRefreshCbs[i] = null;
            }
        }
        if (prevBtn != null) prevBtn.clicked -= OnPrevClicked;
        if (nextBtn != null) nextBtn.clicked -= OnNextClicked;
    }

    // ── Public API ──────────────────────────────────────────────────

    public void ShowWithBounceAtWorldPosition(Vector3 worldPos, Vector2 panelOffset, Camera renderCamera = null)
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;
        _open = true;
        _groupIndex = 0;

        var panel = uiDocument.rootVisualElement.panel;
        anchorPanelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
            panel, worldPos, renderCamera != null ? renderCamera : Camera.main);
        anchorPanelPos += panelOffset;

        var panelSize = uiDocument.rootVisualElement.layout;
        float margin = radialRadius + buttonSize;
        anchorPanelPos.x = Mathf.Clamp(anchorPanelPos.x, margin, panelSize.width  - margin);
        anchorPanelPos.y = Mathf.Clamp(anchorPanelPos.y, margin, panelSize.height - margin);

        EnterRadialLayout();
        macroContainer.schedule.Execute(RevealPage).StartingIn(16);
    }

    public void HideWithShrink()
    {
        if (!_open) return;
        _open = false;
        HideArrows();
        CancelLabelFade();
        HideAllTooltips();

        for (int i = 0; i < SlotsPerPage; i++)
        {
            var btn = slotButtons[i];
            if (btn == null || btn.style.display == DisplayStyle.None) continue;
            ApplyTransitions(btn, 0, (long)outDurationMs);
            btn.style.scale   = new Scale(Vector2.zero);
            btn.style.opacity = 0f;
            btn.style.rotate  = new Rotate(new Angle(-1080f));
        }

        if (groupLabel != null) groupLabel.style.opacity = 0f;

        macroContainer?.schedule.Execute(() =>
        {
            if (!_open) macroContainer.style.display = DisplayStyle.None;
        }).StartingIn((long)outDurationMs + 40);
    }

    public void HideImmediate()
    {
        _open = false;
        CancelLabelFade();
        HideAllTooltips();
        for (int i = 0; i < SlotsPerPage; i++)
        {
            var btn = slotButtons[i];
            if (btn == null) continue;
            btn.Unbind();
            ClearTransitions(btn);
            btn.style.rotate  = new Rotate(new Angle(0f));
            btn.style.scale   = new Scale(Vector2.zero);
            btn.style.opacity = 0f;
            btn.style.display = DisplayStyle.None;
        }
        HideArrows();
        if (groupLabel != null) groupLabel.style.opacity = 0f;
        if (macroContainer != null) macroContainer.style.display = DisplayStyle.None;
    }

    // ── Radial layout ───────────────────────────────────────────────

    private void EnterRadialLayout()
    {
        if (macroContainer == null) return;

        macroContainer.style.position          = Position.Absolute;
        macroContainer.style.left              = anchorPanelPos.x;
        macroContainer.style.top               = anchorPanelPos.y;
        macroContainer.style.right             = StyleKeyword.Auto;
        macroContainer.style.width             = 0;
        macroContainer.style.height            = 0;
        macroContainer.style.paddingLeft       = 0;
        macroContainer.style.paddingRight      = 0;
        macroContainer.style.paddingTop        = 0;
        macroContainer.style.paddingBottom     = 0;
        macroContainer.style.backgroundColor   = new StyleColor(Color.clear);
        macroContainer.style.borderLeftWidth   = 0;
        macroContainer.style.borderRightWidth  = 0;
        macroContainer.style.borderTopWidth    = 0;
        macroContainer.style.borderBottomWidth = 0;
        macroContainer.style.overflow          = Overflow.Visible;
        macroContainer.style.display           = DisplayStyle.Flex;

        if (buttonGrid != null)
        {
            buttonGrid.style.position = Position.Absolute;
            buttonGrid.style.left = 0; buttonGrid.style.top = 0;
            buttonGrid.style.width = 0; buttonGrid.style.height = 0;
            buttonGrid.style.overflow = Overflow.Visible;
        }

        for (int i = 0; i < SlotsPerPage; i++)
        {
            var btn = slotButtons[i];
            if (btn == null) continue;
            btn.Unbind();
            ClearTransitions(btn);
            btn.style.position = Position.Absolute;
            btn.style.left     = -MacroHalfSize;
            btn.style.top      = -MacroHalfSize;
            btn.style.rotate   = new Rotate(new Angle(0f));
            btn.style.scale    = new Scale(Vector2.zero);
            btn.style.opacity  = 0f;
            btn.style.display  = DisplayStyle.None;
        }
        HideArrows();
        if (groupLabel != null)
        {
            groupLabel.style.position = Position.Absolute;
            groupLabel.style.opacity = 0f;
        }
    }

    // ── Arc geometry ────────────────────────────────────────────────

    private Vector2 ArcSlotOffset(int slot, float halfSize, float radiusOverride = -1f)
    {
        float r     = radiusOverride > 0f ? radiusOverride : radialRadius;
        float t     = slot / (float)(SlotsPerPage + 1);
        float angle = Mathf.Lerp(arcStartAngle, arcEndAngle, t) * Mathf.Deg2Rad;
        return new Vector2(
            Mathf.Cos(angle) * r - halfSize,
            Mathf.Sin(angle) * r - halfSize);
    }

    private Vector2 ContentOffset(int contentSlot) => ArcSlotOffset(contentSlot + 1, MacroHalfSize);

    // ── Bind group actions to the 3 slot buttons ────────────────────

    private void BindGroup(int groupIdx)
    {
        if (groupIdx < 0 || groupIdx >= Groups.Length) return;
        var group = Groups[groupIdx];
        MacroActionType[] actions = { group.action0, group.action1, group.action2 };

        for (int i = 0; i < SlotsPerPage; i++)
        {
            var btn = slotButtons[i];
            if (btn == null) continue;

            // Remove any mute-refresh callback left from a previous bind
            if (_slotIconRefreshCbs[i] != null)
            {
                btn.clicked -= _slotIconRefreshCbs[i];
                _slotIconRefreshCbs[i] = null;
            }

            btn.Unbind();

            // None = empty placeholder slot — hide it entirely
            if (actions[i] == MacroActionType.None)
            {
                btn.style.display = DisplayStyle.None;
                continue;
            }

            btn.Bind(MacroActionFactory.Create(actions[i]), inputProvider);
            SetButtonIcon(btn, actions[i]);

            // MuteToggle: the mute state changes on click, so refresh the icon 50 ms later
            if (actions[i] == MacroActionType.MuteToggle)
            {
                var capturedBtn  = btn;
                int capturedSlot = i;
                _slotIconRefreshCbs[capturedSlot] = () =>
                    capturedBtn.schedule.Execute(
                        () => SetButtonIcon(capturedBtn, MacroActionType.MuteToggle)
                    ).StartingIn(50);
                capturedBtn.clicked += _slotIconRefreshCbs[capturedSlot];
            }
        }
    }

    // ── Icon management ─────────────────────────────────────────────

    private string GetIconClass(MacroActionType type)
    {
        switch (type)
        {
            case MacroActionType.Back:            return "icon-back";
            case MacroActionType.Forward:         return "icon-forward";
            case MacroActionType.Refresh:         return "icon-refresh";
            case MacroActionType.NewTab:          return "icon-new-tab";
            case MacroActionType.CloseTab:        return "icon-close-tab";
            case MacroActionType.SwitchWindow:    return "icon-switch-window";
            case MacroActionType.ZoomIn:          return "icon-zoom-in";
            case MacroActionType.ZoomOut:         return "icon-zoom-out";
            case MacroActionType.Screenshot:      return "icon-screenshot";
            case MacroActionType.PageUp:          return "icon-page-up";
            case MacroActionType.PageDown:        return "icon-page-down";
            case MacroActionType.ReturnToDesktop: return "icon-home";
            case MacroActionType.SnapLeft:        return "icon-snap-left";
            case MacroActionType.SnapRight:       return "icon-snap-right";
            case MacroActionType.MaximizeRestore: return "icon-maximize";
            case MacroActionType.Minimize:        return "icon-minimize";
            case MacroActionType.CloseWindow:     return "icon-close-window";
            case MacroActionType.Undo:            return "icon-undo";
            case MacroActionType.Redo:            return "icon-redo";
            case MacroActionType.MuteToggle:      return Win32AudioInterop.GetMute() ? "icon-voice" : "icon-mute";
            case MacroActionType.FindOnPage:      return "icon-find";
            case MacroActionType.HomeDashboard:   return "icon-home";
            case MacroActionType.AppCycler:       return "icon-switch-window";
            case MacroActionType.Settings:        return "icon-settings";
            default:                              return null;
        }
    }

    private void SetButtonIcon(MacroButton btn, MacroActionType actionType)
    {
        var icon = btn.Q<VisualElement>(className: "btn-icon");
        if (icon == null) return;

        // Remove any existing icon-* classes
        var toRemove = new List<string>();
        foreach (var cls in icon.GetClasses())
            if (cls.StartsWith("icon-"))
                toRemove.Add(cls);
        foreach (var cls in toRemove)
            icon.RemoveFromClassList(cls);

        string iconClass = GetIconClass(actionType);
        if (string.IsNullOrEmpty(iconClass)) return;

        icon.AddToClassList(iconClass);

        // Flip horizontally for actions that mirror an existing icon asset
        bool flipX = actionType == MacroActionType.Forward || actionType == MacroActionType.SnapRight;
        icon.style.scale = new Scale(new Vector2(flipX ? -1f : 1f, 1f));
    }

    // ── Group label flash (rewritable -- cancels previous fade) ─────

    private void CancelLabelFade()
    {
        _labelFadeOutHandle = null;
    }

    private void FlashGroupName(string groupName)
    {
        if (groupLabel == null) return;

        // Cancel any in-progress animation (prevents text layering on fast scroll)
        CancelLabelFade();

        // Immediately rewrite text and reset opacity
        ClearTransitions(groupLabel);
        groupLabel.text = groupName;
        groupLabel.style.opacity = 0f;

        // Position above the arc centre
        groupLabel.style.position = Position.Absolute;
        groupLabel.style.left = -80f;
        groupLabel.style.top = labelOffsetY;
        groupLabel.style.width = 160f;

        // Fade in
        groupLabel.schedule.Execute(() =>
        {
            groupLabel.style.transitionProperty       = new List<StylePropertyName> { new("opacity") };
            groupLabel.style.transitionDuration       = new List<TimeValue> { new((long)labelFadeInMs, TimeUnit.Millisecond) };
            groupLabel.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOut) };
            groupLabel.style.transitionDelay          = new List<TimeValue> { new(0, TimeUnit.Millisecond) };
            groupLabel.style.opacity = 1f;
        }).StartingIn(16);

        // Schedule fade out after hold period
        IVisualElementScheduledItem handle = null;
        handle = groupLabel.schedule.Execute(() =>
        {
            // If a newer flash replaced us, don't fade out
            if (_labelFadeOutHandle != handle) return;

            groupLabel.style.transitionDuration = new List<TimeValue> { new((long)labelFadeOutMs, TimeUnit.Millisecond) };
            groupLabel.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseIn) };
            groupLabel.style.opacity = 0f;
        }).StartingIn((long)(labelFadeInMs + labelHoldMs));

        _labelFadeOutHandle = handle;
    }

    // ── Reveal current group ────────────────────────────────────────

    private void RevealPage()
    {
        if (Groups.Length == 0) return;

        BindGroup(_groupIndex);

        // Reset all slot buttons
        for (int i = 0; i < SlotsPerPage; i++)
        {
            var btn = slotButtons[i];
            if (btn == null) continue;
            ClearTransitions(btn);
            btn.style.left    = -MacroHalfSize;
            btn.style.top     = -MacroHalfSize;
            btn.style.rotate  = new Rotate(new Angle(0f));
            btn.style.scale   = new Scale(Vector2.zero);
            btn.style.opacity = 0f;
            btn.style.display = DisplayStyle.None;
        }

        // Show arrows if more than 1 group
        if (Groups.Length > 1)
        {
            PlaceArrow(prevBtn, 0);
            PlaceArrow(nextBtn, SlotsPerPage + 1);
        }

        // Fan buttons in with stagger
        var revealGroup = Groups[_groupIndex];
        MacroActionType[] revealActions = { revealGroup.action0, revealGroup.action1, revealGroup.action2 };
        for (int s = 0; s < SlotsPerPage; s++)
        {
            if (revealActions[s] == MacroActionType.None) continue;
            var btn  = slotButtons[s];
            if (btn == null) continue;
            Vector2 dest = ContentOffset(s);
            long    dly  = (long)(staggerMs * s);

            btn.style.left    = dest.x;
            btn.style.top     = dest.y;
            btn.style.rotate  = new Rotate(new Angle(1080f));
            btn.style.display = DisplayStyle.Flex;
            btn.schedule.Execute(() =>
            {
                ApplyTransitions(btn, dly, (long)inDurationMs);
                btn.style.scale   = new Scale(Vector2.one);
                btn.style.opacity = 1f;
                btn.style.rotate  = new Rotate(new Angle(0f));
            }).StartingIn(16);
        }

        // Show group name + per-button tooltips
        FlashGroupName(Groups[_groupIndex].name);
        FlashButtonTooltips();
    }

    // ── Page to next/previous group ─────────────────────────────────

    private void PageToGroup(int direction)
    {
        if (Groups.Length <= 1 || !_open) return;

        int n = Groups.Length;
        _groupIndex = ((_groupIndex + direction) % n + n) % n;

        bool goNext = direction > 0;

        // Hide tooltips immediately on page change
        HideAllTooltips();

        // EXIT: spin all 3 buttons out in place
        for (int i = 0; i < SlotsPerPage; i++)
        {
            var btn = slotButtons[i];
            if (btn == null) continue;

            ApplyTransitions(btn, 0, (long)outDurationMs);
            btn.style.scale   = new Scale(Vector2.zero);
            btn.style.opacity = 0f;
            btn.style.rotate  = new Rotate(new Angle(-1080f));
        }

        // After exit animation, rebind and enter new group
        macroContainer.schedule.Execute(() =>
        {
            if (!_open) return;

            BindGroup(_groupIndex);

            var pageGroup = Groups[_groupIndex];
            MacroActionType[] pageActions = { pageGroup.action0, pageGroup.action1, pageGroup.action2 };

            for (int i = 0; i < SlotsPerPage; i++)
            {
                var btn = slotButtons[i];
                if (btn == null || pageActions[i] == MacroActionType.None) continue;

                Vector2 entryDest = ContentOffset(i);

                ClearTransitions(btn);
                btn.style.position = Position.Absolute;
                btn.style.left     = entryDest.x;
                btn.style.top      = entryDest.y;
                btn.style.rotate   = new Rotate(new Angle(1080f));
                btn.style.scale    = new Scale(Vector2.zero);
                btn.style.opacity  = 0f;
                btn.style.display  = DisplayStyle.Flex;

                int captured = i;
                btn.schedule.Execute(() =>
                {
                    long dly = (long)(staggerMs * captured);
                    ApplyTransitions(slotButtons[captured], dly, (long)inDurationMs);
                    slotButtons[captured].style.rotate  = new Rotate(new Angle(0f));
                    slotButtons[captured].style.scale   = new Scale(Vector2.one);
                    slotButtons[captured].style.opacity = 1f;
                }).StartingIn(16);
            }

            FlashGroupName(Groups[_groupIndex].name);
            FlashButtonTooltips();
        }).StartingIn((long)outDurationMs + 20);
    }

    // ── Button tooltips ─────────────────────────────────────────────

    private void HideAllTooltips()
    {
        for (int i = 0; i < SlotsPerPage; i++)
        {
            _tooltipFadeHandles[i] = null;
            if (_slotTooltips[i] != null)
            {
                ClearTransitions(_slotTooltips[i]);
                _slotTooltips[i].style.opacity = 0f;
                _slotTooltips[i].style.display = DisplayStyle.None;
            }
        }
    }

    private void FlashButtonTooltips()
    {
        if (Groups.Length == 0) return;
        var group = Groups[_groupIndex];
        MacroActionType[] actions = { group.action0, group.action1, group.action2 };

        for (int s = 0; s < SlotsPerPage; s++)
        {
            var tip = _slotTooltips[s];
            if (tip == null) continue;

            // Cancel any running fade for this slot
            _tooltipFadeHandles[s] = null;

            // Skip placeholder slots
            if (actions[s] == MacroActionType.None)
            {
                tip.style.display = DisplayStyle.None;
                continue;
            }

            ClearTransitions(tip);
            tip.text = MacroActionFactory.Create(actions[s]).DisplayName;
            tip.style.opacity = 0f;

            // Position above the button's arc slot
            Vector2 btnPos = ContentOffset(s);
            tip.style.left = btnPos.x - 20f; // wider than button for centering
            tip.style.top  = btnPos.y + tooltipOffsetY;
            tip.style.width = buttonSize + 40f;
            tip.style.display = DisplayStyle.Flex;

            long slotDelay = (long)(tooltipDelayMs + staggerMs * s);
            int captured = s;

            // Fade in after staggered delay
            tip.schedule.Execute(() =>
            {
                var t = _slotTooltips[captured];
                t.style.transitionProperty       = new List<StylePropertyName> { new("opacity") };
                t.style.transitionDuration       = new List<TimeValue> { new((long)tooltipFadeInMs, TimeUnit.Millisecond) };
                t.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOut) };
                t.style.transitionDelay          = new List<TimeValue> { new(0, TimeUnit.Millisecond) };
                t.style.opacity = 1f;
            }).StartingIn(slotDelay);

            // Schedule fade out
            IVisualElementScheduledItem fadeHandle = null;
            fadeHandle = tip.schedule.Execute(() =>
            {
                if (_tooltipFadeHandles[captured] != fadeHandle) return;
                var t = _slotTooltips[captured];
                t.style.transitionDuration       = new List<TimeValue> { new((long)tooltipFadeOutMs, TimeUnit.Millisecond) };
                t.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseIn) };
                t.style.opacity = 0f;
            }).StartingIn(slotDelay + (long)(tooltipFadeInMs + tooltipHoldMs));

            _tooltipFadeHandles[s] = fadeHandle;
        }
    }

    private void OnSlotHoverEnter(int slot)
    {
        var tip = _slotTooltips[slot];
        if (tip == null || !_open) return;

        var group = Groups[_groupIndex];
        MacroActionType[] actions = { group.action0, group.action1, group.action2 };
        if (actions[slot] == MacroActionType.None) return;

        // Cancel any running fade-out for this slot to make sure animations dont conflict
        _tooltipFadeHandles[slot] = null;
        ClearTransitions(tip);

        // Refresh text and position (in case auto-dismiss already cleared it)
        tip.text = MacroActionFactory.Create(actions[slot]).DisplayName;
        Vector2 btnPos = ContentOffset(slot);
        tip.style.left  = btnPos.x - 20f;
        tip.style.top   = btnPos.y + tooltipOffsetY;
        tip.style.width = buttonSize + 40f;
        tip.style.display = DisplayStyle.Flex;

        tip.schedule.Execute(() =>
        {
            tip.style.transitionProperty       = new List<StylePropertyName> { new("opacity") };
            tip.style.transitionDuration       = new List<TimeValue> { new((long)tooltipFadeInMs, TimeUnit.Millisecond) };
            tip.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOut) };
            tip.style.transitionDelay          = new List<TimeValue> { new(0, TimeUnit.Millisecond) };
            tip.style.opacity = 1f;
        }).StartingIn(16);
    }

    private void OnSlotHoverLeave(int slot)
    {
        var tip = _slotTooltips[slot];
        if (tip == null) return;

        IVisualElementScheduledItem handle = null;
        handle = tip.schedule.Execute(() =>
        {
            if (_tooltipFadeHandles[slot] != handle) return;
            tip.style.transitionDuration       = new List<TimeValue> { new((long)tooltipFadeOutMs, TimeUnit.Millisecond) };
            tip.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseIn) };
            tip.style.opacity = 0f;
        }).StartingIn(50);

        _tooltipFadeHandles[slot] = handle;
    }

    // ── Arrows ──────────────────────────────────────────────────────

    private void PlaceArrow(Button arrow, int slot)
    {
        if (arrow == null) return;
        Vector2 pos = ArcSlotOffset(slot, ArrowHalfSize, radialRadius + (MacroHalfSize - ArrowHalfSize));
        arrow.style.position = Position.Absolute;
        arrow.style.left     = pos.x;
        arrow.style.top      = pos.y;
        if (slot == 0)
            arrow.style.scale = new Scale(new Vector2(-1f, 1f));
        else
            arrow.style.scale = new Scale(Vector2.one);
        arrow.style.rotate  = new Rotate(new Angle(0f));
        arrow.style.opacity = 1f;
        arrow.style.display = DisplayStyle.Flex;
    }

    private void HideArrows()
    {
        if (prevBtn != null) prevBtn.style.display = DisplayStyle.None;
        if (nextBtn != null) nextBtn.style.display = DisplayStyle.None;
    }

    // ── Paging callbacks ────────────────────────────────────────────

    private void OnPrevClicked() => PageToGroup(-1);
    private void OnNextClicked() => PageToGroup(+1);

    // ── Layout ──────────────────────────────────────────────────────

    private void ApplyLayout()
    {
        var doc  = uiDocument != null ? uiDocument : GetComponent<UIDocument>();
        if (doc == null) return;
        var root = doc.rootVisualElement;
        if (root == null) return;

        var container = root.Q<VisualElement>("macro-container");
        if (container != null)
        {
            container.style.width                    = containerWidth;
            container.style.paddingTop               = containerPaddingTB;
            container.style.paddingBottom            = containerPaddingTB;
            container.style.paddingLeft              = containerPaddingLR;
            container.style.paddingRight             = containerPaddingLR;
            container.style.borderTopLeftRadius      = containerBorderRadius;
            container.style.borderTopRightRadius     = containerBorderRadius;
            container.style.borderBottomLeftRadius   = containerBorderRadius;
            container.style.borderBottomRightRadius  = containerBorderRadius;
        }

        float half = MacroHalfSize;
        for (int i = 0; i < SlotsPerPage; i++)
        {
            var btn = slotButtons[i];
            if (btn == null) continue;
            btn.style.width               = buttonSize;
            btn.style.height              = buttonSize;
            btn.style.marginTop           = buttonMargin;
            btn.style.marginBottom        = buttonMargin;
            btn.style.marginLeft          = buttonMargin;
            btn.style.marginRight         = buttonMargin;
            btn.style.borderTopLeftRadius     = half;
            btn.style.borderTopRightRadius    = half;
            btn.style.borderBottomLeftRadius  = half;
            btn.style.borderBottomRightRadius = half;
            var icon = btn.Q<VisualElement>(className: "btn-icon");
            if (icon != null) { icon.style.width = iconSize; icon.style.height = iconSize; }
        }

        void SizeArrow(Button a)
        {
            if (a == null) return;
            float ah = ArrowHalfSize;
            a.style.width               = wheelArrowSize;
            a.style.height              = wheelArrowSize;
            a.style.borderTopLeftRadius     = ah;
            a.style.borderTopRightRadius    = ah;
            a.style.borderBottomLeftRadius  = ah;
            a.style.borderBottomRightRadius = ah;
        }
        SizeArrow(prevBtn != null ? prevBtn : root.Q<Button>("BtnWheelPrev"));
        SizeArrow(nextBtn != null ? nextBtn : root.Q<Button>("BtnWheelNext"));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        var saved = uiDocument;
        uiDocument = doc;
        ApplyLayout();
        uiDocument = saved;
    }
#endif

    // ── USS transitions ─────────────────────────────────────────────

    private void ApplyTransitions(VisualElement el, long delayMs, long durationMs)
    {
        el.style.transitionProperty       = new List<StylePropertyName>
            { new("left"), new("top"), new("scale"), new("opacity"), new("rotate") };
        el.style.transitionDuration       = new List<TimeValue> { new(durationMs, TimeUnit.Millisecond) };
        el.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOutCubic) };
        el.style.transitionDelay          = new List<TimeValue> { new(delayMs, TimeUnit.Millisecond) };
    }

    private static void ClearTransitions(VisualElement el)
    {
        el.style.transitionProperty       = StyleKeyword.Null;
        el.style.transitionDuration       = StyleKeyword.Null;
        el.style.transitionTimingFunction = StyleKeyword.Null;
        el.style.transitionDelay          = StyleKeyword.Null;
    }
}
