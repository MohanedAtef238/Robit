using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MacroButtonController : MonoBehaviour
{
    [SerializeField] private List<MacroButtonBinding> bindings = new();

    private UIDocument uiDocument;
    private readonly List<MacroButton> boundButtons = new();
    private IInputProvider inputProvider;

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

        Debug.Log($"[MacroButtonController] Registered {boundButtons.Count} macro buttons");
    }

    void OnDisable()
    {
        foreach (var mb in boundButtons)
            mb.Unbind();

        boundButtons.Clear();
    }
}
