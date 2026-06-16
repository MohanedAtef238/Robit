using UnityEngine;
using UnityEngine.InputSystem;

public class NuclearEyeFollower : MonoBehaviour
{
    [Header("References")]
    public Animator characterAnimator;
    public Transform leftEyeball;
    public Transform rightEyeball;

    [Header("Constraints")]
    public Vector2 maxRotationAngle = new Vector2(30f, 30f);
    public float followSpeed = 15f;
    public float targetDistance = 5f; // Adjust this if eyes look crossed or weird

    [Header("Timer Settings")]
    public float stopDelay = 1.0f;

    private Quaternion leftInitialRotation;
    private Quaternion rightInitialRotation;
    private Vector2 lastMousePosition;
    private float idleTimer;
    private bool isTracking = false;

    void Start()
    {
        // Capture initial local rotations as the "center" point
        if (leftEyeball) leftInitialRotation = leftEyeball.localRotation;
        if (rightEyeball) rightInitialRotation = rightEyeball.localRotation;

        if (characterAnimator == null)
            characterAnimator = GetComponentInParent<Animator>();

        if (Mouse.current != null)
            lastMousePosition = Mouse.current.position.ReadValue();
    }

    void LateUpdate()
    {
        if (characterAnimator == null || Mouse.current == null) return;

        Vector2 currentMousePos = Mouse.current.position.ReadValue();

        // Mouse sensitivity threshold
        if (Vector2.Distance(currentMousePos, lastMousePosition) > 0.5f)
        {
            isTracking = true;
            idleTimer = 0f;
            characterAnimator.enabled = false;
        }
        else
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= stopDelay && isTracking)
            {
                isTracking = false;
                characterAnimator.enabled = true;
                // Reset eyes to center when animator takes back over
                ResetEyes();
            }
        }

        lastMousePosition = currentMousePos;

        if (isTracking)
        {
            UpdateEyeRotation(leftEyeball, leftInitialRotation, currentMousePos);
            UpdateEyeRotation(rightEyeball, rightInitialRotation, currentMousePos);
        }
    }

    void UpdateEyeRotation(Transform eyeball, Quaternion initialRot, Vector2 mousePos)
    {
        if (eyeball == null) return;

        // 1. Get world position of the mouse projected into the scene
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        Vector3 targetWorldPos = ray.GetPoint(targetDistance);

        // 2. Find the direction from the eye to that target
        Vector3 directionToTarget = targetWorldPos - eyeball.position;

        // 3. Convert that world direction into a local direction relative to the eye's parent
        Vector3 localTargetDir = eyeball.parent.InverseTransformDirection(directionToTarget);

        // 4. Create a rotation looking in that local direction
        Quaternion lookRot = Quaternion.LookRotation(localTargetDir, Vector3.up);

        // 5. Extract and clamp angles
        Vector3 angles = lookRot.eulerAngles;
        float x = NormalizeAngle(angles.x);
        float y = NormalizeAngle(angles.y);

        x = Mathf.Clamp(x, -maxRotationAngle.x, maxRotationAngle.x);
        y = Mathf.Clamp(y, -maxRotationAngle.y, maxRotationAngle.y);

        // 6. Smoothly apply the rotation relative to the original start rotation
        Quaternion finalRot = initialRot * Quaternion.Euler(x, y, 0);
        eyeball.localRotation = Quaternion.Slerp(eyeball.localRotation, finalRot, Time.deltaTime * followSpeed);
    }

    void ResetEyes()
    {
        if (leftEyeball) leftEyeball.localRotation = leftInitialRotation;
        if (rightEyeball) rightEyeball.localRotation = rightInitialRotation;
    }

    float NormalizeAngle(float angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }
}