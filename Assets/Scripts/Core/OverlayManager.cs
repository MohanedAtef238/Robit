using UnityEngine;

public class OverlayManager : MonoBehaviour
{
    [Header("Overlay Dimensions")]
    [SerializeField] private float widthPercent = 0.3f;
    [SerializeField] private float heightPercent = 0.3f;
    
    [Header("Position")]
    [SerializeField] private bool anchorBottomRight = true;

    void Start()
    {
        #if !UNITY_EDITOR
        int screenWidth = Screen.currentResolution.width;
        int screenHeight = Screen.currentResolution.height;
        
        int windowWidth = Mathf.RoundToInt(screenWidth * widthPercent);
        int windowHeight = Mathf.RoundToInt(screenHeight * heightPercent);
        
        int x = 0;
        int y = 0;
        
        if (anchorBottomRight)
        {
            x = screenWidth - windowWidth;
            y = screenHeight - windowHeight;
        }
        
        WindowManager.SetWindowPosition(x, y, windowWidth, windowHeight);
        #endif
    }
}
