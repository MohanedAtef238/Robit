using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

// Manages eye-tracking calibration UI and communication with Python bridge
public class CalibrationManager : MonoBehaviour
{
    public static CalibrationManager Instance { get; private set; }
    
    // Events for UI and state tracking
    public event Action OnCalibrationStarted;
    public event Action OnCalibrationCompleted;
    public event Action<int, int> OnCalibrationPointProgress;
    public event Action<string> OnCalibrationFailed;
    
    [Header("Calibration Settings")]
    [SerializeField] private float dwellTimePerPoint = 1.5f;
    [SerializeField] private int calibrationPointCount = 9;
    [SerializeField] private float screenMargin = 0.1f;
    
    [Header("UDP Configuration")]
    [SerializeField] private string pythonIP = "127.0.0.1";
    [SerializeField] private int pythonCommandPort = 5006;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject calibrationPointPrefab;
    [SerializeField] private Color pointIdleColor = Color.white;
    [SerializeField] private Color pointActiveColor = Color.cyan;
    [SerializeField] private Color pointCompleteColor = Color.green;
    [SerializeField] private float pointPulseSpeed = 2f;
    
    [Header("UI Elements")]
    [SerializeField] private Canvas calibrationCanvas;
    [SerializeField] private Image backgroundOverlay;
    [SerializeField] private Text instructionText;
    [SerializeField] private Text progressText;
    
    public bool IsCalibrating { get; private set; }
    public bool IsCalibrated { get; private set; }
    public int CurrentPointIndex { get; private set; }
    
    private List<Vector2> calibrationPoints;
    private List<GameObject> pointVisuals;
    private UdpClient commandSocket;
    private IPEndPoint pythonEndpoint;
    private Coroutine calibrationCoroutine;
    
