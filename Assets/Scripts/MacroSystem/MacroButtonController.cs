using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MacroButtonController : MonoBehaviour
{
    [SerializeField] private List<MacroButtonBinding> bindings = new();

    [Header("Entrance Animation")]
    [SerializeField] private float staggerDelay = 0.08f;
    [SerializeField] private float bounceDuration = 0.5f;

    private UIDocument uiDocument;
    private VisualElement macroContainer;
    private readonly List<MacroButton> boundButtons = new();
    private IInputProvider inputProvider;
    private Coroutine animationCoroutine;

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

        foreach (var binding in bindings)
        {
            if (string.IsNullOrEmpty(binding.buttonName))
                continue;

            var element = root.Q<MacroButton>(binding.buttonName);
            if (element == null)
            {
                Debug.LogWarning($"[MacroButtonController] '{binding.buttonName}' not found");
                continue;
            }

            var action = MacroActionFactory.Create(binding.actionType);
            element.Bind(action, inputProvider);
            boundButtons.Add(element);
        }

        // Start hidden
        HideImmediate();
        ShowWithBounce();
        Debug.Log($"[MacroButtonController] Registered {boundButtons.Count} macro buttons");
    }

    void OnDisable()
    {
        foreach (var mb in boundButtons)
            mb.Unbind();

        boundButtons.Clear();
    }

    /// <summary>
    /// Instantly hides all buttons (no animation).
    /// </summary>
    public void HideImmediate()
    {
        foreach (var btn in boundButtons)
        {
            btn.style.scale = new Scale(Vector2.zero);
            btn.style.opacity = 0f;
        }
        if (macroContainer != null)
            macroContainer.style.display = DisplayStyle.None;
    }

    /// <summary>
    /// Plays a staggered bouncy entrance — buttons pop in one by one
    /// as if thrown from the Robit.
    /// </summary>
    public void ShowWithBounce()
    {
        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        if (macroContainer != null)
            macroContainer.style.display = DisplayStyle.Flex;

        animationCoroutine = StartCoroutine(BounceInRoutine());
    }

    /// <summary>
    /// Plays a staggered shrink-out animation, then hides the container.
    /// </summary>
    public void HideWithShrink()
    {
        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        animationCoroutine = StartCoroutine(ShrinkOutRoutine());
    }

    private IEnumerator BounceInRoutine()
    {
        // Reset all buttons to hidden state
        foreach (var btn in boundButtons)
        {
            btn.style.scale = new Scale(Vector2.zero);
            btn.style.opacity = 0f;
        }

        yield return null; // let layout settle

        for (int i = 0; i < boundButtons.Count; i++)
        {
            StartCoroutine(AnimateBounceIn(boundButtons[i]));
            yield return new WaitForSeconds(staggerDelay);
        }
    }

    private IEnumerator ShrinkOutRoutine()
    {
        for (int i = boundButtons.Count - 1; i >= 0; i--)
        {
            StartCoroutine(AnimateShrinkOut(boundButtons[i]));
            yield return new WaitForSeconds(staggerDelay);
        }

        yield return new WaitForSeconds(bounceDuration);

        if (macroContainer != null)
            macroContainer.style.display = DisplayStyle.None;
    }

    private IEnumerator AnimateBounceIn(MacroButton btn)
    {
        float elapsed = 0f;

        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);
            float s = BounceEaseOut(t);

            btn.style.scale = new Scale(new Vector2(s, s));
            btn.style.opacity = Mathf.Clamp01(t * 3f); // fade in quickly
            yield return null;
        }

        btn.style.scale = new Scale(Vector2.one);
        btn.style.opacity = 1f;
    }

    private IEnumerator AnimateShrinkOut(MacroButton btn)
    {
        float elapsed = 0f;
        float duration = bounceDuration * 0.6f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float s = 1f - t;

            btn.style.scale = new Scale(new Vector2(s, s));
            btn.style.opacity = 1f - t;
            yield return null;
        }

        btn.style.scale = new Scale(Vector2.zero);
        btn.style.opacity = 0f;
    }

    /// <summary>
    /// A bounce easing function that overshoots past 1.0 and settles back.
    /// </summary>
    private static float BounceEaseOut(float t)
    {
        if (t < 0.3636f)
            return 7.5625f * t * t;
        else if (t < 0.7273f)
            return 7.5625f * (t - 0.5455f) * (t - 0.5455f) + 0.75f;
        else if (t < 0.9091f)
            return 7.5625f * (t - 0.8182f) * (t - 0.8182f) + 0.9375f;
        else
            return 7.5625f * (t - 0.9545f) * (t - 0.9545f) + 0.984375f;
    }
}
