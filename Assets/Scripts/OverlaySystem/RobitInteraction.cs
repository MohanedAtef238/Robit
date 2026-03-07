using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

[RequireComponent(typeof(BoxCollider))]
public class RobitInteraction : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private UIDocument appLauncherUIDocument;
    
    private VisualElement rootParams;
    private VisualElement appLauncherRoot;
    private Camera mainCamera;
    private bool isUiVisible = true;

    void Start()
    {
        mainCamera = Camera.main;
        
        if (uiDocument == null)
            uiDocument = FindFirstObjectByType<UIDocument>();

        if (uiDocument != null)
            rootParams = uiDocument.rootVisualElement;
        else
            Debug.LogError("[RobitInteraction] No Mascot UIDocument found in the scene to toggle.");

        if (appLauncherUIDocument != null)
        {
            appLauncherRoot = appLauncherUIDocument.rootVisualElement;
            if (isUiVisible) 
                appLauncherRoot.style.display = DisplayStyle.None;
        }
        else
        {
            Debug.LogWarning("[RobitInteraction] No AppLauncher UIDocument assigned to toggle.");
        }
    }

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            CheckClickOnModel();
    }

    private void CheckClickOnModel()
    {
        if (mainCamera == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider != null && hit.collider.gameObject == gameObject)
                ToggleUI();
        }
    }

    private void ToggleUI()
    {
        isUiVisible = !isUiVisible;

        if (rootParams != null)
            rootParams.style.display = isUiVisible ? DisplayStyle.Flex : DisplayStyle.None;

        if (appLauncherRoot != null)
            appLauncherRoot.style.display = !isUiVisible ? DisplayStyle.Flex : DisplayStyle.None;

        Debug.Log($"[RobitInteraction] Toggled Mascot UI: {isUiVisible}, AppLauncher UI: {!isUiVisible}");
    }
}
