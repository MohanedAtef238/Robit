using UnityEngine;

public class UIHover : MonoBehaviour
{
    [Header("Hover Settings")]
    [Tooltip("How far the image will move along its diagonal path from its starting position.")]
    [SerializeField] private float amplitude = 20f;

    [Tooltip("How fast the image moves. Lower numbers mean slower movement.")]
    [SerializeField] private float speed = 1f;

    [Tooltip("The direction of diagonal movement. (1, 1) is up-right, (1, -1) is down-right, etc.")]
    [SerializeField] private Vector2 movementDirection = new Vector2(1f, 1f);

    private RectTransform rectTransform;
    private Vector2 startPosition;

    private void Awake()
    {
        // Cache the RectTransform component used by UI elements
        rectTransform = GetComponent<RectTransform>();

        if (rectTransform != null)
        {
            startPosition = rectTransform.anchoredPosition;

            // Normalize the direction vector to ensure consistency in movement amplitude 
            // regardless of the inspector values typed (e.g., 5, 5 won't move faster than 1, 1)
            if (movementDirection != Vector2.zero)
            {
                movementDirection.Normalize();
            }
        }
        else
        {
            Debug.LogError($"UIHover on {gameObject.name} requires a RectTransform! Ensure this is on a UI element.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        // Time.time * speed advances the animation smoothly over time
        // Mathf.Sin naturally handles the "ease in and out" at the peaks and valleys
        float waveOffset = Mathf.Sin(Time.time * speed) * amplitude;

        // Multiply the wave scale by our direction vector and add it to the starting position
        Vector2 offset = movementDirection * waveOffset;
        rectTransform.anchoredPosition = startPosition + offset;
    }
}