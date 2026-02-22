using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections;

public class Transparency : MonoBehaviour
{
    #if !UNITY_EDITOR
    private IntPtr hWnd;
    private bool isClickThrough = true;
    private bool isTransparencyEnabled = true;
    private string debugHitInfo = "none";
    private Vector2 debugCursorPos;
    private bool debugOverUI = false;
    
    private const float TOGGLE_COOLDOWN = 0.1f;
    private float lastToggleTime = 0f;
    #endif
    
    private Camera mainCamera;
    
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
    
    [Tooltip("Check this for the Overlay scene. Uncheck it for the Home/Menu scene.")]
    public bool startInTransparentMode = true; 

    void Start()
    {
        Application.runInBackground = true;

        #if !UNITY_EDITOR
        hWnd = WindowManager.GetWindowHandle();
        mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = FindObjectOfType<Camera>();

        if (startInTransparentMode)
        {
            WindowManager.MakeTransparent(); 
            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = new Color(0, 0, 0, 0);
            }
        }
        else
        {
            StartCoroutine(ForceOpaqueRoutine());
        }
        
        Debug.Log($"[Transparency] Initialized. Mode: {(startInTransparentMode ? "Transparent" : "Opaque")}");
        #endif
    }

    public void SwitchToHomeMode()
    {
        StartCoroutine(SwitchToHomeRoutine());
    }

    private IEnumerator ForceOpaqueRoutine()
    {
        WindowManager.MakeOpaque();
        if (mainCamera != null)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(mainCamera.backgroundColor.r, mainCamera.backgroundColor.g, mainCamera.backgroundColor.b, 1f);
        }
        yield break;
    }

    // Waits for an opaque frame to render before stripping window styles
    private IEnumerator SwitchToHomeRoutine()
    {
        if (mainCamera != null)
        {
            mainCamera.backgroundColor = new Color(mainCamera.backgroundColor.r, mainCamera.backgroundColor.g, mainCamera.backgroundColor.b, 1f);
        }

        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame(); 

        WindowManager.MakeOpaque();
        this.enabled = false;
    }
    
    public void DisableTransparency()
    {
        #if !UNITY_EDITOR
        isTransparencyEnabled = false;
        #endif
        SetClickThrough(false);
        if (mainCamera != null)
        {
            mainCamera.backgroundColor = new Color(mainCamera.backgroundColor.r, mainCamera.backgroundColor.g, mainCamera.backgroundColor.b, 1f);
        }
        enabled = false; 
    }
    
    public void EnableTransparency()
    {
        #if !UNITY_EDITOR
        isTransparencyEnabled = true;
        #endif
        SetClickThrough(true);
        #if !UNITY_EDITOR
        WindowManager.MakeTransparent();
        #endif
        Debug.Log("[Transparency] Transparency mode enabled");
    }

    // Polls cursor position and toggles click-through based on UI/3D hits
    void Update()
    {
        #if !UNITY_EDITOR
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
        
        debugCursorPos = unityScreenPos;
        debugHitInfo = hitInfo;
        debugOverUI = overUI;
        
        if (Time.time - lastToggleTime < TOGGLE_COOLDOWN)
            return;
        
        if (overUI && isClickThrough)
        {
            Debug.Log($"[Transparency] Click-through OFF. Hit: {hitInfo}");
            SetClickThrough(false);
            WindowManager.FocusWindow();
            lastToggleTime = Time.time;
        }
        else if (!overUI && !isClickThrough)
        {
            Debug.Log($"[Transparency] Click-through ON");
            SetClickThrough(true);
            lastToggleTime = Time.time;
        }
        #endif
    }
    
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
        
        GUI.backgroundColor = new Color(0, 0, 0, 0.8f);
        GUI.Box(new Rect(10, 10, 280, 140), "");
        
        style.richText = true;
        GUI.Label(new Rect(10, 10, 280, 140), debugText, style);
        #endif
    }
    
    // Raycasts UI elements first, then 3D colliders
    private bool IsPointerOverUI(Vector2 screenPosition, out string hitInfo)
    {
        hitInfo = "none";
        
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

