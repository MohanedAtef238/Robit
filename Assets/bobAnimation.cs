using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BouncyLifeCycle : MonoBehaviour
{
    public enum BobDirection { X, Y, Z }

    [Header("Bouncy Birth (On Start)")]
    public float birthDuration = 0.6f;     // "Quickly"
    public float startZScale = 0.01f;      // Starts almost flat on Z
    [Range(1f, 5f)] public float bounciness = 1.7f; // Higher = more "wiggle" at the end

    [Header("Model Scaling (The Bob)")]
    public BobDirection scaleAxis = BobDirection.Z;
    public float scaleIncrease = 5f;
    public float stretchSqueeze = 2f;
    public float speed = 10f;
    public bool pinBackSide = true;

    [Header("The Eye List")]
    public List<Transform> eyesToMove;

    private Vector3 originalScale;
    private Vector3 originalPos;
    private bool isAnimating = false;

    void Start()
    {
        originalScale = transform.localScale;
        originalPos = transform.position;

        // Start the bouncy entrance
        StartCoroutine(BouncyBirth());
    }

    void Update()
    {
        // Trigger the bob manually
        if (Input.anyKeyDown && !isAnimating)
        {
            StartCoroutine(BobRoutine());
        }
    }

    IEnumerator BouncyBirth()
    {
        isAnimating = true;
        float elapsed = 0;

        // Set the initial "Flat Pancake" scale on Z
        Vector3 initialSquash = originalScale;
        initialSquash.z = startZScale;
        transform.localScale = initialSquash;

        while (elapsed < birthDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / birthDuration;

            // --- ELASTIC OUT EASING FORMULA ---
            // This creates the "Overshoot and Settle" bounce effect
            float c4 = (2f * Mathf.PI) / 3f;
            float bounceT = t == 0 ? 0 : t == 1 ? 1
                : Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;

            // Apply the bounce specifically to the Z axis
            float finalZ = Mathf.LerpUnclamped(startZScale, originalScale.z, bounceT);

            // Apply it to the transform
            transform.localScale = new Vector3(originalScale.x, originalScale.y, finalZ);

            yield return null;
        }

        transform.localScale = originalScale;
        isAnimating = false;
    }

    // --- BobRoutine remains the same as your previous working displacement code ---
    IEnumerator BobRoutine()
    {
        isAnimating = true;
        Vector3 startModelPos = transform.position;
        Vector3[] startEyePositions = new Vector3[eyesToMove.Count];
        for (int i = 0; i < eyesToMove.Count; i++)
        {
            if (eyesToMove[i] != null) startEyePositions[i] = eyesToMove[i].position;
        }

        float t = 0;
        while (t < Mathf.PI)
        {
            t += Time.deltaTime * speed;
            float sineWave = Mathf.Sin(t);
            float addedForward = sineWave * scaleIncrease;
            float addedSqueeze = sineWave * stretchSqueeze;

            Vector3 newScale = originalScale;
            Vector3 localDir = Vector3.forward;

            switch (scaleAxis)
            {
                case BobDirection.X: newScale.x += addedForward; newScale.y -= addedSqueeze; newScale.z -= addedSqueeze; localDir = Vector3.right; break;
                case BobDirection.Y: newScale.y += addedForward; newScale.x -= addedSqueeze; newScale.z -= addedSqueeze; localDir = Vector3.up; break;
                case BobDirection.Z: newScale.z += addedForward; newScale.x -= addedSqueeze; newScale.y -= addedSqueeze; localDir = Vector3.forward; break;
            }
            transform.localScale = newScale;

            if (pinBackSide)
            {
                Vector3 worldMoveDir = transform.TransformDirection(localDir);
                transform.position = startModelPos + (worldMoveDir * (addedForward / 2f));
            }

            Vector3 displacement = transform.position - startModelPos;
            for (int i = 0; i < eyesToMove.Count; i++)
            {
                if (eyesToMove[i] != null) eyesToMove[i].position = startEyePositions[i] + displacement;
            }
            yield return null;
        }

        transform.localScale = originalScale;
        transform.position = originalPos;
        for (int i = 0; i < eyesToMove.Count; i++)
        {
            if (eyesToMove[i] != null) eyesToMove[i].position = startEyePositions[i];
        }
        isAnimating = false;
    }
}