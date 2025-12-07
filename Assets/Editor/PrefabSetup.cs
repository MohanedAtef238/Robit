using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class PrefabSetup : Editor
{
    private const string prefabPath = "Assets/Resources/AppCardPrefab.prefab";

    [MenuItem("Desktop App Launcher/Create App Card Prefab")]
    public static void CreateAppCardPrefab()
    {
        // Create the card - wider horizontal layout (300x80) for side-by-side arrangement
        GameObject appCardPrefab = new GameObject("AppCardPrefab");
        RectTransform cardRect = appCardPrefab.AddComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(300, 80);
        Image cardImage = appCardPrefab.AddComponent<Image>();
        cardImage.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        appCardPrefab.AddComponent<Button>();

        // Icon on the left side - larger and prominent
        GameObject icon = new GameObject("Icon");
        icon.transform.SetParent(appCardPrefab.transform, false);
        RectTransform iconRect = icon.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0, 0);
        iconRect.anchorMax = new Vector2(0, 1);
        iconRect.pivot = new Vector2(0, 0.5f);
        iconRect.anchoredPosition = new Vector2(10, 0);
        iconRect.sizeDelta = new Vector2(60, 60);
        RawImage rawImage = icon.AddComponent<RawImage>();
        rawImage.color = Color.white; // Ensure full color, no tint

        // App Name on the right side of icon
        GameObject appName = new GameObject("AppName");
        appName.transform.SetParent(appCardPrefab.transform, false);
        RectTransform nameRect = appName.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = new Vector2(80, 10); // Left padding after icon
        nameRect.offsetMax = new Vector2(-10, -10); // Right/top/bottom padding
        Text nameText = appName.AddComponent<Text>();
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = 18;
        nameText.color = Color.white;
        
        // Save the prefab
        PrefabUtility.SaveAsPrefabAsset(appCardPrefab, prefabPath);
        DestroyImmediate(appCardPrefab);
        
        Debug.Log("AppCardPrefab created at: " + prefabPath + " (300x80 horizontal layout)");
    }
}
