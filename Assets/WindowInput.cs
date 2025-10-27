using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using uWindowCapture;

public class WindowInput : MonoBehaviour
{
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    private UwcWindowTexture uwcTexture;
    private Camera mainCamera;

    void Start()
    {
        uwcTexture = GetComponent<UwcWindowTexture>();
        mainCamera = Camera.main;

        if (uwcTexture == null)
            Debug.LogError("WindowInput: UwcWindowTexture component not found!");
        if (mainCamera == null)
            Debug.LogError("WindowInput: Main Camera not found! (Is it tagged 'MainCamera'?)");

        Debug.Log("WindowInput: Script Started.");
    }

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                Debug.LogWarning("WindowInput: Click detected, but it was BLOCKED by the UI EventSystem.");
                return;
            }

            Debug.Log("WindowInput: Click detected. Firing ray...");
            HandleClick();
        }
    }

    void HandleClick()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            Debug.Log("WindowInput: Raycast hit '" + hit.collider.name + "'.");

            if (hit.collider.gameObject == gameObject)
            {
                Debug.Log("WindowInput: Raycast hit THIS object. Proceeding...");

                Vector2 uv = hit.textureCoord;
                var window = uwcTexture.window;

                if (window == null || !window.isValid)
                {
                    Debug.LogWarning("WindowInput: Window is not valid. Click ignored.");
                    return;
                }

                // Translation Y-FLIP
                int windowX = window.x;
                int windowY = window.y;
                int clickX = windowX + (int)(uv.x * window.width);
                int clickY = windowY + (int)(uv.y * window.height);


                Debug.Log("WindowInput: SUCCESS! Sending click to Windows at (" + clickX + ", " + clickY + ")");

                //Send to Windows
                SetCursorPos(clickX, clickY);
                mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)clickX, (uint)clickY, 0, 0);
                mouse_event(MOUSEEVENTF_LEFTUP, (uint)clickX, (uint)clickY, 0, 0);
            }
            else
            {
                Debug.LogWarning("WindowInput: Raycast hit '" + hit.collider.name + "', not this object. Click ignored.");
            }
        }
        else
        {
            Debug.LogWarning("WindowInput: Click detected, but raycast hit nothing.");
        }
    }
}