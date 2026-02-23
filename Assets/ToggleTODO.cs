using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TogglePanelColor : MonoBehaviour, IPointerClickHandler
{
    [Header("Color Settings")]
    public Color colorA = Color.white;
    public Color colorB = Color.green;

    private Image panelImage;
    private bool isColorA = true;

    void Awake()
    {
        panelImage = GetComponent<Image>();
        panelImage.color = colorA;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        isColorA = !isColorA;
        panelImage.color = isColorA ? colorA : colorB;
    }
}
