using UnityEngine;
using UnityEngine.EventSystems;

public class HoverCursorUGUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (GlobalCursorManager.Instance != null)
        {
            GlobalCursorManager.Instance.SetHoverCursor();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (GlobalCursorManager.Instance != null)
        {
            GlobalCursorManager.Instance.SetDefaultCursor();
        }
    }
}
