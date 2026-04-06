using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;

[RequireComponent(typeof(BoxCollider))]
public class RobitInteraction : MonoBehaviour, IPointerClickHandler
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
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // When the HomePage is open, clicking robit toggles the dialogue bubble
        if (homePageController != null && homePageController.IsOpen)
        {
            homePageController.ToggleDialogue();
            return;
        }

        // In HomeScene (no MacroButtonController): always navigate back to OverlayScene.
        // Note: the UI Toolkit PanelRaycaster normally intercepts pointer events before
        // they reach 3D colliders, so the dedicated back button in AppLauncherMain.uxml
        // is the primary return path. This branch fires only if the panel is hidden.
        if (macroController == null)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("OverlayScene");
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
}
