using UnityEngine;
using UnityEngine.InputSystem;

public class MouseRotateSphere : MonoBehaviour
{
    public float maxRotation = 60f;   // Maximum rotation angle
    public float smoothSpeed = 5f;    // How smooth the movement feels

    private float currentYRotation = 0f;

    void Update()
    {
        if (Mouse.current == null) return;

        // Get mouse position
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // Normalize Y position (0 to 1)
        float normalizedY = mousePos.y / Screen.height;

        // Convert to range (-1 to 1)
        float centeredY = (normalizedY - 0.5f) * 2f;

        // Target rotation based on mouse height
        float targetYRotation = centeredY * maxRotation;

        // Smooth transition
        currentYRotation = Mathf.Lerp(currentYRotation, targetYRotation, Time.deltaTime * smoothSpeed);

        // Apply rotation (only Y axis)
        transform.rotation = Quaternion.Euler(0f, currentYRotation, 0f);
    }
}