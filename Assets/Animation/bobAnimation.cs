using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BouncyLifeCycle : MonoBehaviour
{
    public enum BobDirection { X, Y, Z }

    [Header("Main Character Shrink Settings")]
    public BobDirection compressAxis = BobDirection.Y;
    [Tooltip("How much the body shrinks along the main axis (e.g., top-to-bottom).")]
    public float shrinkMainAmount = 0.5f;
    [Tooltip("How much the body shrinks inward from the sides simultaneously.")]
    public float shrinkSidesAmount = 0.3f;
    public float speed = 10f;
    public bool pinBackSide = true;

    [Header("The Eye Setup")]
    [Tooltip("Drag your eye objects straight from your normal hierarchy here.")]
    public List<Transform> eyesToAnimate;

    [Tooltip("How much the eyes push forward out of their sockets.")]
    public float eyePopIntensity = 5f; // Bumped up because your model's scale is large!

    [Tooltip("MULTIPLIER: 2 means the eyes will DOUBLE in size (60 -> 120). 3 means TRIPLE.")]
    public float eyeScaleMultiplier = 100f;

    private Vector3 originalScale;
    private Vector3 originalLocalPos;

    private List<Vector3> originalEyeLocalPositions = new List<Vector3>();
    private List<Vector3> originalEyeLocalScales = new List<Vector3>();

    private bool isAnimating = false;

    void Start()
    {
        originalScale = transform.localScale;
        originalLocalPos = transform.localPosition;

        // Snapshot original baseline values safely
        foreach (Transform eye in eyesToAnimate)
        {
            if (eye != null)
            {
                originalEyeLocalPositions.Add(eye.localPosition);
                originalEyeLocalScales.Add(eye.localScale);
            }
        }

        StartCoroutine(RandomBobBrain());
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.bKey.wasPressedThisFrame)
        {
            if (!isAnimating)
            {
                Debug.Log("[BouncyLifeCycle] 'B' Key Pressed! Scaling relative to base factor.");
                StartCoroutine(BobRoutine());
            }
        }
    }

    private IEnumerator RandomBobBrain()
    {
        while (true)
        {
            float waitTime = Random.Range(2f, 6f);
            yield return new WaitForSeconds(waitTime);

            if (!isAnimating)
            {
                yield return StartCoroutine(BobRoutine());
            }
        }
    }

    private Vector3 CalculateNewScale(float currentMainShrink, float currentSideShrink, out Vector3 localDir)
    {
        Vector3 newScale = originalScale;
        switch (compressAxis)
        {
            case BobDirection.X:
                newScale.x -= currentMainShrink; newScale.y -= currentSideShrink; newScale.z -= currentSideShrink;
                localDir = Vector3.right;
                break;
            case BobDirection.Y:
                newScale.y -= currentMainShrink; newScale.x -= currentSideShrink; newScale.z -= currentSideShrink;
                localDir = Vector3.up;
                break;
            default:
                newScale.z -= currentMainShrink; newScale.x -= currentSideShrink; newScale.y -= currentSideShrink;
                localDir = Vector3.forward;
                break;
        }
        return newScale;
    }

    private void ApplyEyeExplosion(float sineWave)
    {
        for (int i = 0; i < eyesToAnimate.Count; i++)
        {
            if (eyesToAnimate[i] != null)
            {
                // 1. Position: Push forward relative to your larger scale space
                Vector3 forwardPush = Vector3.forward * (sineWave * eyePopIntensity);
                eyesToAnimate[i].localPosition = originalEyeLocalPositions[i] + forwardPush;

                // 2. Scale: Multiply instead of add! 
                // When sineWave is 1, this scales the eye by your multiplier factor smoothly.
                float scaleFactor = 1f + (sineWave * (eyeScaleMultiplier - 1f));
                eyesToAnimate[i].localScale = originalEyeLocalScales[i] * scaleFactor;
            }
        }
    }

    private void ResetAnimationState()
    {
        transform.localScale = originalScale;
        transform.localPosition = originalLocalPos;

        for (int i = 0; i < eyesToAnimate.Count; i++)
        {
            if (eyesToAnimate[i] != null)
            {
                eyesToAnimate[i].localPosition = originalEyeLocalPositions[i];
                eyesToAnimate[i].localScale = originalEyeLocalScales[i];
            }
        }
    }

    IEnumerator BobRoutine()
    {
        isAnimating = true;
        Vector3 startingLocalPos = transform.localPosition;

        float t = 0;
        while (t < Mathf.PI)
        {
            t += Time.deltaTime * speed;
            float currentT = Mathf.Min(t, Mathf.PI);
            float sineWave = Mathf.Sin(currentT);

            float currentMainShrink = sineWave * shrinkMainAmount;
            float currentSideShrink = sineWave * shrinkSidesAmount;

            transform.localScale = CalculateNewScale(currentMainShrink, currentSideShrink, out Vector3 localDir);

            if (pinBackSide)
            {
                transform.localPosition = startingLocalPos - (localDir * (currentMainShrink / 2f));
            }

            ApplyEyeExplosion(sineWave);
            yield return null;
        }

        ResetAnimationState();
        isAnimating = false;
    }
}