using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

public class UIBubbleEntry : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private float staggerDelay = 0.1f;

    private bool _hasAnimated = false;

    void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();

        // Wait for the UI to actually be laid out in the scene
        uiDocument.rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        // Unregister so it only triggers once per enable
        uiDocument.rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);

        if (!_hasAnimated)
        {
            StartCoroutine(AnimateElements());
        }
    }

    private IEnumerator AnimateElements()
    {
        _hasAnimated = true;
        var root = uiDocument.rootVisualElement;
        var elements = root.Query(className: "bubble-element").ToList();

        // Safety: Ensure they start at zero
        foreach (var el in elements)
        {
            el.RemoveFromClassList("bubble-active");
        }

        // Short frame buffer
        yield return null;

        foreach (var el in elements)
        {
            el.AddToClassList("bubble-active");
            yield return new WaitForSeconds(staggerDelay);
        }
    }
}