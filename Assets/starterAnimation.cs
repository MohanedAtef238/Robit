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
        StopAllCoroutines();
        StartCoroutine(AnimateElements());
    }

    public void ResetAnimation()
    {
        StopAllCoroutines();
        _hasAnimated = false;
        
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            var elements = uiDocument.rootVisualElement.Query(className: "bubble-element").ToList();
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
            yield break;

        var root = uiDocument.rootVisualElement;

        // 1. Wait two frames to allow UI Toolkit to register display changes and layout passes
        yield return null;
        yield return null;

        // 2. Query elements now that the display has fully transitioned to 'Flex'
        var elements = root.Query(className: "bubble-element").ToList();

        // 3. Reset all elements to baseline 'opacity: 0' and 'scale: 0 0'
        foreach (var el in elements)
        {
            el.RemoveFromClassList("bubble-active");
        }

        // 4. Wait 1 frame so the transition engine registers this baseline state
        yield return null;

        // 5. Stagger-animate elements into view by adding the class
        foreach (var el in elements)
        {
            el.AddToClassList("bubble-active");
            yield return new WaitForSecondsRealtime(staggerDelay);
        }
    }
}