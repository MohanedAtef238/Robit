using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MacroButtonController : MonoBehaviour
{
    [SerializeField] private List<MacroButtonBinding> bindings = new();

    // Arc is symmetric around 270° (= straight UP in UITK Y-down space).
    // 200 → 340 spans 140°, giving a wide crown above the mascot with
    // comfortable spacing between all 5 arc slots (3 content + 2 arrows).
    [Header("Arc  (must match scene: 200 start, 340 end, 150 radius)")]
    [SerializeField] private float arcStartAngle = 200f;
    [SerializeField] private float arcEndAngle   = 340f;
    [SerializeField] private float radialRadius  = 10f;

    [Header("Carousel Animation")]
    [SerializeField] private float inDurationMs  = 300f;
    [SerializeField] private float outDurationMs = 180f;
    [SerializeField] private float staggerMs     = 60f;

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

    //    runtime references                                           
    private UIDocument      uiDocument;
    private VisualElement   macroContainer;
    private VisualElement   buttonGrid;
    private Button          prevBtn;
    private Button          nextBtn;
    private readonly List<MacroButton> boundButtons = new();
    private IInputProvider  inputProvider;

    //    state                                                        
    private const int SlotsPerPage = 3;
    private int       _pageOffset;
    private Vector2   anchorPanelPos;
    private bool      _open;
    // Maps content slot index (0=left, 1=mid, 2=right) â†’ boundButtons index
    private readonly int[] _slotBtn = new int[SlotsPerPage];

    // Half-sizes derived from serializable size fields — keep in sync with Inspector.
    private float MacroHalfSize => buttonSize     * 0.5f;
    private float ArrowHalfSize => wheelArrowSize * 0.5f;

    //    lifecycle                                                    
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

        foreach (var binding in bindings)
        {
            if (string.IsNullOrEmpty(binding.buttonName)) continue;
            var el = root.Q<MacroButton>(binding.buttonName);
            if (el == null) { Debug.LogWarning($"[MacroButtonController] '{binding.buttonName}' not found"); continue; }
            el.Bind(MacroActionFactory.Create(binding.actionType), inputProvider);
            boundButtons.Add(el);
        }

        prevBtn = root.Q<Button>("BtnWheelPrev");
        nextBtn = root.Q<Button>("BtnWheelNext");
        if (prevBtn != null) prevBtn.clicked += OnPrevClicked;
        if (nextBtn != null) nextBtn.clicked += OnNextClicked;

        ApplyLayout();
        HideImmediate();
        Debug.Log($"[MacroButtonController] Registered {boundButtons.Count} macro buttons");
    }

    void OnDisable()
    {
        foreach (var mb in boundButtons) mb.Unbind();
        boundButtons.Clear();
        if (prevBtn != null) prevBtn.clicked -= OnPrevClicked;
        if (nextBtn != null) nextBtn.clicked -= OnNextClicked;
    }

    //    public API                                                   

    public void ShowWithBounceAtWorldPosition(Vector3 worldPos, Vector2 panelOffset, Camera renderCamera = null)
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;
        _open = true;
        _pageOffset = 0;

        var panel = uiDocument.rootVisualElement.panel;
        anchorPanelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
            panel, worldPos, renderCamera != null ? renderCamera : Camera.main);
            
        anchorPanelPos += panelOffset;

        // Clamp so the arc centre stays within the visible panel area.
        var panelSize = uiDocument.rootVisualElement.layout;
        float margin = radialRadius + buttonSize; // keep buttons on-screen
        anchorPanelPos.x = Mathf.Clamp(anchorPanelPos.x, margin, panelSize.width  - margin);
        anchorPanelPos.y = Mathf.Clamp(anchorPanelPos.y, margin, panelSize.height - margin);

        Debug.Log($"[MacroButtonController] anchor panel pos = {anchorPanelPos}  (world input = {worldPos})");

        EnterRadialLayout();
        macroContainer.schedule.Execute(RevealPage).StartingIn(16);
    }

    public void HideWithShrink()
    {
        if (!_open) return;
        _open = false;
        HideArrows();

        foreach (var btn in boundButtons)
        {
            if (btn.style.display == DisplayStyle.None) continue;
            ApplyTransitions(btn, 0, (long)outDurationMs);
            btn.style.left    = -MacroHalfSize;
            btn.style.top     = -MacroHalfSize;
            btn.style.scale   = new Scale(Vector2.zero);
            btn.style.opacity = 0f;
        }

        macroContainer?.schedule.Execute(() =>
        {
            if (!_open) { macroContainer.style.display = DisplayStyle.None; }
        }).StartingIn((long)outDurationMs + 40);
    }

    public void HideImmediate()
    {
        _open = false;
        foreach (var btn in boundButtons)
        {
            ClearTransitions(btn);
            btn.style.scale   = new Scale(Vector2.zero);
            btn.style.opacity = 0f;
            btn.style.display = DisplayStyle.None;
        }
        HideArrows();
        if (macroContainer != null) macroContainer.style.display = DisplayStyle.None;
    }

    // ── radial layout ───────────────────────────────────────────────

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

        foreach (var btn in boundButtons)
        {
            ClearTransitions(btn);
            btn.style.position = Position.Absolute;
            btn.style.left = -MacroHalfSize; btn.style.top = -MacroHalfSize;
            btn.style.scale = new Scale(Vector2.zero);
            btn.style.opacity = 0f;
            btn.style.display = DisplayStyle.None;
        }
        HideArrows();
    }
    //    arc geometry                                                 

    // Full arc: slot 0 = Prev arrow | slots 1-3 = content | slot 4 = Next arrow.
    // Slots are evenly spaced across the arc. `radiusOverride` shifts an element
    // slightly outward (used for arrows so their outer edge aligns with macro edges).
    private Vector2 ArcSlotOffset(int slot, float halfSize, float radiusOverride = -1f)
    {
        float r     = radiusOverride > 0f ? radiusOverride : radialRadius;
        float t     = slot / (float)(SlotsPerPage + 1);
        float angle = Mathf.Lerp(arcStartAngle, arcEndAngle, t) * Mathf.Deg2Rad;
        return new Vector2(
            Mathf.Cos(angle) * r - halfSize,
            Mathf.Sin(angle) * r - halfSize);
    }

    // Content slot 0-2 â†’ arc slots 1-3
    private Vector2 ContentOffset(int contentSlot) => ArcSlotOffset(contentSlot + 1, MacroHalfSize);

    //    initial reveal                                               

    private void RevealPage()
    {
        int n = boundButtons.Count;
        if (n == 0) return;

        foreach (var btn in boundButtons)
        {
            ClearTransitions(btn);
            btn.style.left = -MacroHalfSize; btn.style.top = -MacroHalfSize;
            btn.style.scale = new Scale(Vector2.zero);
            btn.style.opacity = 0f;
            btn.style.display = DisplayStyle.None;
        }

        // Initialise slot map
        for (int s = 0; s < SlotsPerPage; s++)
            _slotBtn[s] = (_pageOffset + s) % n;

        // Arrows always visible while wheel is open (hidden only if n <= SlotsPerPage)
        if (n > SlotsPerPage)
        {
            PlaceArrow(prevBtn, 0);
            PlaceArrow(nextBtn, SlotsPerPage + 1);
        }

        // Fan content buttons in with stagger
        for (int s = 0; s < SlotsPerPage && s < n; s++)
        {
            MacroButton btn  = boundButtons[_slotBtn[s]];
            Vector2     dest = ContentOffset(s);
            long        dly  = (long)(staggerMs * s);

            btn.style.display = DisplayStyle.Flex;
            btn.schedule.Execute(() =>
            {
                ApplyTransitions(btn, dly, (long)inDurationMs);
                btn.style.left    = dest.x;
                btn.style.top     = dest.y;
                btn.style.scale   = new Scale(Vector2.one);
                btn.style.opacity = 1f;
            }).StartingIn(16);
        }
    }

    //    true carousel: slide individual buttons along the arc         

    private void PageTo(int direction)
    {
        int n = boundButtons.Count;
        if (n <= SlotsPerPage || !_open) return;

        bool goNext   = direction > 0;
        _pageOffset   = ((_pageOffset + direction) % n + n) % n;

        float btnHalf = MacroHalfSize;

        //    EXIT: the button leaving the visible area              
        int exitSlot   = goNext ? 0 : SlotsPerPage - 1;
        int exitBtnIdx = _slotBtn[exitSlot];
        MacroButton exitBtn  = boundButtons[exitBtnIdx];
        // Move the exiting button toward the arrow position. Use an
        // overridden radius so the exiting macro's centre aligns with the
        // arrow centre (arrow is placed slightly farther out to match edges).
        Vector2     exitDest = ArcSlotOffset(
            goNext ? 0 : SlotsPerPage + 1,
            btnHalf,
            radialRadius + (btnHalf - ArrowHalfSize));

        ApplyTransitions(exitBtn, 0, (long)outDurationMs);
        exitBtn.style.left    = exitDest.x;
        exitBtn.style.top     = exitDest.y;
        exitBtn.style.scale   = new Scale(Vector2.zero);
        exitBtn.style.opacity = 0f;

        // Hide after transition (capture index so closure is safe)
        int capturedExit = exitBtnIdx;
        boundButtons[capturedExit].schedule
            .Execute(() => boundButtons[capturedExit].style.display = DisplayStyle.None)
            .StartingIn((long)outDurationMs + 20);

        //    SHIFT: slide remaining 2 buttons to their new slots    
        if (goNext)
        {
            // slot 1 â†’ slot 0,  slot 2 â†’ slot 1
            for (int s = 1; s < SlotsPerPage; s++)
            {
                MacroButton btn  = boundButtons[_slotBtn[s]];
                Vector2     dest = ContentOffset(s - 1);
                ApplyTransitions(btn, 0, (long)inDurationMs);
                btn.style.left = dest.x;
                btn.style.top  = dest.y;
                _slotBtn[s - 1] = _slotBtn[s];
            }
        }
        else
        {
            // slot 1 â†’ slot 2,  slot 0 â†’ slot 1
            for (int s = SlotsPerPage - 2; s >= 0; s--)
            {
                MacroButton btn  = boundButtons[_slotBtn[s]];
                Vector2     dest = ContentOffset(s + 1);
                ApplyTransitions(btn, 0, (long)inDurationMs);
                btn.style.left = dest.x;
                btn.style.top  = dest.y;
                _slotBtn[s + 1] = _slotBtn[s];
            }
        }

        //    ENTER: new button slides in from behind the arrow      
        int enterSlot   = goNext ? SlotsPerPage - 1 : 0;
        int enterBtnIdx = (_pageOffset + enterSlot) % n;
        _slotBtn[enterSlot] = enterBtnIdx;

        MacroButton enterBtn   = boundButtons[enterBtnIdx];
        Vector2     entryStart = ArcSlotOffset(
            goNext ? SlotsPerPage + 1 : 0,
            btnHalf,
            radialRadius + (btnHalf - ArrowHalfSize));
        Vector2     entryDest  = ContentOffset(enterSlot);

        ClearTransitions(enterBtn);
        enterBtn.style.position = Position.Absolute;
        enterBtn.style.left     = entryStart.x;
        enterBtn.style.top      = entryStart.y;
        enterBtn.style.scale    = new Scale(Vector2.zero);
        enterBtn.style.opacity  = 0f;
        enterBtn.style.display  = DisplayStyle.Flex;

        enterBtn.schedule.Execute(() =>
        {
            ApplyTransitions(enterBtn, 0, (long)inDurationMs);
            enterBtn.style.left    = entryDest.x;
            enterBtn.style.top     = entryDest.y;
            enterBtn.style.scale   = new Scale(Vector2.one);
            enterBtn.style.opacity = 1f;
        }).StartingIn(16);
    }

    //    arrows                                                       

    private void PlaceArrow(Button arrow, int slot)
    {
        if (arrow == null) return;
        // Place arrows slightly outward so their outer edge aligns with
        // the macros' outer edge, giving consistent visual spacing.
        Vector2 pos = ArcSlotOffset(slot, ArrowHalfSize, radialRadius + (MacroHalfSize - ArrowHalfSize));
        arrow.style.position = Position.Absolute;
        arrow.style.left     = pos.x;
        arrow.style.top      = pos.y;
        // Use the same right-pointing asset for both arrows. Mirror the left arrow
        // by flipping its X scale so the curvature stays correct.
        if (slot == 0)
        {
            // Prev (left) arrow — mirror horizontally
            arrow.style.scale = new Scale(new Vector2(-1f, 1f));
        }
        else
        {
            // Next (right) arrow — normal scale
            arrow.style.scale = new Scale(Vector2.one);
        }
        // Ensure rotation is neutral; mirroring handled by scale above.
        arrow.style.rotate = new Rotate(new Angle(0f));
        arrow.style.opacity  = 1f;
        arrow.style.display  = DisplayStyle.Flex;
    }

    private void HideArrows()
    {
        if (prevBtn != null) prevBtn.style.display = DisplayStyle.None;
        if (nextBtn != null) nextBtn.style.display = DisplayStyle.None;
    }

    // ── paging callbacks ────────────────────────────────────────────

    private void OnPrevClicked() => PageTo(-1);
    private void OnNextClicked() => PageTo(+1);

    //    layout  ─────────────────────────────────────────────────────

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

        IEnumerable<MacroButton> btns = boundButtons.Count > 0
            ? (IEnumerable<MacroButton>)boundButtons
            : root.Query<MacroButton>(className: "macro-button").ToList();

        float half = MacroHalfSize;
        foreach (var btn in btns)
        {
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
        // Guard: panel may not be ready during edit-mode domain reloads.
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        // Temporarily stash so ApplyLayout() can resolve refs.
        var saved = uiDocument;
        uiDocument = doc;
        ApplyLayout();
        uiDocument = saved;
    }
#endif

    //    USS transitions                                               

    private void ApplyTransitions(VisualElement el, long delayMs, long durationMs)
    {
        el.style.transitionProperty       = new List<StylePropertyName>
            { new("left"), new("top"), new("scale"), new("opacity") };
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
