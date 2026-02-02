using UnityEngine;
using UnityEngine.EventSystems;

public class UIClickHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        #if !UNITY_EDITOR
        WindowManager.SetClickThrough(false);
        #endif
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        #if !UNITY_EDITOR
        WindowManager.SetClickThrough(true);
        #endif
    }
}
