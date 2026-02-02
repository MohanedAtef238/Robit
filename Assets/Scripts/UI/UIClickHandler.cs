using UnityEngine;
using UnityEngine.EventSystems;

public class UIClickHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Transparency transparency;

    void Start()
    {
        // To use this script:
        // 1. Attach this script to all of your UI elements in the "OverlayScene".
        // 2. Make sure the main camera in the "OverlayScene" has the "Transparency" script attached.
        // 3. Ensure there is an EventSystem in the scene.

        transparency = Camera.main.GetComponent<Transparency>();
        if (transparency == null)
        {
            Debug.LogError("Transparency script not found on the main camera!");
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
