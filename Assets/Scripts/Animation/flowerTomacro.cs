using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider))]
public class FlowerMacroController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign your UI Macro Button Controller here.")]
    public MacroButtonController macroController;

    [Header("Settings")]
    [Tooltip("The offset of the dynamic UI popping up relative to the flower.")]
    public Vector2 uiOffset = new Vector2(0, -60f);

    private Animator _animator;
    private bool isMenuOpen = false; // Tracks the true toggle state

    void Start()
    {
        _animator = GetComponent<Animator>();

        if (macroController == null)
            Debug.LogError($"[FlowerMacroController] Please assign the MacroButtonController on {gameObject.name}!", this);
    }

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            CheckForObjectClick();
        }
    }

    private void CheckForObjectClick()
    {
        if (Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Register click if hitting this object or its sub-meshes
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                ToggleUI();
            }
        }
    }

    private void ToggleUI()
    {
        if (macroController == null) return;

        // 1. Fire the Animator State Machine Trigger ("ClickTrigger") regardless of opening or closing
        if (_animator != null)
        {
            _animator.SetTrigger("ClickTrigger");
        }

        // 2. Drive the Macro UI System based on the toggle state
        if (!isMenuOpen)
        {
            // OPEN: Fan out the UI elements precisely at the flower's location
            macroController.ShowWithBounceAtWorldPosition(transform.position, uiOffset, Camera.main);
            isMenuOpen = true;
            Debug.Log($"[{gameObject.name}] UI Opened via Macro System");
        }
        else
        {
            // CLOSE: Run the dynamic shrink animation
            macroController.HideWithShrink();
            isMenuOpen = false;
            Debug.Log($"[{gameObject.name}] UI Closed via Macro System");
        }
    }

    /// <summary>
    /// Keeps state synced if an external close button or Home Page event triggers a layout reset.
    /// </summary>
    public void ForceSetMenuState(bool open)
    {
        isMenuOpen = open;
    }
}