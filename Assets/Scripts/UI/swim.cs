using UnityEngine;

public class ObjectHover : MonoBehaviour
{
    [Header("Hover Settings")]
    [Tooltip("How far up and down the object will move from its starting position.")]
    [SerializeField] private float amplitude = 0.5f;

    [Tooltip("How fast the object moves. Lower numbers mean slower movement.")]
    [SerializeField] private float speed = 1.5f;

    [Header("Optional Behavior")]
    [Tooltip("Should the object slowly rotate while hovering?")]
    [SerializeField] private bool rotate = false;
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 45f, 0f);

    private Vector3 startPosition;

    private void Start()
    {
        // Cache the starting position in local space so it can be moved/parented safely
        startPosition = transform.localPosition;
    }

    private void Update()
    {
        // Smooth sine wave calculation for natural ease-in and ease-out
        float newY = startPosition.y + Mathf.Sin(Time.time * speed) * amplitude;

        // Apply the new position while keeping X and Z relative to its start
        transform.localPosition = new Vector3(startPosition.x, newY, startPosition.z);

        // Optional: Adds a nice floating/spinning visual polish if enabled
        if (rotate)
        {
            transform.Rotate(rotationSpeed * Time.deltaTime);
        }
    }
}