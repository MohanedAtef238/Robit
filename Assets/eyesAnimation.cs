using UnityEngine;

public class EyeSocketLockYZ : MonoBehaviour
{
    [Header("Rotation Limits")]
    public float horizontalLimit = 30f;
    public float verticalLimit = 20f;
    public float smoothing = 15f;

    private Vector3 socketRelativePos;
    private Quaternion initialLocalRot;

    void Start()
    {
        socketRelativePos = transform.localPosition;
        initialLocalRot = transform.localRotation;
    }

    void LateUpdate()
    {
        // HARD LOCK to the socket
        transform.localPosition = socketRelativePos;

        ApplyLookRotation();
    }

    void ApplyLookRotation()
    {
        // 1. Get Mouse relative to screen center
        Vector2 mousePos = new Vector2(
            (Input.mousePosition.x / Screen.width) - 0.5f,
            (Input.mousePosition.y / Screen.height) - 0.5f
        );

        // 2. Map Mouse X to Target Z and Mouse Y to Target Y
        // (Adjust the negative signs if the eye moves opposite to the mouse)
        float targetZ = mousePos.x * horizontalLimit * 2f;
        float targetY = -mousePos.y * verticalLimit * 2f;

        // 3. Construct the rotations for Y and Z only
        Quaternion yRot = Quaternion.AngleAxis(targetY, Vector3.forward);
        Quaternion zRot = Quaternion.AngleAxis(targetZ, -Vector3.up);

        // 4. Combine with initial rotation (X remains locked to the Editor value)
        Quaternion targetRotation = initialLocalRot * yRot * zRot;

        // 5. Smoothly apply
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRotation,
            Time.deltaTime * smoothing
        );
    }
}