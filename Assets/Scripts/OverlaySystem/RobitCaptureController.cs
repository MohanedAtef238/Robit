using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the capture overlay panel.
/// The panel only opens when explicitly called by AppLauncher; 
/// Robit clicks no longer trigger it.
/// </summary>
[AddComponentMenu("Mock OS/Robit Capture Controller")]
public class RobitCaptureController : MonoBehaviour
{
    [Header("Panel References")]
    public Animator panelAnimator;
    public RawImage captureDisplay;

    [Header("Hole-Punch Settings")]
    public HolePunchController holePunchController;

    [Header("Animation Parameters")]
    [SerializeField] private string openTrigger  = "open";
    [SerializeField] private string closeTrigger = "close";

    private Camera mainCamera;
    private bool isPanelOpen = false;

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindFirstObjectByType<Camera>();

        if (holePunchController == null && captureDisplay != null)
        {
            holePunchController = captureDisplay.GetComponent<HolePunchController>();
            if (holePunchController == null)
                holePunchController = captureDisplay.gameObject.AddComponent<HolePunchController>();

            holePunchController.panelRect = captureDisplay.rectTransform;
            holePunchController.renderCamera = mainCamera;
        }
    }

    // No Update() — we no longer detect clicks on the Robit here.
    // The overlay is only opened programmatically via OpenPanel().

    public void OpenPanel()
    {
        isPanelOpen = true;
        if (panelAnimator != null)
            panelAnimator.SetTrigger(openTrigger);
    }

    public void ClosePanel()
    {
        isPanelOpen = false;

        if (holePunchController != null)
            holePunchController.Deactivate();

        if (panelAnimator != null)
            panelAnimator.SetTrigger(closeTrigger);
    }

    void OnDestroy()
    {
        if (isPanelOpen && holePunchController != null)
            holePunchController.Deactivate();
    }
}
