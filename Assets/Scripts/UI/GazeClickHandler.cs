using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles gaze-based interactions using eye tracking.
/// Uses blink detection for clicks and dwell time for hover.
/// </summary>
public class GazeClickHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Gaze Interaction Settings")]
    [SerializeField] private float dwellTimeRequired = 1.0f; // Time to hover before activation
    [SerializeField] private bool useBlinkForClick = true;
    [SerializeField] private bool enableHapticFeedback = false;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject gazeIndicatorPrefab;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = Color.cyan;
    [SerializeField] private Color activationColor = Color.green;
    
    // Private variables
    private EyeTrackingInput eyeTracking;
    private float dwellTimer;
    private bool isGazeHovering;
    private Renderer objectRenderer;
    private GameObject indicatorInstance;
    
    private void Start()
    {
        eyeTracking = EyeTrackingInput.Instance;
        
        // Get renderer for visual feedback
        objectRenderer = GetComponent<Renderer>();
        
        // Create gaze indicator if prefab is assigned
        if (gazeIndicatorPrefab != null)
        {
            indicatorInstance = Instantiate(gazeIndicatorPrefab, transform);
            indicatorInstance.SetActive(false);
        }
        
        // Subscribe to blink events
        // We'll check blink in Update instead
    }
    
    private void OnEnable()
    {
        dwellTimer = 0f;
        isGazeHovering = false;
    }
    
    private void OnDisable()
    {
        ResetState();
    }
    
    private void Update()
    {
        if (eyeTracking == null || !eyeTracking.IsConnected)
            return;
        
        Vector2 gazePos = eyeTracking.GazePosition;
        
        // Check if gaze is over this object using raycast
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = gazePos;
        
        System.Collections.Generic.List<RaycastResult> results = 
            new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        bool isOverObject = false;
        foreach (RaycastResult result in results)
        {
            if (result.gameObject == gameObject)
            {
                isOverObject = true;
                break;
            }
        }
        
        if (isOverObject)
        {
            HandleGazeHover();
        }
        else
        {
            ResetState();
        }
    }
    
    private void HandleGazeHover()
    {
        if (!isGazeHovering)
        {
            // Just started hovering
            isGazeHovering = true;
            dwellTimer = 0f;
            
            if (indicatorInstance != null)
            {
                indicatorInstance.SetActive(true);
            }
            
            UpdateVisualFeedback(hoverColor);
        }
        
        // Increment dwell timer
        dwellTimer += Time.deltaTime;
        
        // Check for blink click
        if (useBlinkForClick && eyeTracking.IsBlinking)
        {
            // Debounce blink
            eyeTracking.IsBlinking = false; // Consume the blink
            ActivateGazeAction();
        }
        
        // Check dwell time completion
        if (dwellTimer >= dwellTimeRequired)
        {
            ActivateGazeAction();
        }
    }
    
    private void ActivateGazeAction()
    {
        Debug.Log($"[GazeClick] Activated: {gameObject.name}");
        
        // Visual feedback
        UpdateVisualFeedback(activationColor);
        
        // Trigger button click if this is a button
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick?.Invoke();
        }
        
        // Trigger IPointerClickHandler
        ExecuteEvents.Execute<IPointerClickHandler>(
            gameObject, 
            new PointerEventData(EventSystem.current), 
            ExecuteEvents.pointerClickHandler
        );
        
        // Haptic feedback if enabled
        if (enableHapticFeedback)
        {
            // Trigger haptic feedback if supported
        }
        
        // Reset timer
        dwellTimer = 0f;
        
        // Return to hover color after brief moment
        Invoke(nameof(ReturnToHoverColor), 0.2f);
    }
    
    private void ReturnToHoverColor()
    {
        if (isGazeHovering && gameObject.activeInHierarchy)
        {
            UpdateVisualFeedback(hoverColor);
        }
    }
    
    private void ResetState()
    {
        isGazeHovering = false;
        dwellTimer = 0f;
        
        if (indicatorInstance != null)
        {
            indicatorInstance.SetActive(false);
        }
        
        UpdateVisualFeedback(normalColor);
    }
    
    private void UpdateVisualFeedback(Color color)
    {
        if (objectRenderer != null)
        {
            objectRenderer.material.color = color;
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        // This handles standard mouse input as fallback
        isGazeHovering = true;
        dwellTimer = 0f;
        UpdateVisualFeedback(hoverColor);
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        // This handles standard mouse input as fallback
        ResetState();
    }
    
    private void OnDestroy()
    {
        CancelInvoke();
    }
}
