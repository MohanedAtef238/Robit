using UnityEngine;
using UnityEngine.InputSystem;

public class LocalSpaceLookAt : MonoBehaviour
{
    [Header("Targeting Settings")]
    public Camera mainCamera;
    public float turnSpeed = 15f;

    private Quaternion targetLocalRotation;
    private Quaternion originalLocalRotation;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        // Cache the starting local rotation from the rig template
        originalLocalRotation = transform.localRotation;
    }

    void Update()
    {
        if (mainCamera == null) return;

        // 1. Find where the mouse is pointing in the world
        if (Mouse.current == null) return;
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane plane = new Plane(transform.forward, transform.position);

        if (plane.Raycast(ray, out float enterDistance))
        {
            Vector3 targetWorldPoint = ray.GetPoint(enterDistance);

            // 2. CONVERT the world point into local space relative to the eye's parent bone
            Vector3 localTargetPoint = transform.parent.InverseTransformPoint(targetWorldPoint);
            Vector3 localEyePos = transform.localPosition;
            Vector3 localLookDir = localTargetPoint - localEyePos;

            if (localLookDir != Vector3.zero)
            {
                // Calculate target rotation purely in local space coordinates
                targetLocalRotation = Quaternion.LookRotation(localLookDir, Vector3.up);
            }
        }
    }

    // Since the PlayableGraph handles bone evaluation right before rendering, 
    // using localRotation here lets us slip our changes into the local matrix space.
    void LateUpdate()
    {
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRotation, Time.deltaTime * turnSpeed);
    }
}