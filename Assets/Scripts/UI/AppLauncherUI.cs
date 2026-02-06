using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class AppLauncherUI : MonoBehaviour
{
    public DesktopParser desktopParser;
    public RectTransform cardContainer;
    public Text statusText; // Public reference to the new status text UI element
    private GameObject appCardPrefab;

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

        // Load the prefab from the Resources folder
        appCardPrefab = Resources.Load<GameObject>("AppCardPrefab");
        if (appCardPrefab == null)
        {
            statusText.text = "ERROR: AppCardPrefab not found in Resources folder.\nPlease run 'Desktop App Launcher/Create App Card Prefab' from the menu.";
            Debug.LogError(statusText.text);
            yield break;
        }

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
            GameObject cardInstance = Instantiate(appCardPrefab, cardContainer);
            cardInstance.name = shortcut.Name + " Card";

            // Get the UI components from the instantiated card
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
}