    private string CalibrationDataPath => 
        System.IO.Path.Combine(Application.persistentDataPath, "eye_calibration.json");
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        pointVisuals = new List<GameObject>();
        InitializeCommandSocket();
    }
    
    private void Start()
    {
        GenerateCalibrationPoints();
        
        if (calibrationCanvas != null)
            calibrationCanvas.gameObject.SetActive(false);
            
        CheckCalibrationStatus();
    }
    
    private void InitializeCommandSocket()
    {
        try
        {
            commandSocket = new UdpClient();
            pythonEndpoint = new IPEndPoint(IPAddress.Parse(pythonIP), pythonCommandPort);
            Debug.Log($"[Calibration] Command socket initialized for {pythonIP}:{pythonCommandPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Calibration] Failed to create command socket: {e.Message}");
        }
    }
    
    // Generates 9 or 5 point grid using normalized coordinates
    private void GenerateCalibrationPoints()
    {
        calibrationPoints = new List<Vector2>();
        
        float margin = screenMargin;
        float left = margin;
        float right = 1f - margin;
        float top = 1f - margin;
        float bottom = margin;
        float centerX = 0.5f;
        float centerY = 0.5f;
        
        if (calibrationPointCount == 9)
        {
            calibrationPoints.Add(new Vector2(left, top));
            calibrationPoints.Add(new Vector2(centerX, top));
            calibrationPoints.Add(new Vector2(right, top));
            calibrationPoints.Add(new Vector2(left, centerY));
            calibrationPoints.Add(new Vector2(centerX, centerY));
            calibrationPoints.Add(new Vector2(right, centerY));
            calibrationPoints.Add(new Vector2(left, bottom));
            calibrationPoints.Add(new Vector2(centerX, bottom));
            calibrationPoints.Add(new Vector2(right, bottom));
        }
        else
        {
            calibrationPoints.Add(new Vector2(centerX, centerY));
            calibrationPoints.Add(new Vector2(left, top));
            calibrationPoints.Add(new Vector2(right, top));
            calibrationPoints.Add(new Vector2(left, bottom));
            calibrationPoints.Add(new Vector2(right, bottom));
        }
    }
    
    private void SendCommand(string command, object data = null)
    {
        if (commandSocket == null) return;
        
        try
        {
            string json;
            if (data != null)
            {
                json = $"{{\"command\":\"{command}\",\"data\":{JsonUtility.ToJson(data)},\"timestamp\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}";
            }
            else
            {
                json = $"{{\"command\":\"{command}\",\"timestamp\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}";
            }
            
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            commandSocket.Send(bytes, bytes.Length, pythonEndpoint);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Calibration] Failed to send command: {e.Message}");
        }
    }
    
    public void CheckCalibrationStatus()
    {
        SendCommand("CHECK_CALIBRATION");
        
        if (System.IO.File.Exists(CalibrationDataPath))
        {
            try
            {
                string json = System.IO.File.ReadAllText(CalibrationDataPath);
                var data = JsonUtility.FromJson<CalibrationData>(json);
                
                // Expiry check (24h)
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - data.timestamp < 86400)
                {
                    IsCalibrated = true;
                    return;
                }
            }
            catch {}
        }
        
        IsCalibrated = false;
    }
    
    public void StartCalibration()
    {
        if (IsCalibrating) return;
        calibrationCoroutine = StartCoroutine(CalibrationRoutine());
    }
    
    public void CancelCalibration()
    {
        if (!IsCalibrating) return;
        
        if (calibrationCoroutine != null)
        {
            StopCoroutine(calibrationCoroutine);
            calibrationCoroutine = null;
        }
        
        SendCommand("CALIBRATE_CANCEL");
        CleanupCalibrationUI();
        IsCalibrating = false;
        OnCalibrationFailed?.Invoke("Cancelled");
    }
    
    private IEnumerator CalibrationRoutine()
    {
        IsCalibrating = true;
        CurrentPointIndex = 0;
        
        SetupCalibrationUI();
        SendCommand("CALIBRATE_START");
        OnCalibrationStarted?.Invoke();
        
        if (instructionText != null)
            instructionText.text = "Look at each point as it appears.\nKeep your head still.";
        yield return new WaitForSeconds(2f);
        
        for (int i = 0; i < calibrationPoints.Count; i++)
        {
            CurrentPointIndex = i;
            Vector2 normalizedPoint = calibrationPoints[i];
            
            OnCalibrationPointProgress?.Invoke(i + 1, calibrationPoints.Count);
            if (progressText != null)
                progressText.text = $"Point {i + 1} of {calibrationPoints.Count}";
            
            ShowCalibrationPoint(normalizedPoint, i);
            yield return new WaitForSeconds(0.3f);
            
            float elapsed = 0f;
            while (elapsed < dwellTimePerPoint)
            {
                elapsed += Time.deltaTime;
                UpdatePointVisual(i, elapsed / dwellTimePerPoint);
                yield return null;
            }
            
            SendCommand("CALIBRATE_POINT", new CalibrationPointData
            {
                index = i,
                x = normalizedPoint.x,
                y = normalizedPoint.y
            });
            
            CompletePointVisual(i);
            yield return new WaitForSeconds(0.2f);
        }
        
        SendCommand("CALIBRATE_END");
        SaveCalibrationData();
        
        if (instructionText != null)
            instructionText.text = "Calibration Complete!";
        yield return new WaitForSeconds(1.5f);
        
        CleanupCalibrationUI();
        IsCalibrating = false;
        IsCalibrated = true;
        OnCalibrationCompleted?.Invoke();
    }
    
    private void SetupCalibrationUI()
    {
        if (calibrationCanvas != null)
        {
            calibrationCanvas.gameObject.SetActive(true);
        }
        else
        {
            var canvasGO = new GameObject("CalibrationCanvas");
            calibrationCanvas = canvasGO.AddComponent<Canvas>();
            calibrationCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            calibrationCanvas.sortingOrder = 9999;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            backgroundOverlay = bgGO.AddComponent<Image>();
            backgroundOverlay.color = new Color(0, 0, 0, 0.85f);
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            
            var textGO = new GameObject("Instructions");
            textGO.transform.SetParent(canvasGO.transform, false);
            instructionText = textGO.AddComponent<Text>();
            instructionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            instructionText.fontSize = 32;
            instructionText.alignment = TextAnchor.MiddleCenter;
            instructionText.color = Color.white;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.2f, 0.1f);
            textRect.anchorMax = new Vector2(0.8f, 0.2f);
            textRect.sizeDelta = Vector2.zero;
            
            var progressGO = new GameObject("Progress");
            progressGO.transform.SetParent(canvasGO.transform, false);
            progressText = progressGO.AddComponent<Text>();
            progressText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            progressText.fontSize = 24;
            progressText.alignment = TextAnchor.MiddleCenter;
            progressText.color = Color.gray;
            var progressRect = progressGO.GetComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0.3f, 0.05f);
            progressRect.anchorMax = new Vector2(0.7f, 0.1f);
            progressRect.sizeDelta = Vector2.zero;
        }
        
        foreach (var visual in pointVisuals)
        {
            if (visual != null) Destroy(visual);
        }
        pointVisuals.Clear();
    }
    
    private void ShowCalibrationPoint(Vector2 normalizedPos, int index)
    {
        GameObject pointGO;
        if (calibrationPointPrefab != null)
        {
            pointGO = Instantiate(calibrationPointPrefab, calibrationCanvas.transform);
        }
        else
        {
            pointGO = new GameObject($"CalibrationPoint_{index}", typeof(RectTransform));
            pointGO.transform.SetParent(calibrationCanvas.transform, false);
            
            var outerGO = new GameObject("OuterRing", typeof(RectTransform), typeof(Image));
            outerGO.transform.SetParent(pointGO.transform, false);
            var outerImage = outerGO.GetComponent<Image>();
            outerImage.color = pointIdleColor;
            outerImage.sprite = CreateCircleSprite();
            var outerRect = outerGO.GetComponent<RectTransform>();
            outerRect.sizeDelta = new Vector2(60, 60);
            
            var innerGO = new GameObject("InnerDot", typeof(RectTransform), typeof(Image));
            innerGO.transform.SetParent(pointGO.transform, false);
            var innerImage = innerGO.GetComponent<Image>();
            innerImage.color = pointActiveColor;
            innerImage.sprite = CreateCircleSprite();
            var innerRect = innerGO.GetComponent<RectTransform>();
            innerRect.sizeDelta = new Vector2(20, 20);
        }
        
        var rectTransform = pointGO.GetComponent<RectTransform>() ?? pointGO.AddComponent<RectTransform>();
        rectTransform.anchorMin = normalizedPos;
        rectTransform.anchorMax = normalizedPos;
        rectTransform.anchoredPosition = Vector2.zero;
        
        pointVisuals.Add(pointGO);
    }
    
    private void UpdatePointVisual(int index, float progress)
    {
        if (index >= pointVisuals.Count || pointVisuals[index] == null) return;
        
        var pointGO = pointVisuals[index];
        float scale = 1f + 0.15f * Mathf.Sin(Time.time * pointPulseSpeed * Mathf.PI);
        pointGO.transform.localScale = Vector3.one * scale;
        
        foreach (var img in pointGO.GetComponentsInChildren<Image>())
        {
            img.color = Color.Lerp(pointActiveColor, pointCompleteColor, progress);
        }
    }
    
    private void CompletePointVisual(int index)
    {
        if (index >= pointVisuals.Count || pointVisuals[index] == null) return;
        
        var pointGO = pointVisuals[index];
        pointGO.transform.localScale = Vector3.one * 0.7f;
        foreach (var img in pointGO.GetComponentsInChildren<Image>())
        {
            img.color = pointCompleteColor;
        }
    }
    
    private void CleanupCalibrationUI()
    {
        foreach (var visual in pointVisuals)
        {
            if (visual != null) Destroy(visual);
        }
        pointVisuals.Clear();
        
        if (calibrationCanvas != null)
            calibrationCanvas.gameObject.SetActive(false);
    }
    
    private void SaveCalibrationData()
    {
        try
        {
            var data = new CalibrationData
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                pointCount = calibrationPoints.Count,
                screenWidth = Screen.width,
                screenHeight = Screen.height
            };
            
            System.IO.File.WriteAllText(CalibrationDataPath, JsonUtility.ToJson(data, true));
        }
        catch {}
    }
    
    private Sprite CreateCircleSprite()
    {
        int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= radius)
                {
                    float alpha = Mathf.Clamp01((radius - distance) / 2f);
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                else texture.SetPixel(x, y, Color.clear);
            }
        }
        
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
    
    private void OnDestroy()
    {
        if (commandSocket != null)
        {
            commandSocket.Close();
            commandSocket = null;
        }
    }
    
    [Serializable] private class CommandPacket { public string command; public long timestamp; }
    [Serializable] private class CalibrationPointData { public int index; public float x; public float y; }
    [Serializable] private class CalibrationData { public long timestamp; public int pointCount; public int screenWidth; public int screenHeight; }
}
