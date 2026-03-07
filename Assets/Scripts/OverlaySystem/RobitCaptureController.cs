using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Controls the capture overlay panel that emerges from the robit (frog).
///
/// When the frog is clicked:
///   1. The "open" trigger fires on the panel's Animator, playing the pop-out animation
///   2. The HolePunchController activates, launching/finding the target app
///      and positioning it behind a transparent hole in the Unity window
///
/// When clicked again:
///   1. The "close" trigger fires, animating the panel away
///   2. The HolePunchController deactivates, restoring the Unity window
///
/// Attach this to the frog root ("robirt_text") which must have a collider.
/// </summary>
[RequireComponent(typeof(Collider))]
[AddComponentMenu("Mock OS/Robit Capture Controller")]
public class RobitCaptureController : MonoBehaviour
{
    [Header("Panel References")]
    [Tooltip("The Animator on the Image panel (child of Canvas) with 'open' and 'close' triggers.")]
    public Animator panelAnimator;

    [Tooltip("RawImage on the panel where the app will appear behind (used for positioning).")]
    public RawImage captureDisplay;

    [Header("Hole-Punch Settings")]
    [Tooltip("The HolePunchController that manages the native window positioning.")]
    public HolePunchController holePunchController;

    [Header("Animation Parameters")]
    [SerializeField] private string openTrigger  = "open";
    [SerializeField] private string closeTrigger = "close";

    // ── State ──────────────────────────────────────────────────────────────────
    private Camera mainCamera;
    private bool isPanelOpen = false;

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
            if (mainCamera == null)
                Debug.LogError("[RobitCaptureController] No camera found in the scene.");
        }

        if (GetComponent<Collider>() == null)
            Debug.LogError("[RobitCaptureController] NO COLLIDER — clicks will never register.");

        if (panelAnimator == null)
            Debug.LogError("[RobitCaptureController] panelAnimator is NOT assigned!");

        // Auto-wire HolePunchController if not assigned
        if (holePunchController == null && captureDisplay != null)
        {
            holePunchController = captureDisplay.GetComponent<HolePunchController>();
            if (holePunchController == null)
            {
                holePunchController = captureDisplay.gameObject.AddComponent<HolePunchController>();
            }
            // Wire up the panel rect and camera
            holePunchController.panelRect = captureDisplay.rectTransform;
            holePunchController.renderCamera = mainCamera;
        }
    }

    void Update()
    {
        if (Mouse.current == null || mainCamera == null) return;

        // Don't process clicks when Unity doesn't have focus
        if (!Application.isFocused) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
            CheckClickOnRobit();
    }

    // ── Click Detection ────────────────────────────────────────────────────────

    private void CheckClickOnRobit()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        // Use RaycastAll so that tree / environment colliders don't block clicks
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        if (hits.Length == 0) return;

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
            {
                TogglePanel();
                return;
            }
        }
    }

    // ── Panel Toggle ───────────────────────────────────────────────────────────

    public void TogglePanel()
    {
        isPanelOpen = !isPanelOpen;

        if (isPanelOpen)
            OpenPanel();
        else
            ClosePanel();
    }

    public void OpenPanel()
    {
        isPanelOpen = true;

        // Fire the animation
        if (panelAnimator != null)
            panelAnimator.SetTrigger(openTrigger);

        // Activate hole-punch after a short delay to let the animation play
        if (holePunchController != null)
        {
            // Give the panel animation time to open before cutting the hole
            StartCoroutine(DelayedActivate(0.5f));
        }
    }

    public void ClosePanel()
    {
        isPanelOpen = false;

        // Deactivate hole-punch immediately
        if (holePunchController != null)
            holePunchController.Deactivate();

        // Fire the close animation
        if (panelAnimator != null)
            panelAnimator.SetTrigger(closeTrigger);
    }

    private System.Collections.IEnumerator DelayedActivate(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (isPanelOpen && holePunchController != null)
            holePunchController.Activate();
    }

    // ── Focus handling ──────────────────────────────────────────────────────────

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && isPanelOpen)
            Debug.Log("[RobitCaptureController] Lost focus — hole-punch continues");
        else if (hasFocus && isPanelOpen)
            Debug.Log("[RobitCaptureController] Regained focus");
    }

    void OnDestroy()
    {
        if (isPanelOpen && holePunchController != null)
            holePunchController.Deactivate();
    }

    // ── Editor Gizmos ──────────────────────────────────────────────────────────
    void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.35f);
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }

    void OnDrawGizmosSelected()
    {
        var col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}
