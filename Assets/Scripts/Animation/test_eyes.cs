using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class EyeTestTrigger : MonoBehaviour
{
    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
        {
            Debug.Log($"[EyeTest] 'T' pressed. Current Scale before change: {transform.localScale}");

            // 1. Force an instant, massive change that should be impossible to miss
            transform.localScale = new Vector3(10f, 10f, 10f);
            transform.position += transform.forward * 5f;

            Debug.Log($"[EyeTest] Scale immediately after assignment: {transform.localScale}");

            // 2. Schedule a check for the very next frame
            StartCoroutine(VerifyNextFrame());
        }
    }

    private IEnumerator VerifyNextFrame()
    {
        yield return null; // Wait exactly 1 frame
        Debug.Log($"[EyeTest] Scale on the next frame: {transform.localScale}");
    }
}