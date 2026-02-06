using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class EyeTrackerSetup : EditorWindow
{
    [MenuItem("Tools/EyeTracker/Auto-Setup Scene")]
    public static void SetupScene()
    {
        // 1. Create Main Manager Object
        GameObject root = GameObject.Find("EyeTracker");
        if (root == null)
        {
            root = new GameObject("EyeTracker");
            Undo.RegisterCreatedObjectUndo(root, "Create EyeTracker");
        }
        Selection.activeGameObject = root;

        // 2. Attach Core Scripts
        var calibrationManager = GetOrAddComponent<CalibrationManager>(root);
        var eyeInput = GetOrAddComponent<EyeTrackingInput>(root);
        var tester = GetOrAddComponent<EyeTrackingTester>(root);

        // 3. Setup UI Hierarchy
        GameObject canvasGO = GameObject.Find("CalibrationCanvas");
        if (canvasGO == null)
        {
            canvasGO = new GameObject("CalibrationCanvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Calibration Canvas");
        }
        
        // Canvas Setup
        var canvas = GetOrAddComponent<Canvas>(canvasGO);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        
        var scaler = GetOrAddComponent<CanvasScaler>(canvasGO);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        GetOrAddComponent<GraphicRaycaster>(canvasGO);

        // Background
        GameObject bgGO = FindChild(canvasGO, "Background");
        if (bgGO == null)
        {
            bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            Undo.RegisterCreatedObjectUndo(bgGO, "Create Background");
        }
        var bgImage = GetOrAddComponent<Image>(bgGO);
        bgImage.color = new Color(0, 0, 0, 0.85f);
        var bgRect = bgImage.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Instructions
        GameObject instrGO = FindChild(canvasGO, "Instructions");
        if (instrGO == null)
        {
            instrGO = new GameObject("Instructions");
            instrGO.transform.SetParent(canvasGO.transform, false);
            Undo.RegisterCreatedObjectUndo(instrGO, "Create Instructions");
        }
        var instrText = GetOrAddComponent<Text>(instrGO);
        instrText.alignment = TextAnchor.UpperCenter;
        instrText.color = Color.white;
        instrText.fontSize = 48;
        instrText.text = "Look at the dots";
        instrText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        
        var instrRect = instrText.rectTransform;
        instrRect.anchorMin = new Vector2(0, 0.8f);
        instrRect.anchorMax = new Vector2(1, 1);
        instrRect.offsetMin = Vector2.zero;
        instrRect.offsetMax = Vector2.zero;

        // Progress
        GameObject progGO = FindChild(canvasGO, "Progress");
        if (progGO == null)
        {
            progGO = new GameObject("Progress");
            progGO.transform.SetParent(canvasGO.transform, false);
            Undo.RegisterCreatedObjectUndo(progGO, "Create Progress");
        }
        var progText = GetOrAddComponent<Text>(progGO);
        progText.alignment = TextAnchor.LowerCenter;
        progText.color = Color.gray;
        progText.fontSize = 24;
        progText.text = "0 / 9";
        progText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var progRect = progText.rectTransform;
        progRect.anchorMin = new Vector2(0, 0);
        progRect.anchorMax = new Vector2(1, 0.1f);
        progRect.offsetMin = Vector2.zero;
        progRect.offsetMax = Vector2.zero;

        // Calibration Point Prefab (Create a temporary one if needed or just let Manager handle it)
        // We will explicitly set references now using SerializedObject to be safe
        
        SerializedObject so = new SerializedObject(calibrationManager);
        so.Update();
        
        // Find properties backing field names (usually camelCase or _camelCase)
        // Unity serialization for private fields usually matches name if [SerializeField] is used
        so.FindProperty("calibrationCanvas").objectReferenceValue = canvas;
        so.FindProperty("backgroundOverlay").objectReferenceValue = bgImage;
        so.FindProperty("instructionText").objectReferenceValue = instrText;
        so.FindProperty("progressText").objectReferenceValue = progText;
        
        so.ApplyModifiedProperties();
        
        canvasGO.SetActive(false); // Hide by default

        Debug.Log("<color=green>EyeTracker Scene Setup Complete!</color>");
    }

    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null) comp = go.AddComponent<T>();
        return comp;
    }

    private static GameObject FindChild(GameObject parent, string name)
    {
        Transform t = parent.transform.Find(name);
        return t != null ? t.gameObject : null;
    }
}
