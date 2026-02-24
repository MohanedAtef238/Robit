using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(BoxCollider))]
public class RobitInteraction : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    
    private VisualElement rootParams;
    private Camera mainCamera;
    private bool isUiVisible = true;

    void Start()
    {
        mainCamera = Camera.main;
        
        if (uiDocument == null)
        {
            uiDocument = FindFirstObjectByType<UIDocument>();
        }

        if (uiDocument != null)
        {
            rootParams = uiDocument.rootVisualElement;
        }
        else
        {
            Debug.LogError("[RobitInteraction] No UIDocument found in the scene to toggle.");
        }
    }

    void Update()
    {
        // Detect click using the new Input System
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            CheckClickOnModel();
        }
    }

    private void CheckClickOnModel()
    {
        if (mainCamera == null) return;

        // Create a ray from the mouse position
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        // Perform raycast
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // If the ray hit this exact GameObject (the robit)
            if (hit.collider != null && hit.collider.gameObject == gameObject)
            {
                ToggleUI();
            }
        }
    }

    private void ToggleUI()
    {
        if (rootParams == null) return;

        isUiVisible = !isUiVisible;
        rootParams.style.display = isUiVisible ? DisplayStyle.Flex : DisplayStyle.None;
        
        Debug.Log($"[RobitInteraction] Toggled UI Visibility to: {isUiVisible}");
    }
}
