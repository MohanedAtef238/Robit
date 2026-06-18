using UnityEngine;
using System.Collections.Generic;

public class CharacterBreather : MonoBehaviour
{
    [Header("Breathing Settings")]
    [Tooltip("How fast the character breathes.")]
    public float breathSpeed = 2f;

    [Tooltip("How much the character expands horizontally (X and Z axes).")]
    public float horizontalScaleIntensity = 0.03f;

    [Tooltip("How much the character expands vertically (Y axis).")]
    public float verticalScaleIntensity = 0.02f;

    [Header("Follower Settings")]
    [Tooltip("Objects that move upward as the character inhales.")]
    public List<Transform> followers = new List<Transform>();

    [Tooltip("How much the followers move up on the Y-axis.")]
    public float followTranslateAmount = 0.02f;

    private Vector3 initialScale;
    private List<Vector3> followerInitialPositions = new List<Vector3>();

    void Start()
    {
        // Store original scale
        initialScale = transform.localScale;

        // Store original positions of followers
        foreach (Transform t in followers)
        {
            if (t != null)
                followerInitialPositions.Add(t.localPosition);
        }
    }

    void Update()
    {
        // Create a sine wave that fluctuates between -1 and 1
        float wave = Mathf.Sin(Time.time * breathSpeed);

        // --- Handle Squash and Stretch Scale ---
        // Expand outward on X and Z, and subtly expand upward on Y for a full lung expansion
        float newX = initialScale.x + (wave * horizontalScaleIntensity);
        float newY = initialScale.y + (wave * verticalScaleIntensity);
        float newZ = initialScale.z + (wave * horizontalScaleIntensity);

        transform.localScale = new Vector3(newX, newY, newZ);

        // --- Handle Follower Translation ---
        for (int i = 0; i < followers.Count; i++)
        {
            if (followers[i] != null)
            {
                // Calculate new Y position based on the same wave
                float followerNewY = followerInitialPositions[i].y + (wave * followTranslateAmount);
                followers[i].localPosition = new Vector3(
                    followerInitialPositions[i].x,
                    followerNewY,
                    followerInitialPositions[i].z
                );
            }
        }
    }
}