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
    public float scaleObjectsIncrease = 2f;

    private Vector3 originalScale;
    private Vector3 originalPos;
    private List<Vector3> originalObjectScales = new List<Vector3>();
    private bool isAnimating = false;

    void Start()
    {
        originalScale = transform.localScale;
        originalPos = transform.position;

        foreach (Transform obj in objectsToScale)
        {
            if (obj != null) originalObjectScales.Add(obj.localScale);
        }

        // Start the random bobbing loop here
        StartCoroutine(RandomBobBrain());
    }

    private IEnumerator RandomBobBrain()
    {
        while (true)
        {
            // Wait for a random duration (e.g., between 2 and 6 seconds)
            float waitTime = Random.Range(2f, 6f);
            yield return new WaitForSeconds(waitTime);

            if (!isAnimating)
            {
                // We "yield return" the routine so it waits for the 
                // animation to finish before starting the next wait timer
                yield return StartCoroutine(BobRoutine());
            }
        }
    }



    private Vector3 CalculateNewScale(float addedForward, float addedSqueeze, out Vector3 localDir)
    {
        Vector3 newScale = originalScale;
        switch (scaleAxis)
        {
            case BobDirection.X:
                newScale.x += addedForward; newScale.y -= addedSqueeze; newScale.z -= addedSqueeze;
                localDir = Vector3.right;
                break;
            case BobDirection.Y:
                newScale.y += addedForward; newScale.x -= addedSqueeze; newScale.z -= addedSqueeze;
                localDir = Vector3.up;
                break;
            default:
                newScale.z += addedForward; newScale.x -= addedSqueeze; newScale.y -= addedSqueeze;
                localDir = Vector3.forward;
                break;
        }
        return newScale;
    }

    private void ApplyDisplacement(Vector3 startModelPos, Vector3[] startEyePositions, Vector3[] startObjPositions, float addedObjScale)
    {
        Vector3 displacement = transform.position - startModelPos;

        for (int i = 0; i < eyesToMove.Count; i++)
        {
            if (eyesToMove[i] != null) eyesToMove[i].position = startEyePositions[i] + displacement;
        }

        for (int i = 0; i < objectsToScale.Count; i++)
        {
            if (objectsToScale[i] != null)
            {
                objectsToScale[i].position = startObjPositions[i] + displacement;
                objectsToScale[i].localScale = originalObjectScales[i] + (Vector3.one * addedObjScale);
            }
        }
    }

    private void ResetAnimationState(Vector3 startModelPos, Vector3[] startEyePositions, Vector3[] startObjPositions)
    {
        transform.localScale = originalScale;
        transform.position = startModelPos;

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
    }

    IEnumerator BobRoutine()
    {
        isAnimating = true;
        Vector3 startModelPos = transform.position;

        Vector3[] startEyePositions = new Vector3[eyesToMove.Count];
        for (int i = 0; i < eyesToMove.Count; i++)
        {
            if (eyesToMove[i] != null) startEyePositions[i] = eyesToMove[i].position;
        }

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

            transform.localScale = CalculateNewScale(addedForward, addedSqueeze, out Vector3 localDir);

            if (pinBackSide)
            {
                Vector3 worldMoveDir = transform.TransformDirection(localDir);
                transform.position = startModelPos + (worldMoveDir * (addedForward / 2f));
            }

            ApplyDisplacement(startModelPos, startEyePositions, startObjPositions, addedObjScale);
            yield return null;
        }

        ResetAnimationState(startModelPos, startEyePositions, startObjPositions);
        isAnimating = false;
    }
}
