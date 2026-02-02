using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class Transparency : MonoBehaviour
{
    private IntPtr hWnd;
    private bool isClickThrough = true;
    
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
        #if !UNITY_EDITOR
        hWnd = WindowManager.GetWindowHandle();
        WindowManager.MakeTransparent();
        Debug.Log("[Transparency] Window initialized with click-through enabled");
        #endif
    }

    void Update()
    {
        #if !UNITY_EDITOR
        POINT cursorPos;
        if (!GetCursorPos(out cursorPos))
            return;
        
        POINT clientPos = cursorPos;
        ScreenToClient(hWnd, ref clientPos);
        
        Vector2 unityScreenPos = new Vector2(clientPos.X, Screen.height - clientPos.Y);
        
        bool overUI = IsPointerOverUI(unityScreenPos);
        
        if (Time.time - lastToggleTime < TOGGLE_COOLDOWN)
            return;
        
        if (overUI && isClickThrough)
        {
            SetClickThrough(false);
            lastToggleTime = Time.time;
            Debug.Log($"[Transparency] Cursor over UI at {unityScreenPos}, disabling click-through");
        }
        else if (!overUI && !isClickThrough)
        {
            SetClickThrough(true);
            lastToggleTime = Time.time;
            Debug.Log($"[Transparency] Cursor left UI, enabling click-through");
        }
        #endif
    }
    
    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        // Create a pointer event data for the raycast
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPosition;
        
        // Raycast against all UI elements
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        // Return true if we hit any UI element
        return results.Count > 0;
    }

    public void SetClickThrough(bool clickThrough)
    {
        #if !UNITY_EDITOR
        WindowManager.SetClickThrough(clickThrough);
        isClickThrough = clickThrough;
        #endif
    }
}
