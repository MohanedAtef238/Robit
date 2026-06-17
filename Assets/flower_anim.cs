using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider))]
public class FlowerStateToggle : MonoBehaviour
{
    private Animator _animator;

    void Start()
    {
        _animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            CheckClickTarget();
        }
    }

    private void CheckClickTarget()
    {
        if (Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                // Simply fire the trigger. The Animator state machine handles the rest!
                _animator.SetTrigger("ToggleTrigger");
                Debug.Log($"[FlowerToggle] Sent ToggleTrigger to {gameObject.name}");
            }
        }
    }
}