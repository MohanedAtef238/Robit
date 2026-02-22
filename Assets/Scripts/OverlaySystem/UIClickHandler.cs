using UnityEngine;
using UnityEngine.EventSystems;

public class UIClickHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Transparency transparency;

    void Start()
    {
        transparency = FindFirstObjectByType<Transparency>();
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
        if (transparency != null && transparency.enabled)
            transparency.SetClickThrough(true);
    }
}
