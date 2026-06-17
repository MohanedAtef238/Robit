using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

public class UIBubbleEntry : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private float staggerDelay = 0.05f;
    [SerializeField] private float animationDurationSeconds = 0.35f;

    private List<VisualElement> _bubbleElements = new List<VisualElement>();
    private Coroutine _activeAnimationCoroutine;

    // No OnEnable, No Start, No Update. Purely controlled by HomePageController.

    public void ResetAnimation()
    {
        StopAllCoroutines();
        _activeAnimationCoroutine = null;

        FetchBubbleElements();

        foreach (var el in _bubbleElements)
        {
            if (el == null) continue;

            // Wipe from display immediately
            el.style.display = DisplayStyle.None;
            el.style.opacity = 0f;
            el.style.scale = new Scale(new Vector3(0.01f, 0.01f, 1f));
        }
    }

    public void ReplayAnimation()
    {
        StopAllCoroutines();

        ResetAnimation();
        _activeAnimationCoroutine = StartCoroutine(AnimateMenuState(true));
    }

    private void FetchBubbleElements()
    {
        _bubbleElements.Clear();
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();

        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            _bubbleElements = uiDocument.rootVisualElement.Query(className: "bubble-element").ToList();
        }
    }

    private IEnumerator AnimateMenuState(bool open)
    {
        FetchBubbleElements();

        foreach (var el in _bubbleElements)
        {
            if (el == null) continue;
            StartCoroutine(AnimateSingleElement(el, open));
            yield return new WaitForSecondsRealtime(staggerDelay);
        }
    }

    private IEnumerator AnimateSingleElement(VisualElement el, bool open)
    {
        float startOpacity = open ? 0f : 1f;
        float endOpacity = open ? 1f : 0f;
        float startScale = open ? 0.01f : 1f;
        float endScale = open ? 1f : 0.01f;

        el.style.display = DisplayStyle.Flex;
        el.style.opacity = startOpacity;
        el.style.scale = new Scale(new Vector3(startScale, startScale, 1f));

        float elapsedTime = 0f;
        while (elapsedTime < animationDurationSeconds)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / animationDurationSeconds);
            float evaluatedT = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f); // EaseOutBack Math

            el.style.opacity = Mathf.Lerp(startOpacity, endOpacity, t);
            float scaleVal = Mathf.Lerp(startScale, endScale, evaluatedT);
            el.style.scale = new Scale(new Vector3(scaleVal, scaleVal, 1f));

            yield return null;
        }

        el.style.opacity = endOpacity;
        el.style.scale = new Scale(new Vector3(endScale, endScale, 1f));
    }
}