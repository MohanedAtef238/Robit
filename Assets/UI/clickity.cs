using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to any 3D object with a Collider.
/// Fires:
///   OnObjectClicked — on left mouse button down over this object
///   OnObjectHeld    — after holding the mouse down for HoldThreshold seconds over this object
/// Completely self-contained — no UI dependencies.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ClickableObject : MonoBehaviour
{
    [Header("Click Settings")]
    [Tooltip("Which camera to raycast from. Defaults to Camera.main if left empty.")]
    [SerializeField] private Camera _raycastCamera;

    [Tooltip("Which layers count as 'this object'. Leave as Default or set to your object's layer.")]
    [SerializeField] private LayerMask _clickableLayers = ~0;

    [Header("Hold Settings")]
    [Tooltip("How long the mouse must be held over this object before OnObjectHeld fires (seconds).")]
    [SerializeField] private float _holdThreshold = 1f;

    [Header("Events")]
    public UnityEvent OnObjectClicked;
    public UnityEvent OnObjectHeld;

    // ── internals ─────────────────────────────────────────────────────────────
    private bool _isHoldingOverObject = false;
    private float _holdTimer = 0f;
    private bool _holdFired = false;

    private void Awake()
    {
        if (_raycastCamera == null)
            _raycastCamera = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        bool pressedThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
        bool releasedThisFrame = Mouse.current.leftButton.wasReleasedThisFrame;
        bool isHeld = Mouse.current.leftButton.isPressed;

        Ray ray = _raycastCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        bool hitsThis = Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _clickableLayers)
                        && hit.collider.gameObject == gameObject;

        // ── Press ─────────────────────────────────────────────────────────────
        if (pressedThisFrame && hitsThis)
        {
            OnObjectClicked?.Invoke();
            _isHoldingOverObject = true;
            _holdTimer = 0f;
            _holdFired = false;
        }

        // ── Hold timer ────────────────────────────────────────────────────────
        if (_isHoldingOverObject && isHeld && !_holdFired)
        {
            _holdTimer += Time.deltaTime;

            if (_holdTimer >= _holdThreshold)
            {
                OnObjectHeld?.Invoke();
                _holdFired = true;
            }
        }

        // ── Release ───────────────────────────────────────────────────────────
        if (releasedThisFrame)
        {
            _isHoldingOverObject = false;
            _holdTimer = 0f;
            _holdFired = false;
        }
    }
}