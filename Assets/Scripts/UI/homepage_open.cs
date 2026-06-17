using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Attach to the GameObject that holds your UIDocument.
/// Call Toggle() from the Inspector (wire it to ClickableObject.OnObjectClicked).
///
/// Animates UI elements (tagged with a USS class) and 3D GameObjects
/// in a single interleaved stagger list — each entry pops in/out one after another.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MenuToggleController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("UI Animation Targets")]
    [Tooltip("USS class that marks which UI elements should stagger-animate.")]
    [SerializeField] private string _animationClass = "opening_animation";

    [Header("3D Object Animation Targets")]
    [Tooltip("3D GameObjects to include in the stagger list, with their target open scale.")]
    [SerializeField] private SceneObjectEntry[] _sceneObjects;

    [Header("Animation")]
    [Tooltip("How long each individual element's pop takes in seconds.")]
    [SerializeField] private float _elementDuration = 0.2f;

    [Tooltip("Delay between each item in the stagger list.")]
    [SerializeField] private float _staggerDelay = 0.06f;

    [Tooltip("Overshoot scale multiplier on open (e.g. 1.08 = 8% bounce). Set to 1 for no bounce.")]
    [SerializeField] private float _overshootScale = 1.08f;

    // ── Types ─────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class SceneObjectEntry
    {
        [Tooltip("The 3D GameObject to animate.")]
        public GameObject target;

        [Tooltip("The scale this object should have when fully open.")]
        public Vector3 openScale = Vector3.one;

        [Tooltip("Position in the stagger list (0 = first). UI elements fill the remaining slots in UXML order.")]
        public int staggerIndex = 0;

        [HideInInspector] public Vector3 _originalScale;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private UIDocument _document;
    private VisualElement _root;
    private List<VisualElement> _uiElements = new();
    private bool _isOpen = false;
    private Coroutine _activeAnimation;

    // A unified stagger slot — either a UI element or a 3D object
    private abstract class StaggerSlot { }
    private class UISlot : StaggerSlot { public VisualElement element; }
    private class ObjectSlot : StaggerSlot { public SceneObjectEntry entry; }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _document = GetComponent<UIDocument>();

        // Cache original scales of all 3D objects
        if (_sceneObjects != null)
            foreach (var entry in _sceneObjects)
                if (entry.target != null)
                    entry._originalScale = entry.target.transform.localScale;
    }

    private void OnEnable()
    {
        StartCoroutine(InitializeUI());
    }

    private IEnumerator InitializeUI()
    {
        yield return null; // let UIDocument build its visual tree

        _root = _document.rootVisualElement;

        if (_root == null)
        {
            Debug.LogError("[MenuToggleController] rootVisualElement is null. " +
                           "Make sure your UIDocument has a valid UXML asset assigned.");
            yield break;
        }

        RefreshUIElements();

        // Hide UI
        _root.style.display = DisplayStyle.None;
        foreach (var el in _uiElements)
        {
            SetUIScale(el, 0f);
            el.style.opacity = 0f;
        }

        // Hide 3D objects
        if (_sceneObjects != null)
            foreach (var entry in _sceneObjects)
                if (entry.target != null)
                    entry.target.transform.localScale = Vector3.zero;

        _isOpen = false;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Toggle()
    {
        if (_root == null) return;
        if (_isOpen) Close(); else Open();
    }

    public void Open()
    {
        if (_root == null || _isOpen) return;
        _isOpen = true;

        RefreshUIElements();

        if (_activeAnimation != null) StopCoroutine(_activeAnimation);
        _activeAnimation = StartCoroutine(AnimateOpen());
    }

    public void Close()
    {
        if (_root == null || !_isOpen) return;
        _isOpen = false;

        if (_activeAnimation != null) StopCoroutine(_activeAnimation);
        _activeAnimation = StartCoroutine(AnimateClose());
    }

    // ── Stagger list builder ──────────────────────────────────────────────────

    /// Builds a merged, ordered list of UI and 3D slots.
    /// 3D objects are inserted at their chosen staggerIndex; UI elements fill remaining positions in order.
    private List<StaggerSlot> BuildStaggerList()
    {
        var result = new List<StaggerSlot>();

        // Start with all UI elements as placeholders
        var uiQueue = new Queue<VisualElement>(_uiElements);

        // Determine total slot count
        int objectCount = 0;
        if (_sceneObjects != null)
            foreach (var e in _sceneObjects)
                if (e.target != null) objectCount++;

        int totalSlots = _uiElements.Count + objectCount;

        // Build a lookup of which slots are reserved for 3D objects
        var objectsAtIndex = new Dictionary<int, List<SceneObjectEntry>>();
        if (_sceneObjects != null)
        {
            foreach (var entry in _sceneObjects)
            {
                if (entry.target == null) continue;
                int idx = Mathf.Clamp(entry.staggerIndex, 0, totalSlots - 1);
                if (!objectsAtIndex.ContainsKey(idx))
                    objectsAtIndex[idx] = new List<SceneObjectEntry>();
                objectsAtIndex[idx].Add(entry);
            }
        }

        // Walk through slots and fill them
        int slot = 0;
        while (result.Count < totalSlots)
        {
            // Insert any 3D objects assigned to this slot
            if (objectsAtIndex.ContainsKey(slot))
            {
                foreach (var entry in objectsAtIndex[slot])
                    result.Add(new ObjectSlot { entry = entry });
            }

            // Fill with next UI element if available
            if (uiQueue.Count > 0)
                result.Add(new UISlot { element = uiQueue.Dequeue() });

            slot++;
        }

        return result;
    }

    // ── Animation coroutines ──────────────────────────────────────────────────

    private IEnumerator AnimateOpen()
    {
        _root.style.display = DisplayStyle.Flex;

        // Reset all to hidden
        foreach (var el in _uiElements) { SetUIScale(el, 0f); el.style.opacity = 0f; }
        if (_sceneObjects != null)
            foreach (var entry in _sceneObjects)
                if (entry.target != null)
                    entry.target.transform.localScale = Vector3.zero;

        var staggerList = BuildStaggerList();

        foreach (var slot in staggerList)
        {
            if (slot is UISlot ui)
                StartCoroutine(PopInUI(ui.element));
            else if (slot is ObjectSlot obj)
                StartCoroutine(PopInObject(obj.entry));

            yield return new WaitForSeconds(_staggerDelay);
        }

        yield return new WaitForSeconds(_elementDuration);
        _activeAnimation = null;
    }

    private IEnumerator AnimateClose()
    {
        var staggerList = BuildStaggerList();

        foreach (var slot in staggerList)
        {
            if (slot is UISlot ui)
                StartCoroutine(PopOutUI(ui.element));
            else if (slot is ObjectSlot obj)
                StartCoroutine(PopOutObject(obj.entry));

            yield return new WaitForSeconds(_staggerDelay);
        }

        yield return new WaitForSeconds(_elementDuration);
        _root.style.display = DisplayStyle.None;
        _activeAnimation = null;
    }

    // ── UI element animations ─────────────────────────────────────────────────

    private IEnumerator PopInUI(VisualElement el)
    {
        float halfDuration = _elementDuration * 0.6f;
        float bounceDuration = _elementDuration * 0.4f;
        float elapsed = 0f;

        el.style.opacity = 1f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            SetUIScale(el, Mathf.Lerp(0f, _overshootScale, EaseOutCubic(Mathf.Clamp01(elapsed / halfDuration))));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            SetUIScale(el, Mathf.Lerp(_overshootScale, 1f, EaseOutCubic(Mathf.Clamp01(elapsed / bounceDuration))));
            yield return null;
        }

        // Clear the inline scale so USS (including :hover transitions) takes back control
        el.style.scale = StyleKeyword.Null;
    }

    private IEnumerator PopOutUI(VisualElement el)
    {
        // Re-apply inline scale to take control back from USS before animating out
        SetUIScale(el, 1f);
        yield return null; // one frame for the style to settle

        float elapsed = 0f;

        while (elapsed < _elementDuration)
        {
            elapsed += Time.deltaTime;
            SetUIScale(el, Mathf.Lerp(1f, 0f, EaseInCubic(Mathf.Clamp01(elapsed / _elementDuration))));
            yield return null;
        }

        SetUIScale(el, 0f);
        el.style.opacity = 0f;
    }

    // ── 3D object animations ──────────────────────────────────────────────────

    private IEnumerator PopInObject(SceneObjectEntry entry)
    {
        Transform t = entry.target.transform;
        Vector3 targetScale = entry.openScale;
        Vector3 overshootTarget = targetScale * _overshootScale;

        float halfDuration = _elementDuration * 0.6f;
        float bounceDuration = _elementDuration * 0.4f;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            t.localScale = Vector3.Lerp(Vector3.zero, overshootTarget, EaseOutCubic(Mathf.Clamp01(elapsed / halfDuration)));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            t.localScale = Vector3.Lerp(overshootTarget, targetScale, EaseOutCubic(Mathf.Clamp01(elapsed / bounceDuration)));
            yield return null;
        }

        t.localScale = targetScale;
    }

    private IEnumerator PopOutObject(SceneObjectEntry entry)
    {
        Transform t = entry.target.transform;
        Vector3 startScale = t.localScale;
        float elapsed = 0f;

        while (elapsed < _elementDuration)
        {
            elapsed += Time.deltaTime;
            t.localScale = Vector3.Lerp(startScale, Vector3.zero, EaseInCubic(Mathf.Clamp01(elapsed / _elementDuration)));
            yield return null;
        }

        t.localScale = Vector3.zero;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RefreshUIElements()
    {
        _uiElements.Clear();
        _uiElements.AddRange(_root.Query<VisualElement>(className: _animationClass).ToList());
    }

    private static void SetUIScale(VisualElement el, float scale)
    {
        el.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0f);
        el.style.scale = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseInCubic(float t) => t * t * t;
}