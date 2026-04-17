using UnityEngine;
using System.Collections.Generic;

public partial class CharacterBreather : MonoBehaviour
{
    [Header("Breathing Settings")]
    [Tooltip("How fast the character breathes.")]
    public float breathSpeed = 2f;

    [Tooltip("How much the Z-axis scales (subtle values like 0.02 to 0.05 work best).")]
    public float zScaleIntensity = 0.05f;

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

        // --- Handle Z-Scale ---
        // We only modify the Z component of the scale
        float newZ = initialScale.z + (wave * zScaleIntensity);
        transform.localScale = new Vector3(initialScale.x, initialScale.y, newZ);

        // --- Handle Follower Translation ---
        for (int i = 0; i < followers.Count; i++)
        {
            if (followers[i] != null)
            {
                // Calculate new Y position based on the same wave
                float newY = followerInitialPositions[i].y + (wave * followTranslateAmount);
                followers[i].localPosition = new Vector3(
                    followerInitialPositions[i].x,
                    newY,
                    followerInitialPositions[i].z
                );
            }
        }
    }
}