using UnityEngine;
using System;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Collections;
using Unity.Profiling;

public class Transparency : MonoBehaviour
{
    #if !UNITY_EDITOR
    private IntPtr hWnd;
    #endif

    // Debug fields — used by the editor OnGUI overlay
    private bool isClickThrough = true;
#pragma warning disable 0414
    private bool isTransparencyEnabled = true;
#pragma warning restore 0414
    #if UNITY_EDITOR
    private string debugHitInfo = "none";
    private Vector2 debugCursorPos;
    private bool debugOverUI = false;
    #endif
    
    private const float TOGGLE_COOLDOWN = 0.1f;
    #if !UNITY_EDITOR
    private float lastToggleTime = 0f;
    #endif

    // Counted pause: if multiple systems pause us (e.g. AppLauncher then HomePage),
    // we only resume transparency when all of them have released their hold.
    private int _pauseDepth = 0;
    
    private Camera mainCamera;

    // --- Performance: cached fields to avoid per-frame allocations ---
    private UIDocument[] _cachedUIDocuments = Array.Empty<UIDocument>();
    private PointerEventData _pointerEventData;
    private EventSystem _lastKnownEventSystem;
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(8);
#if !UNITY_EDITOR
    private int _lastRawX = -1;
    private int _lastRawY = -1;
#endif
    private GUIStyle _debugStyle;
    private static readonly ProfilerMarker _updateMarker = new ProfilerMarker("Transparency.Update");
    private static readonly ProfilerMarker _pickMarker = new ProfilerMarker("Transparency.IsPointerOverUI");
    // -----------------------------------------------------------------

    [Tooltip("Check this for the Overlay scene. Uncheck it for the Home/Menu scene.")]
    public bool startInTransparentMode = true;

    void OnEnable()
    {
        RefreshUIDocumentCache();
    }

    public void RefreshUIDocumentCache()
    {
        _cachedUIDocuments = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
    }

