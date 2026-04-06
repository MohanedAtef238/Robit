using System.Collections;
using UnityEngine;

public class RelativeEyelid : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Degrees to ADD to the current X rotation (e.g., 40)")]
    public float closedXOffset = 40f;
    public float blinkSpeed = 20f;

    private Quaternion openRotation;
    private Quaternion closedRotation;
    private Coroutine activeMove;

    void Start()
    {
        // 1. Capture the exact rotation from the Inspector
        openRotation = transform.localRotation;

        // 2. Calculate the closed state by ADDING the offset to the current X
        Vector3 currentEuler = openRotation.eulerAngles;

        // We use Euler to handle the math simply, then convert back to Quaternion
        float targetX = currentEuler.x + closedXOffset;
        closedRotation = Quaternion.Euler(targetX, currentEuler.y, currentEuler.z);
    }

    void Update()
    {
        // Mouse Down = Close
        if (Input.GetMouseButtonDown(0))
        {
            StopActiveMove();
            activeMove = StartCoroutine(AnimateEyelid(closedRotation));
        }

        // Mouse Up = Open
        if (Input.GetMouseButtonUp(0))
        {
            StopActiveMove();
            activeMove = StartCoroutine(AnimateEyelid(openRotation));
        }
    }

    private void StopActiveMove()
    {
        if (activeMove != null) StopCoroutine(activeMove);
    }

    IEnumerator AnimateEyelid(Quaternion target)
    {
        // Smoothly transition using Slerp (Spherical Linear Interpolation)
        while (Quaternion.Angle(transform.localRotation, target) > 0.01f)
        {
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                target,
                Time.deltaTime * blinkSpeed
            );
            yield return null;
        }
        transform.localRotation = target;
    }
}