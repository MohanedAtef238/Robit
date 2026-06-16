using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;

[RequireComponent(typeof(BoxCollider))]
public class RobitInteraction : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private MacroButtonController macroController;
    [SerializeField] private HomePageController homePageController;

    [Header("Menu Anchor (world-space offset from mascot pivot)")]
    [Tooltip("In world units. For a 1-unit-tall mascot, Y ≈ 0.8 puts the arc above the head.")]
    [SerializeField] private Vector3 menuAnchorOffset = new Vector3(0f, 0.1f, 0f);

    [Header("Fine-tune (panel-space pixel offset applied AFTER projection)")]
    [Tooltip("X shifts left/right, Y shifts up (negative) or down (positive) in pixels.")]
    [SerializeField] private Vector2 panelPixelOffset = new Vector2(0f, 211f);

    private bool _menuOpen;

    void Start()
    {
        if (macroController == null)
            macroController = Object.FindFirstObjectByType<MacroButtonController>();
        if (homePageController == null)
            homePageController = Object.FindFirstObjectByType<HomePageController>();

        // IPointerClickHandler requires a PhysicsRaycaster on the camera to detect 3D collider
        // clicks through the EventSystem. Add one programmatically if absent.
        Camera cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
        if (cam != null && cam.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
            cam.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();

        // Ensure an EventSystem exists — required for both PhysicsRaycaster and UI Toolkit
        // PanelEventHandler to route pointer events.
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // When the HomePage is open, clicking robit toggles the dialogue bubble
        if (homePageController != null && homePageController.IsOpen)
        {
            homePageController.ToggleDialogue();
            return;
        }

        // Default behavior: toggle the macro menu
        _menuOpen = !_menuOpen;
        if (_menuOpen)
        {
            macroController.ShowWithBounceAtWorldPosition(
                transform.position + menuAnchorOffset,
                panelPixelOffset,
                eventData.pressEventCamera);
        }
        else
        {
            macroController.HideWithShrink();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        GlobalCursorManager.Instance?.SetHoverCursor();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        GlobalCursorManager.Instance?.SetDefaultCursor();
    }
}