    void Start()
    {
        Application.runInBackground = true;

        mainCamera = Camera.main;
        if (mainCamera == null) mainCamera = FindFirstObjectByType<Camera>();

        #if !UNITY_EDITOR
        hWnd = WindowManager.GetWindowHandle();
        #endif

        if (startInTransparentMode)
        {
            #if !UNITY_EDITOR
            WindowManager.MakeTransparent(); 
            #endif
            
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
        
        RobitLogger.Log($"[Transparency] Initialized. Mode: {(startInTransparentMode ? "Transparent" : "Opaque")}");
    }

    public void SwitchToHomeMode()
    {
        StartCoroutine(SwitchToHomeRoutine());
    }

    private IEnumerator ForceOpaqueRoutine()
    {
        #if !UNITY_EDITOR
        WindowManager.MakeOpaque();
        #endif
        
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

        #if !UNITY_EDITOR
        WindowManager.MakeOpaque();
        #endif
        
        this.enabled = false;
    }
    
    public void DisableTransparency()
    {
        isTransparencyEnabled = false;
        
        SetClickThrough(false);
        if (mainCamera != null)
        {
            mainCamera.backgroundColor = new Color(mainCamera.backgroundColor.r, mainCamera.backgroundColor.g, mainCamera.backgroundColor.b, 1f);
        }
        this.enabled = false; 
    }

    public void PausePolling()
    {
        _pauseDepth++;
        this.enabled = false;
        SetClickThrough(false);
    }

    public void ResumePolling()
    {
        // Decrement the pause counter. Only fully resume when all callers have released.
        if (_pauseDepth > 0) _pauseDepth--;
        if (_pauseDepth > 0) return;

        // Don't restore transparent mode if acrylic/glass is still active —
        // that means another UI (e.g. Home Page) is still using the window and
        // MakeTransparent would strip its DWM backdrop.
        if (WindowManager.IsAcrylicActive) return;

        this.enabled = true;
        if (mainCamera != null)
        {
            mainCamera.backgroundColor = new Color(mainCamera.backgroundColor.r, mainCamera.backgroundColor.g, mainCamera.backgroundColor.b, 0f);
        }
        #if !UNITY_EDITOR
        WindowManager.MakeTransparent();
        #endif
        SetClickThrough(true);
    }

    
    public void EnableTransparency()
    {
        isTransparencyEnabled = true;
        this.enabled = true;
        
        if (mainCamera != null)
        {
            mainCamera.backgroundColor = new Color(mainCamera.backgroundColor.r, mainCamera.backgroundColor.g, mainCamera.backgroundColor.b, 0f);
        }

        SetClickThrough(true);
        #if !UNITY_EDITOR
        WindowManager.MakeTransparent();
        #endif
        RobitLogger.Log("[Transparency] Transparency mode enabled");
    }

    // Polls cursor position and toggles click-through based on UI/3D hits
    void Update()
    {
        // Guard against execution during shutdown/domain reload
        if (Application.isEditor && !Application.isPlaying) return;

        using var _um = _updateMarker.Auto();
        #if !UNITY_EDITOR
        if (!isTransparencyEnabled)
        {
            #if UNITY_EDITOR
            debugHitInfo = "DISABLED";
            #endif
            return;
        }
        
        Win32Interop.POINT cursorPos;
        if (!Win32Interop.GetCursorPos(out cursorPos))
            return;

        // Short-circuit before any further P/Invoke or UI work if the cursor hasn't moved
        if (cursorPos.X == _lastRawX && cursorPos.Y == _lastRawY) return;
        _lastRawX = cursorPos.X;
        _lastRawY = cursorPos.Y;

        Win32Interop.POINT clientPos = cursorPos;
        Win32Interop.ScreenToClient(hWnd, ref clientPos);
        
        Vector2 unityScreenPos = new Vector2(clientPos.X, Screen.height - clientPos.Y);
        bool overUI = IsPointerOverUI(unityScreenPos, out string hitInfo);
        
        #if UNITY_EDITOR
        debugCursorPos = unityScreenPos;
        debugHitInfo = hitInfo;
        debugOverUI = overUI;
        #endif
        
        if (Time.time - lastToggleTime < TOGGLE_COOLDOWN)
            return;
        
        if (overUI && isClickThrough)
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            RobitLogger.Log($"[Transparency] Click-through OFF. Hit: {hitInfo}");
            #endif
            SetClickThrough(false);
            // FocusWindow is safe to call here because SetClickThrough now uses SWP_NOACTIVATE,
            // so the style change itself no longer triggers a window-activation event.
            // FocusWindow brings Unity to the foreground so the subsequent click is delivered
            // directly as WM_LBUTTONDOWN rather than going through WM_MOUSEACTIVATE first.
            WindowManager.FocusWindow();
            lastToggleTime = Time.time;
        }
        else if (!overUI && !isClickThrough)
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            RobitLogger.Log($"[Transparency] Click-through ON");
            #endif
            SetClickThrough(true);
            lastToggleTime = Time.time;
        }
        #endif
    }
    
    void OnGUI()
    {
        #if UNITY_EDITOR
        // Cache the GUIStyle so it isn't rebuilt every OnGUI call
        if (_debugStyle == null)
        {
            _debugStyle = new GUIStyle(GUI.skin.box);
            _debugStyle.fontSize = 14;
            _debugStyle.normal.textColor = Color.white;
            _debugStyle.alignment = TextAnchor.UpperLeft;
            _debugStyle.padding = new RectOffset(10, 10, 10, 10);
            _debugStyle.richText = true;
        }

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
        GUI.Label(new Rect(10, 10, 280, 140), debugText, _debugStyle);
        #endif
    }
    
    // Raycasts UI elements first, then UI Toolkit panels, then 3D colliders
    private bool IsPointerOverUI(Vector2 screenPosition, out string hitInfo)
    {
        using var _pm = _pickMarker.Auto();
        hitInfo = "none";

        if (EventSystem.current != null)
        {
            // Re-create PointerEventData only if the EventSystem instance has changed
            if (_pointerEventData == null || _lastKnownEventSystem != EventSystem.current)
            {
                _lastKnownEventSystem = EventSystem.current;
                _pointerEventData = new PointerEventData(EventSystem.current);
            }
            _pointerEventData.position = screenPosition;

            _raycastResults.Clear();
            EventSystem.current.RaycastAll(_pointerEventData, _raycastResults);

            if (_raycastResults.Count > 0)
            {
                hitInfo = $"UI:{_raycastResults[0].gameObject.name}";
                return true;
            }
        }
        else
        {
            hitInfo = "NO_EVENTSYSTEM";
        }

        // Check UI Toolkit panels (not detected by EventSystem.RaycastAll)
        // Uses _cachedUIDocuments — refreshed in OnEnable and via RefreshUIDocumentCache()
        foreach (var doc in _cachedUIDocuments)
        {
            if (doc == null || doc.rootVisualElement == null) continue;
            // Skip panels whose root is hidden — they should not capture pointer hits
            if (doc.rootVisualElement.resolvedStyle.display == DisplayStyle.None) continue;
            var panel = doc.rootVisualElement.panel;
            if (panel == null) continue;

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);
            VisualElement picked = panel.Pick(panelPos);
            if (picked != null)
            {
                hitInfo = $"UIToolkit:{picked.name}";
                return true;
            }
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


