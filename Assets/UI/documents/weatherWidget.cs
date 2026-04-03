using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

public class DesktopWidget : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private Label locationLabel;
    private Label degreeLabel;
    private Label celsiusLabel;
    private Label statusLabel;

    private Button minuteHand;
    private Button hourHand;

    private Label digitalClockLabel;
    private Label digitalClockM;

    private const string WEATHER_API_KEY = "5df624a0199e71b2c8377b7bff309ae0";

    private float currentMinuteAngle;
    private float currentHourAngle;

    // Set your fallback city here
    [SerializeField] private string fallbackCity = "Cairo";

    void Start()
    {
        var root = uiDocument.rootVisualElement;

        locationLabel = root.Q<Label>("locationLabel");
        degreeLabel = root.Q<Label>("degreeLabel");
        celsiusLabel = root.Q<Label>("celsiusLabel");
        statusLabel = root.Q<Label>("statusLabel");

        minuteHand = root.Q<Button>("minuteHand");
        hourHand = root.Q<Button>("hourHand");

        digitalClockLabel = root.Q<Label>("digitalClockLabel");
        digitalClockM = root.Q<Label>("digitalClockM");


        StartCoroutine(GetWeather(fallbackCity));
    }

    void Update()
    {
        UpdateClockSmooth();
     
        UpdateDigitalClock();
    }

    // -----------------------------
    // CLOCK
    // -----------------------------
    void UpdateClockSmooth()
    {
        DateTime now = DateTime.Now;

        float minute = now.Minute + now.Second / 60f;
        float hour = (now.Hour % 12) + minute / 60f;

        float targetMinuteAngle = minute * 6f; // 0° at top
        float targetHourAngle = hour * 30f;    // 0° at top

        // Smooth interpolation
        currentMinuteAngle = Mathf.LerpAngle(currentMinuteAngle, targetMinuteAngle, Time.deltaTime * 8f);
        currentHourAngle = Mathf.LerpAngle(currentHourAngle, targetHourAngle, Time.deltaTime * 8f);

        // Rotate the buttons
        if (minuteHand != null)
            minuteHand.style.rotate = new Rotate(new Angle(currentMinuteAngle));
        if (hourHand != null)
            hourHand.style.rotate = new Rotate(new Angle(currentHourAngle));
    }

    void UpdateDigitalClock()
    {
        DateTime now = DateTime.Now;
        string hour = now.Hour % 12 == 0 ? "12" : (now.Hour % 12).ToString("00");
        string minute = now.Minute.ToString("00");
        string second = now.Second.ToString("00");
        string amPm = now.Hour >= 12 ? "PM" : "AM";

        if (digitalClockLabel != null)
            digitalClockLabel.text = $"{hour}:{minute}";
        digitalClockM.text = $"{amPm}";

    }

    // -----------------------------
    // WEATHER
    // -----------------------------
    IEnumerator GetWeather(string city)
    {
        string url = $"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={WEATHER_API_KEY}&units=metric";

        UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Weather request failed: " + request.error);
            if (statusLabel != null) statusLabel.text = "Error";
            if (locationLabel != null) locationLabel.text = "Unknown";
            yield break;
        }

        string json = request.downloadHandler.text;
        Debug.Log("Weather JSON: " + json);

        if (json.Contains("\"cod\":401"))
        {
            Debug.LogError("Invalid API Key!");
            if (statusLabel != null) statusLabel.text = "API Key Error";
            yield break;
        }

        var weatherData = JsonUtility.FromJson<WeatherResponse>(json);

        if (weatherData == null || weatherData.main == null || weatherData.weather == null || weatherData.weather.Length == 0)
        {
            Debug.LogError("Failed to parse weather data");
            if (statusLabel != null) statusLabel.text = "Parse Error";
            yield break;
        }

        // Update labels
        if (locationLabel != null) locationLabel.text = string.IsNullOrEmpty(weatherData.name) ? fallbackCity : weatherData.name;
        if (degreeLabel != null) degreeLabel.text = Mathf.RoundToInt(weatherData.main.temp).ToString();
        if (celsiusLabel != null) celsiusLabel.text = "°C";
        if (statusLabel != null) statusLabel.text = weatherData.weather[0].description;
    }

    // -----------------------------
    // JSON STRUCTS
    // -----------------------------
    [Serializable]
    public class WeatherResponse
    {
        public string name;
        public Weather[] weather;
        public Main main;
    }

    [Serializable]
    public class Weather
    {
        public string description;
        public string icon;
    }

    [Serializable]
    public class Main
    {
        public float temp;
    }
}