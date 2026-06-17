using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Required for the New Input System

public class UIClickSplashSprites : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The parent Canvas RectTransform where the splashes should live.")]
    [SerializeField] private RectTransform canvasRectTransform;

    [Header("Splash Sprite Pool")]
    [Tooltip("List of raw 2D Sprites to choose from randomly.")]
    [SerializeField] private List<Sprite> splashSprites = new List<Sprite>();

    [Header("Spawn Settings")]
    [Tooltip("How long the splash stays on screen before disappearing (in seconds).")]
    [SerializeField] private float splashLifetime = 1.5f;

    [Tooltip("The size of the spawned splash image in pixels.")]
    [SerializeField] private Vector2 splashSize = new Vector2(100f, 100f);

    private void Update()
    {
        // Check if a pointer exists, and if its primary action button (press) was activated this frame
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            SpawnSpriteSplash();
        }
    }

    private void SpawnSpriteSplash()
    {
        if (splashSprites == null || splashSprites.Count == 0) return;
        if (canvasRectTransform == null) return;

        // 1. Pick a random sprite
        int randomIndex = Random.Range(0, splashSprites.Count);
        Sprite selectedSprite = splashSprites[randomIndex];

        // 2. Get the current pointer position (works for both Mouse and Touch)
        Vector2 pointerPosition = Pointer.current.position.ReadValue();

        // 3. Convert screen position to Canvas local position
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform,
            pointerPosition,
            null,
            out localPoint
        );

        // 4. Create UI GameObject
        GameObject newSplashGo = new GameObject("Dynamic_Splash", typeof(RectTransform), typeof(Image));
        newSplashGo.transform.SetParent(canvasRectTransform, false);

        // 5. Configure RectTransform
        RectTransform rectTransform = newSplashGo.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = localPoint;
        rectTransform.sizeDelta = splashSize;

        // 6. Assign Sprite
        Image uiImage = newSplashGo.GetComponent<Image>();
        uiImage.sprite = selectedSprite;

        // 7. Auto-destroy
        Destroy(newSplashGo, splashLifetime);
    }
}