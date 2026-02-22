using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class AppLauncherUI : MonoBehaviour
{
    public DesktopParser desktopParser;
    public RectTransform cardContainer;
    public Text statusText; // Public reference to the new status text UI element

    IEnumerator Start()
    {
        // Initialize WindowManager early to cache Unity's window handle, this ensures we wont forget the unity app calls, operation wise.
        WindowManager.Initialize();
        
        if (statusText == null)
        {
            Debug.LogError("Status Text not set in the inspector!");
            yield break;
        }

        statusText.text = "Initializing...";

        if (desktopParser == null || cardContainer == null)
        {
            statusText.text = "ERROR: DesktopParser or CardContainer not linked in the inspector.";
            Debug.LogError(statusText.text);
            yield break;
        }

        if (AppLauncher.Instance == null)
        {
            new GameObject("AppLauncher").AddComponent<AppLauncher>();
        }
        
        statusText.text = "Scanning desktops for shortcuts...";
        
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
            Debug.LogError("[AppLauncherUI] DesktopParser did not complete in time");
            yield break;
        }
        
        Debug.Log($"[AppLauncherUI] DesktopParser finished with {desktopParser.shortcuts.Count} shortcuts after {elapsed:F2}s");

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

        // If we found shortcuts, clear the status message
        statusText.text = "";

        foreach (var shortcut in shortcuts)
        {
            GameObject cardInstance = CreateAppCard(cardContainer);
            cardInstance.name = shortcut.Name + " Card";

            // Get the UI components from the dynamically created card
            RawImage iconImage = cardInstance.transform.Find("Icon").GetComponent<RawImage>();
            Text nameText = cardInstance.transform.Find("AppName").GetComponent<Text>();
            Button button = cardInstance.GetComponent<Button>();

            // Set the card's details
            nameText.text = shortcut.Name;
            
            // Debug: Check if texture is valid
            if (shortcut.Icon == null)
            {
                Debug.LogError($"[AppLauncherUI] Icon texture is NULL for '{shortcut.Name}'");
            }
            else
            {
                Debug.Log($"[AppLauncherUI] Setting icon for '{shortcut.Name}': {shortcut.Icon.width}x{shortcut.Icon.height}, format={shortcut.Icon.format}, isReadable={shortcut.Icon.isReadable}");
                iconImage.texture = shortcut.Icon;
                
                // Force the RawImage to refresh
                iconImage.SetNativeSize();
                iconImage.enabled = false;
                iconImage.enabled = true;
                
                Debug.Log($"[AppLauncherUI] RawImage texture assigned: {(iconImage.texture != null ? "YES" : "NO")}, RawImage enabled: {iconImage.enabled}, GameObject active: {iconImage.gameObject.activeInHierarchy}");
            }

            // Add a listener to the button to launch the app
            button.onClick.AddListener(() => OnAppCardClick(shortcut.TargetPath, shortcut.WorkingDirectory));
        }
    }

    void OnAppCardClick(string path, string workingDirectory)
    {
        AppLauncher.Instance.LaunchApplication(path, workingDirectory);
    }

    private GameObject CreateAppCard(Transform parent)
    {
        GameObject appCard = new GameObject("AppCardPrefab");
        appCard.transform.SetParent(parent, false);

        RectTransform cardRect = appCard.AddComponent<RectTransform>();
        Image cardImage = appCard.AddComponent<Image>();
        appCard.AddComponent<Button>();

        HorizontalLayoutGroup layout = appCard.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 10;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childAlignment = TextAnchor.MiddleLeft;

        cardImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        cardRect.sizeDelta = new Vector2(300, 80);

        GameObject icon = new GameObject("Icon");
        icon.transform.SetParent(appCard.transform, false);
        RectTransform iconRect = icon.AddComponent<RectTransform>();
        icon.AddComponent<RawImage>();
        iconRect.sizeDelta = new Vector2(60, 60);

        GameObject appName = new GameObject("AppName");
        appName.transform.SetParent(appCard.transform, false);
        RectTransform textRect = appName.AddComponent<RectTransform>();
        Text textComponent = appName.AddComponent<Text>();
        textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        textComponent.fontSize = 20;
        textComponent.color = Color.white;
        textComponent.alignment = TextAnchor.MiddleLeft;
        textRect.sizeDelta = new Vector2(200, 60);

        return appCard;
    }
}
