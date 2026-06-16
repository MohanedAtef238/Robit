using UnityEngine;
using UnityEngine.InputSystem;

public class ToggleMacroCube : MonoBehaviour
{
    [Header("References")]
    public MacroButtonController macroController;

    [Header("Settings")]
    public Vector2 uiOffset = new Vector2(0, -60f);
    public float clickBounceForce = 0.3f;

    private Vector3 startPos;
    private float currentBounce;
    private bool isMenuOpen = false; // Tracks the toggle state

    void Start()
    {
        startPos = transform.position;
        if (macroController == null)
            Debug.LogError("Please assign the MacroButtonController!");
    }

    void Update()
    {
        // Visual feedback: Smoothly return cube to original height
        currentBounce = Mathf.Lerp(currentBounce, 0, Time.deltaTime * 10f);
        transform.position = startPos + new Vector3(0, currentBounce, 0);

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            CheckForObjectClick();
        }
    }

    private void CheckForObjectClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.transform == transform)
            {
                ToggleUI();
            }
        }
    }

    private void ToggleUI()
    {
        if (macroController == null) return;

        // Trigger the "thump" animation regardless of opening or closing
        currentBounce = clickBounceForce;

        if (!isMenuOpen)
        {
            // OPEN: Fan out the buttons
            macroController.ShowWithBounceAtWorldPosition(transform.position, uiOffset, Camera.main);
            isMenuOpen = true;
            Debug.Log("Mock OS Menu: Opened");
        }
        else
        {
            // CLOSE: Shrink the buttons back down
            macroController.HideWithShrink();
            isMenuOpen = false;
            Debug.Log("Mock OS Menu: Closed");
        }
    }

    // Optional: Reset state if something else closes the menu (like clicking a home button)
    public void ForceSetMenuState(bool open)
    {
        isMenuOpen = open;
    }
}