using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MacroButtonController : MonoBehaviour
{
    private const int SlotsPerPage = 3;

    // ── Inspector tunables ──────────────────────────────────────────
    [Header("Arc  (200 start, 340 end)")]
    [SerializeField] private float arcStartAngle = 200f;
    [SerializeField] private float arcEndAngle = 340f;
    [SerializeField] private float radialRadius = 10f;

    [Header("Carousel Animation")]
    [SerializeField] private float outDurationMs = 180f;
    [SerializeField] private float staggerMs = 60f;

    [Header("Group Label Animation")]
    [SerializeField] private float labelFadeInMs = 250f;
    [SerializeField] private float labelHoldMs = 900f;
    [SerializeField] private float labelOffsetY = -55f;

    [Header("Button Name Tooltip")]
    [SerializeField] private float tooltipHoldMs = 800f;
    [SerializeField] private float tooltipDelayMs = 150f;

    [Header("Button Sizing")]
    [SerializeField] private float buttonSize = 70f;
    [SerializeField] private float buttonMargin = 12f;
    [SerializeField] private float iconSize = 34f;

    [Header("Wheel Arrow")]
    [SerializeField] private float wheelArrowSize = 44f;

    [Header("Container Layout")]
    [SerializeField] private float containerWidth = 180f;
    [SerializeField] private float containerPaddingTB = 16f;
    [SerializeField] private float containerPaddingLR = 12f;
    [SerializeField] private float containerBorderRadius = 16f;

    // ── Runtime references ──────────────────────────────────────────
    private UIDocument uiDocument;
    private VisualElement macroContainer;
    private VisualElement buttonGrid;
    private Label groupLabel;
    private Button prevBtn;
    private Button nextBtn;
    private MacroButton[] slotButtons = new MacroButton[SlotsPerPage];
    private IInputProvider inputProvider;

    private MacroViewModel viewModel;

    // ── State ───────────────────────────────────────────────────────
    private Vector2 anchorPanelPos;
    private IVisualElementScheduledItem _labelHideSchedule;
    private Action[] _slotIconRefreshCbs = new Action[SlotsPerPage];
    private Label[] _slotTooltips = new Label[SlotsPerPage];
    private IVisualElementScheduledItem[] _tooltipHideSchedules = new IVisualElementScheduledItem[SlotsPerPage];

    private float MacroHalfSize => buttonSize * 0.5f;
    private float ArrowHalfSize => wheelArrowSize * 0.5f;

    // ── Lifecycle ───────────────────────────────────────────────────
    void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        inputProvider = new PointerInputProvider();
        var root = uiDocument.rootVisualElement;
        macroContainer = root.Q<VisualElement>("macro-container");
        buttonGrid = root.Q<VisualElement>("button-grid");
        groupLabel = root.Q<Label>("group-name-label");

        for (int i = 0; i < SlotsPerPage; i++)
        {
            slotButtons[i] = root.Q<MacroButton>($"BtnSlot{i}");
        }

        prevBtn = root.Q<Button>("BtnWheelPrev");
        nextBtn = root.Q<Button>("BtnWheelNext");

        if (prevBtn != null) prevBtn.clicked += () => viewModel?.PrevGroup();
        if (nextBtn != null) nextBtn.clicked += () => viewModel?.NextGroup();

        for (int i = 0; i < SlotsPerPage; i++)
        {
            var tip = new Label();
            tip.AddToClassList("button-tooltip");
            tip.AddToClassList("label-state--hidden");
            tip.style.position = Position.Absolute;
            tip.style.display = DisplayStyle.None;
            tip.pickingMode = PickingMode.Ignore;
            buttonGrid.Add(tip);
            _slotTooltips[i] = tip;
            
            int captured = i;
            slotButtons[i]?.RegisterCallback<PointerEnterEvent>(_ => OnSlotHoverEnter(captured));
            slotButtons[i]?.RegisterCallback<PointerLeaveEvent>(_ => OnSlotHoverLeave(captured));
        }

        viewModel = new MacroViewModel();
        viewModel.OnMenuToggled += OnMenuToggled;
        viewModel.OnGroupChanged += OnGroupChanged;

        ApplyLayout();
        HideImmediate();
    }

    void OnDisable()
    {
        if (viewModel != null)
        {
            viewModel.OnMenuToggled -= OnMenuToggled;
            viewModel.OnGroupChanged -= OnGroupChanged;
        }

        _labelHideSchedule?.Pause();
        foreach (var h in _tooltipHideSchedules) h?.Pause();

        for (int i = 0; i < SlotsPerPage; i++)
        {
            slotButtons[i]?.Unbind();
            if (slotButtons[i] != null && _slotIconRefreshCbs[i] != null)
            {
                slotButtons[i].clicked -= _slotIconRefreshCbs[i];
                _slotIconRefreshCbs[i] = null;
            }
        }
    }

    // ── Public API ──────────────────────────────────────────────────

    public void ShowWithBounceAtWorldPosition(Vector3 worldPos, Vector2 panelOffset, Camera renderCamera = null)
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null || viewModel == null) return;

        var panel = uiDocument.rootVisualElement.panel;
        anchorPanelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
            panel, worldPos, renderCamera != null ? renderCamera : Camera.main);
        anchorPanelPos += panelOffset;

        var panelSize = uiDocument.rootVisualElement.layout;
        float margin = radialRadius + buttonSize;
        anchorPanelPos.x = Mathf.Clamp(anchorPanelPos.x, margin, panelSize.width - margin);
        anchorPanelPos.y = Mathf.Clamp(anchorPanelPos.y, margin, panelSize.height - margin);

        viewModel.Open();
    }

    public void HideWithShrink()
    {
        viewModel?.Close();
    }

    private void OnMenuToggled(bool isOpen)
    {
        if (isOpen)
        {
            EnterRadialLayout();
            macroContainer.schedule.Execute(() => RevealGroup(viewModel.GetCurrentGroup())).StartingIn(16);
        }
        else
        {
            HideArrows();
            _labelHideSchedule?.Pause();
            HideAllTooltips();

            for (int i = 0; i < SlotsPerPage; i++)
            {
                var btn = slotButtons[i];
                if (btn == null || btn.style.display == DisplayStyle.None) continue;
                
                btn.style.transitionDelay = new List<TimeValue> { new(0, TimeUnit.Millisecond) };
                btn.RemoveFromClassList("macro-state--visible");
                btn.AddToClassList("macro-state--hidden");
            }

            if (groupLabel != null) 
            {
                groupLabel.RemoveFromClassList("label-state--visible");
                groupLabel.AddToClassList("label-state--hidden");
            }

            macroContainer?.schedule.Execute(() =>
            {
                if (!viewModel.IsOpen) macroContainer.style.display = DisplayStyle.None;
            }).StartingIn((long)outDurationMs + 40);
        }
    }

    private void OnGroupChanged(int groupIndex)
    {
        HideAllTooltips();

        for (int i = 0; i < SlotsPerPage; i++)
        {
            var btn = slotButtons[i];
            if (btn == null || btn.style.display == DisplayStyle.None) continue;
            
            btn.style.transitionDelay = new List<TimeValue> { new(0, TimeUnit.Millisecond) };
            btn.RemoveFromClassList("macro-state--visible");
            btn.AddToClassList("macro-state--hidden");
        }

        macroContainer.schedule.Execute(() =>
        {
            if (viewModel != null && viewModel.IsOpen)
            {
                RevealGroup(viewModel.GetCurrentGroup());
            }
        }).StartingIn((long)outDurationMs + 20);
    }

    public void HideImmediate()
    {
        _labelHideSchedule?.Pause();
        HideAllTooltips();
        for (int i = 0; i < SlotsPerPage; i++)
        {
            var btn = slotButtons[i];
            if (btn == null) continue;
            btn.Unbind();
            
            btn.RemoveFromClassList("macro-state--visible");
            btn.AddToClassList("macro-state--hidden");
            btn.style.display = DisplayStyle.None;
        }
        HideArrows();
        if (groupLabel != null) 
        {
            groupLabel.RemoveFromClassList("label-state--visible");
            groupLabel.AddToClassList("label-state--hidden");
        }
        if (macroContainer != null) macroContainer.style.display = DisplayStyle.None;
    }

    // ── Radial layout ───────────────────────────────────────────────

    private void EnterRadialLayout()
    {
        if (macroContainer == null) return;

        macroContainer.style.position = Position.Absolute;
        macroContainer.style.left = anchorPanelPos.x;
        macroContainer.style.top = anchorPanelPos.y;
        macroContainer.style.right = StyleKeyword.Auto;
        macroContainer.style.width = 0;
        macroContainer.style.height = 0;
        macroContainer.style.paddingLeft = 0;
        macroContainer.style.paddingRight = 0;
        macroContainer.style.paddingTop = 0;
        macroContainer.style.paddingBottom = 0;
        macroContainer.style.backgroundColor = new StyleColor(Color.clear);
        macroContainer.style.borderLeftWidth = 0;
        macroContainer.style.borderRightWidth = 0;
        macroContainer.style.borderTopWidth = 0;
        macroContainer.style.borderBottomWidth = 0;
        macroContainer.style.overflow = Overflow.Visible;
        macroContainer.style.display = DisplayStyle.Flex;

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
            
            btn.style.position = Position.Absolute;
            btn.style.left = -MacroHalfSize;
            btn.style.top = -MacroHalfSize;
            
            btn.RemoveFromClassList("macro-state--visible");
            btn.AddToClassList("macro-state--hidden");
            btn.style.display = DisplayStyle.None;
        }
        HideArrows();
        if (groupLabel != null)
        {
            groupLabel.style.position = Position.Absolute;
            groupLabel.RemoveFromClassList("label-state--visible");
            groupLabel.AddToClassList("label-state--hidden");
        }
    }

    // ── Arc geometry ────────────────────────────────────────────────

    private Vector2 ArcSlotOffset(int slot, float halfSize, float radiusOverride = -1f)
    {
        float r = radiusOverride > 0f ? radiusOverride : radialRadius;
        float t = slot / (float)(SlotsPerPage + 1);
        float angle = Mathf.Lerp(arcStartAngle, arcEndAngle, t) * Mathf.Deg2Rad;
        return new Vector2(
            Mathf.Cos(angle) * r - halfSize,
            Mathf.Sin(angle) * r - halfSize);
    }

    private Vector2 ContentOffset(int contentSlot) => ArcSlotOffset(contentSlot + 1, MacroHalfSize);

    // ── Bind group actions ────────────────────

    private void BindGroup(MacroGroup group)
    {
        MacroActionType[] actions = { group.action0, group.action1, group.action2 };

        for (int i = 0; i < SlotsPerPage; i++)
        {
            var btn = slotButtons[i];
            if (btn == null) continue;

            if (_slotIconRefreshCbs[i] != null)
            {
                btn.clicked -= _slotIconRefreshCbs[i];
                _slotIconRefreshCbs[i] = null;
            }

            btn.Unbind();

            if (actions[i] == MacroActionType.None)
            {
                btn.style.display = DisplayStyle.None;
                continue;
            }

            btn.Bind(MacroActionFactory.Create(actions[i]), inputProvider);
            SetButtonIcon(btn, actions[i]);

            if (actions[i] == MacroActionType.MuteToggle)
            {
                var capturedBtn = btn;
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

    private static readonly Dictionary<MacroActionType, string> actionIconMap = new Dictionary<MacroActionType, string>
    {
        { MacroActionType.Back, "icon-back" },
        { MacroActionType.Forward, "icon-forward" },
        { MacroActionType.Refresh, "icon-refresh" },
        { MacroActionType.NewTab, "icon-new-tab" },
        { MacroActionType.CloseTab, "icon-close-tab" },
        { MacroActionType.SwitchWindow, "icon-switch-window" },
        { MacroActionType.ZoomIn, "icon-zoom-in" },
        { MacroActionType.ZoomOut, "icon-zoom-out" },
        { MacroActionType.Screenshot, "icon-screenshot" },
        { MacroActionType.PageUp, "icon-page-up" },
        { MacroActionType.PageDown, "icon-page-down" },
        { MacroActionType.ReturnToDesktop, "icon-home" },
        { MacroActionType.SnapLeft, "icon-snap-left" },
        { MacroActionType.SnapRight, "icon-snap-right" },
        { MacroActionType.MaximizeRestore, "icon-maximize" },
        { MacroActionType.Minimize, "icon-minimize" },
        { MacroActionType.CloseWindow, "icon-close-window" },
        { MacroActionType.Undo, "icon-undo" },
        { MacroActionType.Redo, "icon-redo" },
        { MacroActionType.FindOnPage, "icon-find" },
        { MacroActionType.HomeDashboard, "icon-home" },
        { MacroActionType.AppCycler, "icon-switch-window" },
        { MacroActionType.Settings, "icon-settings" }
    };

    private string GetIconClass(MacroActionType type)
    {
        if (type == MacroActionType.MuteToggle)
            return Win32AudioInterop.GetMute() ? "icon-voice" : "icon-mute";

        return actionIconMap.TryGetValue(type, out string iconClass) ? iconClass : null;
    }

    private void SetButtonIcon(MacroButton btn, MacroActionType actionType)
    {
        var icon = btn.Q<VisualElement>(className: "btn-icon");
        if (icon == null) return;

        var toRemove = new List<string>();
        foreach (var cls in icon.GetClasses())
            if (cls.StartsWith("icon-"))
                toRemove.Add(cls);
        foreach (var cls in toRemove)
            icon.RemoveFromClassList(cls);

        string iconClass = GetIconClass(actionType);
        if (string.IsNullOrEmpty(iconClass)) return;

        icon.AddToClassList(iconClass);

        bool flipX = actionType == MacroActionType.Forward || actionType == MacroActionType.SnapRight;
        icon.style.scale = new Scale(new Vector2(flipX ? -1f : 1f, 1f));
    }

    // ── Group label flash ───────────────────────────────────────────

    private void FlashGroupName(string groupName)
    {
        if (groupLabel == null) return;

        _labelHideSchedule?.Pause();

        groupLabel.text = groupName;
        groupLabel.style.position = Position.Absolute;
        groupLabel.style.left = -80f;
        groupLabel.style.top = labelOffsetY;
        groupLabel.style.width = 160f;

        groupLabel.RemoveFromClassList("label-state--hidden");
        groupLabel.AddToClassList("label-state--visible");

        _labelHideSchedule = groupLabel.schedule.Execute(() =>
        {
            groupLabel.RemoveFromClassList("label-state--visible");
            groupLabel.AddToClassList("label-state--hidden");
        }).StartingIn((long)(labelFadeInMs + labelHoldMs));
    }

    // ── Reveal current group ────────────────────────────────────────

    private void RevealGroup(MacroGroup group)
    {
        BindGroup(group);

        for (int i = 0; i < SlotsPerPage; i++)
        {
            var btn = slotButtons[i];
            if (btn == null) continue;
            
            btn.style.left = -MacroHalfSize;
            btn.style.top = -MacroHalfSize;
            btn.RemoveFromClassList("macro-state--visible");
            btn.AddToClassList("macro-state--hidden");
            btn.style.display = DisplayStyle.None;
        }

        if (MacroViewModel.Groups.Length > 1)
        {
            PlaceArrow(prevBtn, 0);
            PlaceArrow(nextBtn, SlotsPerPage + 1);
        }

        MacroActionType[] actions = { group.action0, group.action1, group.action2 };
        for (int s = 0; s < SlotsPerPage; s++)
        {
            if (actions[s] == MacroActionType.None) continue;
            var btn = slotButtons[s];
            if (btn == null) continue;
            
            Vector2 dest = ContentOffset(s);
            btn.style.left = dest.x;
            btn.style.top = dest.y;
            btn.style.display = DisplayStyle.Flex;

            int captured = s;
            btn.schedule.Execute(() =>
            {
                long dly = (long)(staggerMs * captured);
                btn.style.transitionDelay = new List<TimeValue> { new(dly, TimeUnit.Millisecond) };
                btn.RemoveFromClassList("macro-state--hidden");
                btn.AddToClassList("macro-state--visible");
            }).StartingIn(16);
        }

        FlashGroupName(group.name);
        FlashButtonTooltips(group);
    }

    // ── Button tooltips ─────────────────────────────────────────────

    private void HideAllTooltips()
    {
        for (int i = 0; i < SlotsPerPage; i++)
        {
            _tooltipHideSchedules[i]?.Pause();
            SetTooltipVisible(i, false);
        }
    }

    private void SetTooltipVisible(int index, bool visible)
    {
        if (_slotTooltips[index] == null) return;
        _slotTooltips[index].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible) 
        {
            _slotTooltips[index].RemoveFromClassList("label-state--visible");
            _slotTooltips[index].AddToClassList("label-state--hidden");
        }
    }

    private void FlashButtonTooltips(MacroGroup group)
    {
        MacroActionType[] actions = { group.action0, group.action1, group.action2 };

        for (int s = 0; s < SlotsPerPage; s++)
        {
            var tip = _slotTooltips[s];
            if (tip == null) continue;

            _tooltipHideSchedules[s]?.Pause();

            if (actions[s] == MacroActionType.None)
            {
                SetTooltipVisible(s, false);
                continue;
            }

            tip.text = MacroActionFactory.Create(actions[s]).DisplayName;
            
            Vector2 btnPos = ContentOffset(s);
            tip.style.left = btnPos.x - 20f;
            tip.style.top = btnPos.y - (22f + 20f);
            tip.style.width = buttonSize + 40f;
            SetTooltipVisible(s, true);

            int captured = s;
            tip.schedule.Execute(() =>
            {
                var t = _slotTooltips[captured];
                t.RemoveFromClassList("label-state--hidden");
                t.AddToClassList("label-state--visible");
            }).StartingIn((long)(tooltipDelayMs + staggerMs * captured));

            _tooltipHideSchedules[s] = tip.schedule.Execute(() =>
            {
                var t = _slotTooltips[captured];
                t.RemoveFromClassList("label-state--visible");
                t.AddToClassList("label-state--hidden");
            }).StartingIn((long)(tooltipDelayMs + staggerMs * captured + labelFadeInMs + tooltipHoldMs));
        }
    }

    private void OnSlotHoverEnter(int slot)
    {
        var tip = _slotTooltips[slot];
        if (tip == null || viewModel == null || !viewModel.IsOpen) return;

        var group = viewModel.GetCurrentGroup();
        MacroActionType[] actions = { group.action0, group.action1, group.action2 };
        if (actions[slot] == MacroActionType.None) return;

        _tooltipHideSchedules[slot]?.Pause();

        tip.text = MacroActionFactory.Create(actions[slot]).DisplayName;
        Vector2 btnPos = ContentOffset(slot);
        tip.style.left = btnPos.x - 20f;
        tip.style.top = btnPos.y - (22f + 20f);  
        tip.style.width = buttonSize + 40f;
        tip.style.display = DisplayStyle.Flex;

        tip.schedule.Execute(() =>
        {
            tip.RemoveFromClassList("label-state--hidden");
            tip.AddToClassList("label-state--visible");
        }).StartingIn(16);
    }

    private void OnSlotHoverLeave(int slot)
    {
        var tip = _slotTooltips[slot];
        if (tip == null) return;

        _tooltipHideSchedules[slot] = tip.schedule.Execute(() =>
        {
            tip.RemoveFromClassList("label-state--visible");
            tip.AddToClassList("label-state--hidden");
        }).StartingIn(50);
    }

    // ── Arrows ──────────────────────────────────────────────────────

    private void PlaceArrow(Button arrow, int slot)
    {
        if (arrow == null) return;
        Vector2 pos = ArcSlotOffset(slot, ArrowHalfSize, radialRadius + (MacroHalfSize - ArrowHalfSize));
        arrow.style.position = Position.Absolute;
        arrow.style.left = pos.x;
        arrow.style.top = pos.y;
        if (slot == 0)
            arrow.style.scale = new Scale(new Vector2(-1f, 1f));
        else
            arrow.style.scale = new Scale(Vector2.one);
        arrow.style.rotate = new Rotate(new Angle(0f));
        arrow.style.opacity = 1f;
        arrow.style.display = DisplayStyle.Flex;
    }

    private void HideArrows()
    {
        if (prevBtn != null) prevBtn.style.display = DisplayStyle.None;
        if (nextBtn != null) nextBtn.style.display = DisplayStyle.None;
    }

    // ── Layout ──────────────────────────────────────────────────────

    private void ApplyLayout()
    {
        var doc = uiDocument != null ? uiDocument : GetComponent<UIDocument>();
        if (doc == null) return;
        var root = doc.rootVisualElement;
        if (root == null) return;

        var container = root.Q<VisualElement>("macro-container");
        if (container != null)
        {
            container.style.width = containerWidth;
            container.style.paddingTop = containerPaddingTB;
            container.style.paddingBottom = containerPaddingTB;
            container.style.paddingLeft = containerPaddingLR;
            container.style.paddingRight = containerPaddingLR;
            container.style.borderTopLeftRadius = containerBorderRadius;
            container.style.borderTopRightRadius = containerBorderRadius;
            container.style.borderBottomLeftRadius = containerBorderRadius;
            container.style.borderBottomRightRadius = containerBorderRadius;
        }

        float half = MacroHalfSize;
        for (int i = 0; i < SlotsPerPage; i++)
        {
            var btn = slotButtons[i];
            if (btn == null) continue;
            btn.style.width = buttonSize;
            btn.style.height = buttonSize;
            btn.style.marginTop = buttonMargin;
            btn.style.marginBottom = buttonMargin;
            btn.style.marginLeft = buttonMargin;
            btn.style.marginRight = buttonMargin;
            btn.style.borderTopLeftRadius = half;
            btn.style.borderTopRightRadius = half;
            btn.style.borderBottomLeftRadius = half;
            btn.style.borderBottomRightRadius = half;
            var icon = btn.Q<VisualElement>(className: "btn-icon");
            if (icon != null) { icon.style.width = iconSize; icon.style.height = iconSize; }
        }

        void SizeArrow(Button a)
        {
            if (a == null) return;
            float ah = ArrowHalfSize;
            a.style.width = wheelArrowSize;
            a.style.height = wheelArrowSize;
            a.style.borderTopLeftRadius = ah;
            a.style.borderTopRightRadius = ah;
            a.style.borderBottomLeftRadius = ah;
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
}
