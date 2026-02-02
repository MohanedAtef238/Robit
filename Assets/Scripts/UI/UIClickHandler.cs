using UnityEngine;
using UnityEngine.EventSystems;

public class UIClickHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Transparency transparency;

    void Start()
    {
        // To use this script:
        // 1. Attach this script to all of your UI elements in the "OverlayScene".
        // 2. Make sure the "Transparency" script exists somewhere in the scene.
        // 3. Ensure there is an EventSystem in the scene.

        transparency = FindObjectOfType<Transparency>();
        if (transparency == null)
        {
            Debug.LogError("Transparency script not found in the scene! Make sure it's attached to a GameObject.");
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (transparency != null)
        {
            transparency.SetClickThrough(false);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (transparency != null)
        {
            transparency.SetClickThrough(true);
        }
    }
}
