using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterAnimationTester : MonoBehaviour
{
    private CharacterAnimationController animController;

    void Start()
    {
        animController = GetComponent<CharacterAnimationController>();

        if (animController == null)
        {
            Debug.LogError("CharacterAnimationTester requires the CharacterAnimationController script on the same GameObject!");
        }
    }

    void Update()
    {
        if (animController == null) return;

        // Get a reference to the current keyboard
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return; // No keyboard connected

        // Key 0 -> Reset to default pose
        if (keyboard.digit0Key.wasPressedThisFrame)
        {
            Debug.Log("[Tester] Key 0 pressed. Resetting character to default pose.");
            animController.ResetToDefaultPose();
        }

        // Key 1 -> bob
        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            TriggerTestAnimation("bob");
        }

        // Key 2 -> eye_bulge
        if (keyboard.digit2Key.wasPressedThisFrame)
        {
            TriggerTestAnimation("eye_bulge");
        }

        // Key 3 -> huh
        if (keyboard.digit3Key.wasPressedThisFrame)
        {
            TriggerTestAnimation("huh");
        }

        // Key 4 -> new_waving
        if (keyboard.digit4Key.wasPressedThisFrame)
        {
            TriggerTestAnimation("new_waving");
        }

        // Key 5 -> nod
        if (keyboard.digit5Key.wasPressedThisFrame)
        {
            TriggerTestAnimation("nod");
        }

        // Key 6 -> shake_no
        if (keyboard.digit6Key.wasPressedThisFrame)
        {
            TriggerTestAnimation("shake_no");
        }

        // Key 7 -> talking
        if (keyboard.digit7Key.wasPressedThisFrame)
        {
            TriggerTestAnimation("talking");
        }

        // Key 8 -> melt
        if (keyboard.digit8Key.wasPressedThisFrame)
        {
            TriggerTestAnimation("melt");
        }

        // Key 9 -> croak
        if (keyboard.digit9Key.wasPressedThisFrame)
        {
            TriggerTestAnimation("croak");
        }
    }

    private void TriggerTestAnimation(string animName)
    {
        Debug.Log($"[Tester] Triggering animation: {animName}");
        animController.PlayAnimation(animName);
    }
}