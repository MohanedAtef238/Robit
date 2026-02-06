using UnityEngine;
using UnityEngine.UI;

// Displays a stylized cursor at the user's gaze position
// Assign a RectTransform (e.g., a UI Image) to cursorVisual in the Inspector
public class GazeCursor : MonoBehaviour
{
    [Header("Cursor Visual")]
    [Tooltip("Assign a UI RectTransform to display at gaze position")]
    [SerializeField] private RectTransform cursorVisual;
    
    [Header("Smoothing")]
    [SerializeField] private float smoothTime = 0.08f;
    
    [Header("Auto-Create Settings")]
    [SerializeField] private bool autoCreateIfMissing = true;
    [SerializeField] private float cursorSize = 30f;
    [SerializeField] private Color cursorColor = new Color(1f, 0.3f, 0.3f, 0.8f);
    
    private Vector2 velocity;
    private Canvas parentCanvas;
    private RectTransform canvasRect;
    
    private void Start()
    {
        if (cursorVisual == null && autoCreateIfMissing)
        {
            CreateCursorVisual();
        }
        
        if (cursorVisual != null)
        {
            parentCanvas = cursorVisual.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                canvasRect = parentCanvas.GetComponent<RectTransform>();
            }
        }
    }
    
    private void Update()
    {
        if (cursorVisual == null) return;
        if (EyeTrackingInput.Instance == null) return;
        
        // Get gaze position in screen space
        Vector2 gazeScreen = EyeTrackingInput.Instance.GazePosition;
        
        // Convert screen position to canvas local position
        Vector2 targetPos;
        if (parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // For overlay canvas, screen position works directly
            targetPos = gazeScreen;
        }
        else if (parentCanvas != null && canvasRect != null)
        {
            // For camera-based canvas, convert screen to local
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, gazeScreen, parentCanvas.worldCamera, out targetPos);
        }
        else
        {
            targetPos = gazeScreen;
        }
        
        // Smooth the movement
        Vector2 currentPos = cursorVisual.position;
        Vector2 smoothed = Vector2.SmoothDamp(currentPos, targetPos, ref velocity, smoothTime);
        cursorVisual.position = smoothed;
        
        // Hide cursor if not connected
        bool isConnected = EyeTrackingInput.Instance.IsConnected;
        if (cursorVisual.gameObject.activeSelf != isConnected)
        {
            cursorVisual.gameObject.SetActive(isConnected);
        }
    }
    
    private void CreateCursorVisual()
    {
        // Create a Canvas if needed
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            var canvasGO = new GameObject("GazeCursorCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000; // Always on top
            canvasGO.AddComponent<CanvasScaler>();
        }
        
        // Create the cursor image
        var cursorGO = new GameObject("GazeCursor");
        cursorGO.transform.SetParent(canvas.transform, false);
        
        var image = cursorGO.AddComponent<Image>();
        image.sprite = CreateCircleSprite();
        image.color = cursorColor;
        image.raycastTarget = false; // Don't block clicks
        
        cursorVisual = cursorGO.GetComponent<RectTransform>();
        cursorVisual.sizeDelta = new Vector2(cursorSize, cursorSize);
        cursorVisual.anchorMin = Vector2.zero;
        cursorVisual.anchorMax = Vector2.zero;
        cursorVisual.pivot = new Vector2(0.5f, 0.5f);
        
        parentCanvas = canvas;
        canvasRect = canvas.GetComponent<RectTransform>();
        
        Debug.Log("[GazeCursor] Auto-created cursor visual");
    }
    
    private Sprite CreateCircleSprite()
    {
        int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 2;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= radius)
                {
                    float alpha = Mathf.Clamp01((radius - distance) / 3f);
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }
        
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
    
    // Public method to change cursor style at runtime
    public void SetCursorVisual(RectTransform newVisual)
    {
        if (cursorVisual != null && cursorVisual.gameObject.name == "GazeCursor")
        {
            Destroy(cursorVisual.gameObject);
        }
        cursorVisual = newVisual;
    }
    
    public void SetCursorColor(Color color)
    {
        if (cursorVisual != null)
        {
            var image = cursorVisual.GetComponent<Image>();
            if (image != null) image.color = color;
        }
    }
    
    public void SetCursorSize(float size)
    {
        if (cursorVisual != null)
        {
            cursorVisual.sizeDelta = new Vector2(size, size);
        }
    }
}
