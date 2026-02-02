using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class Transparency : MonoBehaviour
{
    private IntPtr hWnd;
    private bool isClickThrough = true;
    private bool isTransparencyEnabled = true; // Can be disabled when returning home
    private Camera mainCamera; // Cached camera reference
    
    // Debug display state
    private string debugHitInfo = "none";
    private Vector2 debugCursorPos;
    private bool debugOverUI = false;
    
    // Debounce: 6 frames at 60Hz = 100ms
    private const float TOGGLE_COOLDOWN = 0.1f;
    private float lastToggleTime = 0f;
    
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
    
    void Start()
    {
        // CRITICAL: Keep running even when window loses focus
        Application.runInBackground = true;
        
        #if !UNITY_EDITOR
        hWnd = WindowManager.GetWindowHandle();
        WindowManager.MakeTransparent();
        
        // Cache camera - try Camera.main first, then fallback to FindObjectOfType
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
            Debug.LogWarning("[Transparency] Camera.main is null! Using FindObjectOfType fallback. Consider tagging your camera as 'MainCamera'.");
        }
        
        Debug.Log($"[Transparency] Window initialized. Camera found: {mainCamera != null}, runInBackground: {Application.runInBackground}");
        #endif
    }
    
    /// <summary>
    /// Call this before switching to home/main scene to fully disable transparency mode.
    /// </summary>
    public void DisableTransparency()
    {
        isTransparencyEnabled = false;
        isClickThrough = false;
        Debug.Log("[Transparency] Transparency mode disabled");
    }
    
    /// <summary>
    /// Re-enable transparency mode (called when entering overlay scene).
    /// </summary>
    public void EnableTransparency()
    {
        isTransparencyEnabled = true;
        #if !UNITY_EDITOR
        WindowManager.MakeTransparent();
        #endif
        Debug.Log("[Transparency] Transparency mode enabled");
    }

    void Update()
    {
        #if !UNITY_EDITOR
        // Skip all processing if transparency is disabled (e.g., returning to home)
        if (!isTransparencyEnabled)
        {
            debugHitInfo = "DISABLED";
            return;
        }
        
        POINT cursorPos;
        if (!GetCursorPos(out cursorPos))
            return;
        
        POINT clientPos = cursorPos;
        ScreenToClient(hWnd, ref clientPos);
        
        Vector2 unityScreenPos = new Vector2(clientPos.X, Screen.height - clientPos.Y);
        
        bool overUI = IsPointerOverUI(unityScreenPos, out string hitInfo);
        
        // Store for debug display
        debugCursorPos = unityScreenPos;
        debugHitInfo = hitInfo;
        debugOverUI = overUI;
        
        if (Time.time - lastToggleTime < TOGGLE_COOLDOWN)
            return;
        
        if (overUI && isClickThrough)
        {
            Debug.Log($"[Transparency] SWITCHING: Click-through OFF. Hit: {hitInfo}");
            SetClickThrough(false);
            WindowManager.FocusWindow(); // Regain focus when entering UI area
            lastToggleTime = Time.time;
        }
        else if (!overUI && !isClickThrough)
        {
            Debug.Log($"[Transparency] SWITCHING: Click-through ON (no hit)");
            SetClickThrough(true);
            lastToggleTime = Time.time;
        }
        #endif
    }
    
    // On-screen debug display
    void OnGUI()
    {
        #if !UNITY_EDITOR
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.UpperLeft;
        style.padding = new RectOffset(10, 10, 10, 10);
        
        string status = isClickThrough ? "<color=red>CLICK-THROUGH</color>" : "<color=green>INTERACTIVE</color>";
        string hitColor = debugOverUI ? "lime" : "yellow";
        
        string debugText = $"=== TRANSPARENCY DEBUG ===\n" +
                          $"Status: {status}\n" +
                          $"Cursor: ({debugCursorPos.x:F0}, {debugCursorPos.y:F0})\n" +
                          $"Hit: <color={hitColor}>{debugHitInfo}</color>\n" +
                          $"Camera: {(mainCamera != null ? "OK" : "NULL")}\n" +
                          $"EventSystem: {(EventSystem.current != null ? "OK" : "NULL")}";
        
        // Draw background box
        GUI.backgroundColor = new Color(0, 0, 0, 0.8f);
        GUI.Box(new Rect(10, 10, 280, 140), "");
        
        // Draw text with rich text
        style.richText = true;
        GUI.Label(new Rect(10, 10, 280, 140), debugText, style);
        #endif
    }
    
    /// <summary>
    /// Checks if the pointer is over any UI element or 3D object with a collider.
    /// </summary>
    private bool IsPointerOverUI(Vector2 screenPosition, out string hitInfo)
    {
        hitInfo = "none";
        
        // 1. Check UI Elements
        if (EventSystem.current != null)
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = screenPosition;
            
            List<RaycastResult> uiResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, uiResults);
            
            if (uiResults.Count > 0)
            {
                hitInfo = $"UI:{uiResults[0].gameObject.name}";
                return true;
            }
        }
        else
        {
            hitInfo = "NO_EVENTSYSTEM";
        }

        // 2. Check 3D Objects (like the robit model that triggers UI)
        if (mainCamera != null)
        {
            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                hitInfo = $"3D:{hit.collider.gameObject.name}";
                return true;
            }
        }
        else
        {
            hitInfo = "NO_CAMERA";
        }
        
        return false;
    }

    public void SetClickThrough(bool clickThrough)
    {
        #if !UNITY_EDITOR
        WindowManager.SetClickThrough(clickThrough);
        isClickThrough = clickThrough;
        #endif
    }
}

