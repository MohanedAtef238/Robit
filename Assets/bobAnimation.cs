using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BouncyLifeCycle : MonoBehaviour
{
    public enum BobDirection { X, Y, Z }

    [Header("Bouncy Birth (On Start)")]
    public float birthDuration = 0.6f;
    public float startZScale = 0.01f;
    [Range(1f, 5f)] public float bounciness = 1.7f;

    [Header("Model Scaling (The Bob)")]
    public BobDirection scaleAxis = BobDirection.Z;
    public float scaleIncrease = 5f;
    public float stretchSqueeze = 2f;
    public float speed = 10f;
    public bool pinBackSide = true;

    [Header("The Eye List (Displacement Only)")]
    public List<Transform> eyesToMove;

    [Header("Scaling Objects (Growth + Displacement)")]
    public List<Transform> objectsToScale;
    public float scaleObjectsIncrease = 2f; // How much these objects grow during the bob

    private Vector3 originalScale;
    private Vector3 originalPos;
    private List<Vector3> originalObjectScales = new List<Vector3>();
    private bool isAnimating = false;

    void Start()
    {
        originalScale = transform.localScale;
        originalPos = transform.position;

        // Store the starting scales of the extra objects
        foreach (Transform obj in objectsToScale)
        {
            if (obj != null) originalObjectScales.Add(obj.localScale);
        }

        StartCoroutine(BouncyBirth());
    }

    void Update()
    {
        if (Input.anyKeyDown && !isAnimating)
        {
            StartCoroutine(BobRoutine());
        }
    }

    IEnumerator BouncyBirth()
    {
        isAnimating = true;
        float elapsed = 0;

        Vector3 initialSquash = originalScale;
        initialSquash.z = startZScale;
        transform.localScale = initialSquash;

        while (elapsed < birthDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / birthDuration;

            float c4 = (2f * Mathf.PI) / 3f;
            float bounceT = t == 0 ? 0 : t == 1 ? 1
                : Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;

            float finalZ = Mathf.LerpUnclamped(startZScale, originalScale.z, bounceT);
            transform.localScale = new Vector3(originalScale.x, originalScale.y, finalZ);

            yield return null;
        }

        transform.localScale = originalScale;
        isAnimating = false;
    }

    IEnumerator BobRoutine()
    {
        isAnimating = true;
        Vector3 startModelPos = transform.position;

        // Snapshot eye positions
        Vector3[] startEyePositions = new Vector3[eyesToMove.Count];
        for (int i = 0; i < eyesToMove.Count; i++)
        {
            if (eyesToMove[i] != null) startEyePositions[i] = eyesToMove[i].position;
        }

        // Snapshot scaling objects positions
        Vector3[] startObjPositions = new Vector3[objectsToScale.Count];
        for (int i = 0; i < objectsToScale.Count; i++)
        {
            if (objectsToScale[i] != null) startObjPositions[i] = objectsToScale[i].position;
        }

        float t = 0;
        while (t < Mathf.PI)
        {
            t += Time.deltaTime * speed;
            float sineWave = Mathf.Sin(t);
            float addedForward = sineWave * scaleIncrease;
            float addedSqueeze = sineWave * stretchSqueeze;
            float addedObjScale = sineWave * scaleObjectsIncrease;

            // 1. Scale the Model
            Vector3 newScale = originalScale;
            Vector3 localDir = Vector3.forward;

            switch (scaleAxis)
            {
                case BobDirection.X: newScale.x += addedForward; newScale.y -= addedSqueeze; newScale.z -= addedSqueeze; localDir = Vector3.right; break;
                case BobDirection.Y: newScale.y += addedForward; newScale.x -= addedSqueeze; newScale.z -= addedSqueeze; localDir = Vector3.up; break;
                case BobDirection.Z: newScale.z += addedForward; newScale.x -= addedSqueeze; newScale.y -= addedSqueeze; localDir = Vector3.forward; break;
            }
            transform.localScale = newScale;

            // 2. Position Displacement (Lunge)
            if (pinBackSide)
            {
                Vector3 worldMoveDir = transform.TransformDirection(localDir);
                transform.position = startModelPos + (worldMoveDir * (addedForward / 2f));
            }

            Vector3 displacement = transform.position - startModelPos;

            // 3. Move Eyes
            for (int i = 0; i < eyesToMove.Count; i++)
            {
                if (eyesToMove[i] != null) eyesToMove[i].position = startEyePositions[i] + displacement;
            }

            // 4. Move AND Scale the new objects
            for (int i = 0; i < objectsToScale.Count; i++)
            {
                if (objectsToScale[i] != null)
                {
                    // Displacement
                    objectsToScale[i].position = startObjPositions[i] + displacement;

                    // Uniform Scale Up
                    Vector3 objBaseScale = originalObjectScales[i];
                    objectsToScale[i].localScale = objBaseScale + (Vector3.one * addedObjScale);
                }
            }

            yield return null;
        }

        // Final Reset
        transform.localScale = originalScale;
        transform.position = originalPos;
        for (int i = 0; i < eyesToMove.Count; i++)
        {
            if (eyesToMove[i] != null) eyesToMove[i].position = startEyePositions[i];
        }
        for (int i = 0; i < objectsToScale.Count; i++)
        {
            if (objectsToScale[i] != null)
            {
                objectsToScale[i].position = startObjPositions[i];
                objectsToScale[i].localScale = originalObjectScales[i];
            }
        }

        isAnimating = false;
    }
}