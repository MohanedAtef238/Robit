using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

public class AppLauncherUIToolkit : MonoBehaviour
{
    public DesktopParser desktopParser;
    public UIDocument uiDocument;
    public MonoBehaviour legacyUiToDisable;
    public string gridName = "app-grid";
    public string statusName = "status-text";
    public string headerName = "header";

    private ScrollView grid;
    private Label statusLabel;

    void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (legacyUiToDisable != null)
        {
            legacyUiToDisable.enabled = false;
        }
    }

    IEnumerator Start()
    {
        if (uiDocument == null)
        {
            Debug.LogError("[AppLauncherUIToolkit] UIDocument not set or missing on this GameObject.");
            yield break;
        }

        VisualElement root = uiDocument.rootVisualElement;
        LoadStylesheet(root);

        grid = root.Q<ScrollView>(gridName);
        statusLabel = root.Q<Label>(statusName);

        if (grid == null || statusLabel == null)
        {
            Debug.LogError("[AppLauncherUIToolkit] Required UI elements not found. Check UXML names.");
            yield break;
        }

        if (desktopParser == null)
        {
            statusLabel.text = "ERROR: DesktopParser not linked in inspector.";
            Debug.LogError(statusLabel.text);
            yield break;
        }

        if (AppLauncher.Instance == null)
        {
            new GameObject("AppLauncher").AddComponent<AppLauncher>();
        }

        statusLabel.text = "Scanning desktops for shortcuts...";

        float timeout = 10f;
        float elapsed = 0f;
        while (!desktopParser.parsingComplete && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        if (!desktopParser.parsingComplete)
        {
            statusLabel.text = "ERROR: Desktop parsing timed out.";
            Debug.LogError("[AppLauncherUIToolkit] DesktopParser did not complete in time");
            yield break;
        }

        GenerateAppCards(desktopParser.shortcuts);
    }

    void GenerateAppCards(List<ShortcutInfo> shortcuts)
    {
        grid.Clear();

        if (shortcuts.Count == 0)
        {
            statusLabel.text = "No shortcuts (.lnk files) were found on the User or Public desktops.";
            Debug.Log(statusLabel.text);
            return;
        }

        statusLabel.text = string.Empty;

        foreach (var shortcut in shortcuts)
        {
            Button card = BuildCard(shortcut);
            grid.Add(card);
        }
    }

    Button BuildCard(ShortcutInfo shortcut)
    {
        Button card = new Button(() => OnAppCardClick(shortcut.TargetPath, shortcut.WorkingDirectory));
        card.AddToClassList("app-card");
        card.text = string.Empty;
        EnforceSquare(card);

        Image icon = new Image();
        icon.AddToClassList("app-card__icon");
        if (shortcut.Icon != null)
        {
            icon.image = shortcut.Icon;
        }
        else
        {
            icon.AddToClassList("app-card__icon--missing");
        }

        Label label = new Label(shortcut.Name);
        label.AddToClassList("app-card__label");

        card.Add(icon);
        card.Add(label);
        return card;
    }

    void EnforceSquare(VisualElement element)
    {
        element.RegisterCallback<GeometryChangedEvent>(_ =>
        {
            float size = element.resolvedStyle.width;
            if (size > 0f)
            {
                element.style.height = size;
            }
        });
    }

    void OnAppCardClick(string path, string workingDirectory)
    {
        AppLauncher.Instance.LaunchApplication(path, workingDirectory);
    }

    void LoadStylesheet(VisualElement root)
    {
        StyleSheet sheet = Resources.Load<StyleSheet>("app-launcher");
        if (sheet != null)
        {
            root.styleSheets.Add(sheet);
        }
        else
        {
            Debug.LogWarning("[AppLauncherUIToolkit] app-launcher.uss not found in Resources.");
        }
    }
}
