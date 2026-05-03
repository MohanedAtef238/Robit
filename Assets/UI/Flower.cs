using UnityEngine;
using UnityEngine.UIElements;

public class UIManager : MonoBehaviour
{
    [Header("UI Documents")]
    public UIDocument mainMenuDocument;
    public UIDocument flowerDocument;

    private Button flowerButton;

    void OnEnable()
    {
        var root = mainMenuDocument.rootVisualElement;

        // Verify the name "FlowerButton" matches your UI Builder exactly!
        flowerButton = root.Q<Button>("FlowerButton");

        if (flowerButton != null)
        {
            flowerButton.clicked += OnFlowerButtonClicked;
            RobitLogger.Log("FlowerButton found and linked!");
        }
        else
        {
            RobitLogger.LogError("UIManager: FlowerButton not found in Main Menu Document!");
        }
    }

    void OnFlowerButtonClicked()
    {
        RobitLogger.Log("FlowerButton was clicked!");

        // 1. CHANGE COLOR (The Debug Check)
        // This turns the button background green
        flowerButton.style.backgroundColor = new StyleColor(Color.green);

        // 2. TRIGGER THE UI
        if (flowerDocument != null)
        {
            flowerDocument.gameObject.SetActive(true);

            // Bring to front logic (just in case)
            flowerDocument.rootVisualElement.BringToFront();
        }
    }

    void OnDisable()
    {
        if (flowerButton != null)
        {
            flowerButton.clicked -= OnFlowerButtonClicked;
        }
    }
}
