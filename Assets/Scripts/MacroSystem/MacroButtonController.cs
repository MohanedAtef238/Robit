using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MacroButtonController : MonoBehaviour
{
    private const int SlotsPerPage = 3;
    
    [SerializeField] private float outDurationMs = 180f;
    [SerializeField] private float staggerMs = 60f;

    private UIDocument uiDocument;
    private VisualElement macroContainer;
    private Label groupLabel;
    private Button prevBtn;
    private Button nextBtn;
    private MacroButton[] slotButtons = new MacroButton[SlotsPerPage];
    private Label[] slotTooltips = new Label[SlotsPerPage];
    
    private IInputProvider inputProvider;
    private MacroViewModel viewModel;
    
    private Action[] _slotIconRefreshCbs = new Action[SlotsPerPage];

    void OnEnable()
    {
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        inputProvider = new PointerInputProvider();
        var root = uiDocument.rootVisualElement;
        
        macroContainer = root.Q<VisualElement>("macro-container");
        groupLabel = root.Q<Label>("group-name-label");
        
        prevBtn = root.Q<Button>("BtnWheelPrev");
        nextBtn = root.Q<Button>("BtnWheelNext");

        if (prevBtn != null) prevBtn.clicked += () => viewModel?.PrevGroup();
        if (nextBtn != null) nextBtn.clicked += () => viewModel?.NextGroup();

        for (int i = 0; i < SlotsPerPage; i++)
        {
            slotButtons[i] = root.Q<MacroButton>($"BtnSlot{i}");
            slotTooltips[i] = root.Q<Label>($"Tooltip{i}");
            
            int captured = i;
            slotButtons[i]?.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(captured, true));
            slotButtons[i]?.RegisterCallback<PointerLeaveEvent>(_ => ShowTooltip(captured, false));

            // Firefly cursor: open on hover, closed on leave
            GlobalCursorManager.AttachTo(slotButtons[i]);
        }

        if (prevBtn != null) GlobalCursorManager.AttachTo(prevBtn);
        if (nextBtn != null) GlobalCursorManager.AttachTo(nextBtn);

        viewModel = new MacroViewModel();
        viewModel.OnMenuToggled += OnMenuToggled;
        viewModel.OnGroupChanged += OnGroupChanged;

        HideImmediate();
    }

    void OnDisable()
    {
        if (viewModel != null)
        {
            viewModel.OnMenuToggled -= OnMenuToggled;
            viewModel.OnGroupChanged -= OnGroupChanged;
        }

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

    // ── Public API (Backward Compatibility) ──────────────────────────

    public void Toggle()
    {
        if (viewModel == null) return;
        if (viewModel.IsOpen) viewModel.Close();
        else viewModel.Open();
    }
    
    public void HideWithShrink() => viewModel?.Close();
    
    // Ignored positional parameters since the UI is strictly fixed layout now.
    public void ShowWithBounceAtTransform(Transform anchor, Vector3 worldOffset, Vector2 panelOffset, Camera renderCamera = null) 
        => viewModel?.Open();

    public void ShowWithBounceAtWorldPosition(Vector3 worldPos, Vector2 panelOffset, Camera renderCamera = null)
        => viewModel?.Open();

    public void HideImmediate()
    {
        for (int i = 0; i < SlotsPerPage; i++)
        {
            var btn = slotButtons[i];
            if (btn == null) continue;
            btn.Unbind();
            SetClass(btn, "macro-state--visible", false);
            SetClass(btn, "macro-state--hidden", true);
            ShowTooltip(i, false);
        }
        
        if (prevBtn != null) { SetClass(prevBtn, "macro-state--visible", false); SetClass(prevBtn, "macro-state--hidden", true); }
        if (nextBtn != null) { SetClass(nextBtn, "macro-state--visible", false); SetClass(nextBtn, "macro-state--hidden", true); }
        
        SetClass(macroContainer, "macro-state--visible", false);
        SetClass(macroContainer, "macro-state--hidden", true);
        
        SetClass(groupLabel, "label-state--visible", false);
        SetClass(groupLabel, "label-state--hidden", true);
    }

    // ── Internal View Logic ──────────────────────────────────────────

    private void OnMenuToggled(bool isOpen)
    {
        if (isOpen)
        {
            SetClass(macroContainer, "macro-state--hidden", false);
            SetClass(macroContainer, "macro-state--visible", true);
            macroContainer.schedule.Execute(() => RevealGroup(viewModel.GetCurrentGroup())).StartingIn(16);
        }
        else
        {
            SetClass(groupLabel, "label-state--visible", false);
            SetClass(groupLabel, "label-state--hidden", true);

            HideSlot(prevBtn);
            HideSlot(nextBtn);
            for (int i = 0; i < SlotsPerPage; i++)
            {
                HideSlot(slotButtons[i]);
                ShowTooltip(i, false);
            }

            macroContainer?.schedule.Execute(() =>
            {
                if (!viewModel.IsOpen) 
                {
                    SetClass(macroContainer, "macro-state--visible", false);
                    SetClass(macroContainer, "macro-state--hidden", true);
                }
            }).StartingIn((long)outDurationMs + 40);
        }
    }

    private void OnGroupChanged(int groupIndex)
    {
        for (int i = 0; i < SlotsPerPage; i++) ShowTooltip(i, false);

        for (int i = 0; i < SlotsPerPage; i++) HideSlot(slotButtons[i]);

        macroContainer.schedule.Execute(() =>
        {
            if (viewModel != null && viewModel.IsOpen)
                RevealGroup(viewModel.GetCurrentGroup(), animateCyclers: false);
        }).StartingIn((long)outDurationMs + 20);
    }

    private void RevealGroup(MacroGroup group, bool animateCyclers = true)
    {
        BindGroup(group);

        if (MacroViewModel.Groups.Length > 1)
        {
            if (animateCyclers)
            {
                ShowSlot(prevBtn, 0);
                ShowSlot(nextBtn, SlotsPerPage + 1);
            }
            else
            {
                SetClass(prevBtn, "macro-state--hidden", false);
                SetClass(prevBtn, "macro-state--visible", true);
                SetClass(nextBtn, "macro-state--hidden", false);
                SetClass(nextBtn, "macro-state--visible", true);
            }
        }

        for (int s = 0; s < SlotsPerPage; s++)
        {
            if (GetGroupAction(group, s) == MacroActionType.None) continue;
            ShowSlot(slotButtons[s], s + 1);
        }

        if (groupLabel != null)
        {
            groupLabel.text = group.name;
            SetClass(groupLabel, "label-state--hidden", false);
            SetClass(groupLabel, "label-state--visible", true);
            
            groupLabel.schedule.Execute(() => {
                SetClass(groupLabel, "label-state--visible", false);
                SetClass(groupLabel, "label-state--hidden", true);
            }).StartingIn(1150);
        }
    }

    private void ShowSlot(VisualElement btn, int staggerIndex)
    {
        if (btn == null) return;
        btn.schedule.Execute(() =>
        {
            btn.style.transitionDelay = new List<TimeValue> { new((long)(staggerMs * staggerIndex), TimeUnit.Millisecond) };
            SetClass(btn, "macro-state--hidden", false);
            SetClass(btn, "macro-state--visible", true);
        }).StartingIn(16);
    }

    private void HideSlot(VisualElement btn)
    {
        if (btn == null) return;
        btn.style.transitionDelay = new List<TimeValue> { new(0, TimeUnit.Millisecond) };
        SetClass(btn, "macro-state--visible", false);
        SetClass(btn, "macro-state--hidden", true);
    }

    private void ShowTooltip(int index, bool show)
    {
        var tip = slotTooltips[index];
        if (tip == null) return;
        
        if (show && viewModel != null && viewModel.IsOpen)
        {
            var group = viewModel.GetCurrentGroup();
            MacroActionType action = GetGroupAction(group, index);
            if (action == MacroActionType.None) return;

            tip.text = MacroActionFactory.Create(action).DisplayName;
            SetClass(tip, "label-state--hidden", false);
            SetClass(tip, "label-state--visible", true);
        }
        else
        {
            SetClass(tip, "label-state--visible", false);
            SetClass(tip, "label-state--hidden", true);
        }
    }

    private void SetClass(VisualElement el, string className, bool enable)
    {
        if (el == null) return;
        if (enable && !el.ClassListContains(className)) el.AddToClassList(className);
        else if (!enable && el.ClassListContains(className)) el.RemoveFromClassList(className);

        // Dynamically disable hitboxes when hidden to prevent overlap issues
        if (className == "macro-state--hidden" && enable)
        {
            el.pickingMode = PickingMode.Ignore;
        }
        else if (className == "macro-state--visible" && enable)
        {
            el.pickingMode = PickingMode.Position;
        }
    }

    // ── Binding & Icons ──────────────────────────────────────────────

    private void BindGroup(MacroGroup group)
    {
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

            MacroActionType action = GetGroupAction(group, i);
            if (action == MacroActionType.None) continue;

            btn.Bind(MacroActionFactory.Create(action), inputProvider);
            SetButtonIcon(btn, action);

            if (action == MacroActionType.MuteToggle)
            {
                var capturedBtn = btn;
                int capturedSlot = i;
                _slotIconRefreshCbs[capturedSlot] = () =>
                    capturedBtn.schedule.Execute(() => SetButtonIcon(capturedBtn, MacroActionType.MuteToggle)).StartingIn(50);
                capturedBtn.clicked += _slotIconRefreshCbs[capturedSlot];
            }
        }
    }

    /// Returns the action assigned to the given slot index (0–2) without allocating a temporary array.
    private static MacroActionType GetGroupAction(MacroGroup group, int slot) => slot switch
    {
        0 => group.action0,
        1 => group.action1,
        2 => group.action2,
        _ => MacroActionType.None,
    };

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
        if (type == MacroActionType.MuteToggle) return Win32AudioInterop.GetMute() ? "icon-voice" : "icon-mute";
        return actionIconMap.TryGetValue(type, out string iconClass) ? iconClass : null;
    }

    private void SetButtonIcon(MacroButton btn, MacroActionType actionType)
    {
        var icon = btn.Q<VisualElement>(className: "btn-icon");
        if (icon == null) return;

        var toRemove = new List<string>();
        foreach (var cls in icon.GetClasses()) if (cls.StartsWith("icon-")) toRemove.Add(cls);
        foreach (var cls in toRemove) icon.RemoveFromClassList(cls);

        string iconClass = GetIconClass(actionType);
        if (string.IsNullOrEmpty(iconClass)) return;
        icon.AddToClassList(iconClass);

        bool flipX = actionType == MacroActionType.Forward || actionType == MacroActionType.SnapRight;
        icon.style.scale = new Scale(new Vector2(flipX ? -1f : 1f, 1f));
    }
}
