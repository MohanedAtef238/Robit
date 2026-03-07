using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(UIDocument))]
public class AppLauncherUIToolkit : MonoBehaviour
{
    public DesktopParser desktopParser;
    public VisualTreeAsset desktopCardTemplate;

    private UIDocument uiDocument;
    private VisualElement rootParams;
    private VisualElement cardsContainer;
    private Label statusText;

    IEnumerator Start()
    {
        // Initialize WindowManager early to cache Unity's window handle
        WindowManager.Initialize();

        uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[AppLauncherUIToolkit] UIDocument or root is null");
            yield break;
        }

        rootParams = uiDocument.rootVisualElement;
        
        statusText = rootParams.Q<Label>("status-text");
        cardsContainer = rootParams.Q<VisualElement>("cards-container");

        if (statusText == null || cardsContainer == null)
        {
            Debug.LogError("[AppLauncherUIToolkit] UXML structure is missing required elements.");
            yield break;
        }

        statusText.text = "Scanning desktops for shortcuts...";

        if (desktopParser == null)
        {
            desktopParser = FindFirstObjectByType<DesktopParser>();
            if (desktopParser == null)
            {
                Debug.Log("[AppLauncherUIToolkit] DesktopParser not found in scene. Creating a new one automatically.");
                GameObject parserObj = new GameObject("DesktopParserInstance");
                desktopParser = parserObj.AddComponent<DesktopParser>();
            }
        }

        if (AppLauncher.Instance == null)
        {
            new GameObject("AppLauncher").AddComponent<AppLauncher>();
        }
        
        // Wait until DesktopParser has finished parsing
        float timeout = 10f;
        float elapsed = 0f;
        
        while (!desktopParser.parsingComplete && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
        
        if (!desktopParser.parsingComplete)
        {
            statusText.text = "ERROR: Desktop parsing timed out.";
            Debug.LogError("[AppLauncherUIToolkit] DesktopParser did not complete in time");
            yield break;
        }
        
        Debug.Log($"[AppLauncherUIToolkit] DesktopParser finished with {desktopParser.shortcuts.Count} shortcuts after {elapsed:F2}s");

        GenerateAppCards(desktopParser.shortcuts);
    }

    void GenerateAppCards(List<ShortcutInfo> shortcuts)
    {
        if (shortcuts.Count == 0)
        {
            statusText.text = "No shortcuts (.lnk files) were found on the User or Public desktops.";
            Debug.Log(statusText.text);
            return;
        }

        // Hide status text
        statusText.style.display = DisplayStyle.None;
        cardsContainer.Clear();

        foreach (var shortcut in shortcuts)
        {
            if (desktopCardTemplate == null)
            {
                Debug.LogError("[AppLauncherUIToolkit] Desktop Card Template is not assigned.");
                return;
            }

            // Instantiate UXML
            TemplateContainer cardContainer = desktopCardTemplate.Instantiate();
            cardContainer.style.flexShrink = 0; // Prevent squishing
            cardsContainer.Add(cardContainer);

            // Bind Data
            Label titleObj = cardContainer.Q<Label>("title");
            if (titleObj != null) titleObj.text = shortcut.Name.ToUpper();

            VisualElement iconContainer = cardContainer.Q<VisualElement>("icon-container");
            if (iconContainer != null && shortcut.Icon != null)
            {
                // Assign icon via background-image (supported in USS)
                iconContainer.style.backgroundImage = new StyleBackground(shortcut.Icon);
                // Keep the original aspect ratio
                iconContainer.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            }

            // Set up Hover & Click Events
            VisualElement triggerTracker = cardContainer.Q<VisualElement>("tracker");
            VisualElement innerCard = cardContainer.Q<VisualElement>("card");

            if (triggerTracker != null && innerCard != null)
            {
                // Click Event
                triggerTracker.RegisterCallback<ClickEvent>(evt =>
                {
                    OnAppCardClick(shortcut.TargetPath, shortcut.WorkingDirectory);
                });
            }
        }
    }

    void OnAppCardClick(string path, string workingDirectory)
    {
        Debug.Log($"[AppLauncherUIToolkit] Launching '{path}'");
        AppLauncher.Instance.LaunchApplication(path, workingDirectory);
    }
}
