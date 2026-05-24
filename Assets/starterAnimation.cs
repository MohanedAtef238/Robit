using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

public class UIBubbleEntry : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private float staggerDelay = 0.05f;

    private bool _hasAnimated = false;
    private bool _isRegistered = false;

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        TryRegisterGeometryCallback();
    }

    void Start()
    {
        TryRegisterGeometryCallback();
    }

    void OnDisable()
    {
        TryUnregisterGeometryCallback();
    }

    private void TryRegisterGeometryCallback()
    {
        if (_isRegistered || uiDocument == null) return;

        var root = uiDocument.rootVisualElement;
        if (root != null)
        {
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _isRegistered = true;
        }
    }

    private void TryUnregisterGeometryCallback()
    {
        if (!_isRegistered || uiDocument == null) return;

        var root = uiDocument.rootVisualElement;
        if (root != null)
        {
            root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }
        _isRegistered = false;
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        TryUnregisterGeometryCallback();

        if (!_hasAnimated)
        {
            StartCoroutine(AnimateElements());
        }
    }

    public void ReplayAnimation()
    {
        Debug.Log($"[UIBubbleEntry] ReplayAnimation called. GameObject activeSelf: {gameObject.activeSelf}, enabled: {enabled}");
        StopAllCoroutines();
        
        // Instead of guessing frame counts, register for a geometry change event 
        // to fire exactly when UI Toolkit has resolved the display:Flex layout.
        TryRegisterGeometryCallback();
    }

    public void ResetAnimation()
    {
        Debug.Log("[UIBubbleEntry] ResetAnimation called.");
        StopAllCoroutines();
        _hasAnimated = false;
        
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            var elements = uiDocument.rootVisualElement.Query(className: "bubble-element").ToList();
            Debug.Log($"[UIBubbleEntry] Resetting {elements.Count} elements (removing bubble-active class).");
            foreach (var el in elements)
            {
                el.RemoveFromClassList("bubble-active");
            }
        }
    }

    private IEnumerator AnimateElements()
    {
        _hasAnimated = true;

        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogWarning("[UIBubbleEntry] AnimateElements aborted because uiDocument or rootVisualElement is null.");
            yield break;
        }

        var root = uiDocument.rootVisualElement;
        var elements = root.Query(className: "bubble-element").ToList();

        Debug.Log($"[UIBubbleEntry] AnimateElements: Ensuring all {elements.Count} elements start at baseline...");
        foreach (var el in elements)
        {
            el.RemoveFromClassList("bubble-active");
            el.RemoveFromClassList("no-transition");
            
            // Debug the raw resolved style values
            Debug.Log($"[UIBubbleEntry] Element {el.name} baseline - opacity: {el.resolvedStyle.opacity}, display: {el.resolvedStyle.display}");
        }

        // Wait a tiny fraction of a second to ensure the UI Toolkit style matching has settled
        // after the geometry event fired.
        yield return new WaitForSecondsRealtime(0.02f);

        Debug.Log($"[UIBubbleEntry] AnimateElements: Stagger animating {elements.Count} elements...");
        foreach (var el in elements)
        {
            Debug.Log($"[UIBubbleEntry] Animating element: {el.name} ({el.viewDataKey}) -> Adding bubble-active");
            el.AddToClassList("bubble-active");
            yield return new WaitForSecondsRealtime(staggerDelay);
        }
        Debug.Log("[UIBubbleEntry] AnimateElements: Completed animation stagger loop.");
    }
}