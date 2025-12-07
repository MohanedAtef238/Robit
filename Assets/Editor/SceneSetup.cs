using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class SceneSetup : Editor
{
    [MenuItem("Desktop App Launcher/Setup Project Scenes")]
    public static void SetupScenes()
    {
        // Create and set up the MainScene
        Scene mainScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        mainScene.name = "MainScene";

        GameObject desktopParser = new GameObject("DesktopParser");
        desktopParser.AddComponent<DesktopParser>();

        GameObject appLauncherUIManager = new GameObject("AppLauncherUIManager");
        AppLauncherUI appLauncherUI = appLauncherUIManager.AddComponent<AppLauncherUI>();

        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject cardContainer = new GameObject("CardContainer");
        cardContainer.transform.SetParent(canvasGO.transform);
        RectTransform cardContainerRect = cardContainer.AddComponent<RectTransform>();
        cardContainerRect.anchorMin = Vector2.zero;
        cardContainerRect.anchorMax = Vector2.one;
        cardContainerRect.offsetMin = new Vector2(50, 50); // Add some padding
        cardContainerRect.offsetMax = new Vector2(-50, -50);
        GridLayoutGroup gridLayout = cardContainer.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(150, 200);
        gridLayout.spacing = new Vector2(15, 15);
        gridLayout.childAlignment = TextAnchor.UpperCenter;
        
        // Add a Status Text UI element for debugging
        GameObject statusTextGO = new GameObject("StatusText");
        statusTextGO.transform.SetParent(canvasGO.transform);
        Text statusText = statusTextGO.AddComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusText.fontSize = 24;
        statusText.color = Color.white;
        statusText.alignment = TextAnchor.MiddleCenter;
        RectTransform statusRect = statusTextGO.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0.5f);
        statusRect.anchorMax = new Vector2(0.5f, 0.5f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.sizeDelta = new Vector2(800, 200);

        appLauncherUI.desktopParser = desktopParser.GetComponent<DesktopParser>();
        appLauncherUI.cardContainer = cardContainer.GetComponent<RectTransform>();
        appLauncherUI.statusText = statusText; // Link the new text element

        GameObject eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<StandaloneInputModule>();

        EditorSceneManager.SaveScene(mainScene, "Assets/MainScene.unity");

        // Create and set up the OverlayScene
        Scene overlayScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        overlayScene.name = "OverlayScene";

        GameObject mainCameraGO = new GameObject("Main Camera");
        Camera mainCamera = mainCameraGO.AddComponent<Camera>();
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0, 0, 0, 0);
        mainCameraGO.AddComponent<Transparency>();
        mainCameraGO.AddComponent<PhysicsRaycaster>();

        GameObject overlayManager = new GameObject("OverlayManager");
        overlayManager.AddComponent<OverlayManager>();

        GameObject overlayCanvasGO = new GameObject("Canvas");
        Canvas overlayCanvas = overlayCanvasGO.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler overlayCanvasScaler = overlayCanvasGO.AddComponent<CanvasScaler>();
        overlayCanvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        overlayCanvasScaler.referenceResolution = new Vector2(1920, 1080);
        overlayCanvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        overlayCanvasScaler.matchWidthOrHeight = 0.5f;
        overlayCanvasGO.AddComponent<GraphicRaycaster>();

        GameObject homeButtonGO = new GameObject("HomeButton");
        homeButtonGO.transform.SetParent(overlayCanvasGO.transform);
        homeButtonGO.AddComponent<Image>();
        Button homeButton = homeButtonGO.AddComponent<Button>();
        homeButtonGO.AddComponent<HomeButton>();
        homeButtonGO.AddComponent<UIClickHandler>();
        RectTransform homeButtonRect = homeButtonGO.GetComponent<RectTransform>();
        homeButtonRect.anchorMin = new Vector2(1, 0);
        homeButtonRect.anchorMax = new Vector2(1, 0);
        homeButtonRect.pivot = new Vector2(1, 0);
        homeButtonRect.anchoredPosition = new Vector2(-20, 20);
        homeButtonRect.sizeDelta = new Vector2(160, 30);
        
        GameObject eventSystemOverlayGO = new GameObject("EventSystem");
        eventSystemOverlayGO.AddComponent<EventSystem>();
        eventSystemOverlayGO.AddComponent<StandaloneInputModule>();

        EditorSceneManager.SaveScene(overlayScene, "Assets/OverlayScene.unity");
        
        // Add scenes to build settings
        EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene("Assets/MainScene.unity", true),
            new EditorBuildSettingsScene("Assets/OverlayScene.unity", true)
        };

        Debug.Log("Scenes created and configured with UI Scaling and Debug Status Text. Build settings updated.");
    }
}