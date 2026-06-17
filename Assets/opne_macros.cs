using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider))]
public class WorldObjectUiTrigger : MonoBehaviour
{
    [Header("UI Document Link")]
    [Tooltip("Drag your scene's UIDocument component here.")]
    [SerializeField] private UIDocument uiDocument;

    [Tooltip("The exact Name of the main wrapper container inside your UXML file.")]
    [SerializeField] private string targetPanelName = "home-panel";

    private Animator _animator;
    private VisualElement _uiRootPanel;
    private bool _isUiVisible = false;

    void Start()
    {
        _animator = GetComponent<Animator>();

        if (uiDocument == null)
        {
            Debug.LogError($"[WorldObjectUiTrigger] UIDocument reference missing on {gameObject.name}!", this);
            return;
        }

        var root = uiDocument.rootVisualElement;
        if (root != null)
        {
            // Print out all top-level elements to help you find the correct name
            Debug.Log("[WorldObjectUiTrigger] Scanning UXML top-level elements:");
            // Add .ToList() right here
            root.Children().ToList().ForEach(child => Debug.Log($" -> Found Element Name: '{child.name}' (Type: {child.GetType().Name})"));
            // Attempt to find the specific container
            _uiRootPanel = root.Q(targetPanelName);

            if (_uiRootPanel == null)
            {
                // FORCE FALLBACK: If the name doesn't match, control the absolute root layout
                _uiRootPanel = root;
                Debug.LogWarning($"[WorldObjectUiTrigger] Could not find '{targetPanelName}'. Forced fallback to the absolute root element so it works anyway.");
            }

            // Ensure it starts hidden
            _uiRootPanel.style.display = DisplayStyle.None;
        }
    }

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            ProcessRaycastClick();
        }
    }

    private void ProcessRaycastClick()
    {
        if (Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                ExecuteTriggerActions();
            }
        }
    }

    private void ExecuteTriggerActions()
    {
        if (_animator != null)
        {
            _animator.SetTrigger("ClickTrigger");
        }

        if (_uiRootPanel != null)
        {
            _isUiVisible = !_isUiVisible;
            _uiRootPanel.style.display = _isUiVisible ? DisplayStyle.Flex : DisplayStyle.None;
            Debug.Log($"[WorldObjectUiTrigger] UI Display toggled. DisplayStyle is now: {_uiRootPanel.style.display.value}");
        }
    }
}