using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

/// Controls the analog/digital clock and weather display on the Home Page.
/// Ported from DesktopWidget (weatherWidget.cs) to work with the overlay system.
public class ClockWidgetController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private string fallbackCity = "Cairo";

    private VisualElement minuteHand;
    private VisualElement hourHand;
    private Label digitalClockLabel;
    private Label digitalClockM;
    private Label locationLabel;
    private Label degreeLabel;
    private Label celsiusLabel;
    private Label statusLabel;

    private float currentMinuteAngle;
    private float currentHourAngle;

    private const string WEATHER_API_KEY = "5df624a0199e71b2c8377b7bff309ae0";

    public void Initialize(VisualElement root)
    {
        minuteHand = root.Q("minuteHand");
        hourHand = root.Q("hourHand");
        digitalClockLabel = root.Q<Label>("digitalClockLabel");
        digitalClockM = root.Q<Label>("digitalClockM");
        locationLabel = root.Q<Label>("locationLabel");
        degreeLabel = root.Q<Label>("degreeLabel");
        celsiusLabel = root.Q<Label>("celsiusLabel");
        statusLabel = root.Q<Label>("statusLabel");

        StartCoroutine(GetWeather(fallbackCity));
    }

    void Update()
    {
        if (minuteHand == null) return;
        UpdateClockSmooth();
        UpdateDigitalClock();
    }

    // ── Analog Clock ───────────────────────────────────────────────────────
    private void UpdateClockSmooth()
    {
        DateTime now = DateTime.Now;
        float minute = now.Minute + now.Second / 60f;
        float hour = (now.Hour % 12) + minute / 60f;

        float targetMinuteAngle = minute * 6f;
        float targetHourAngle = hour * 30f;

        currentMinuteAngle = Mathf.LerpAngle(currentMinuteAngle, targetMinuteAngle, Time.deltaTime * 8f);
        currentHourAngle = Mathf.LerpAngle(currentHourAngle, targetHourAngle, Time.deltaTime * 8f);

        minuteHand.style.rotate = new Rotate(new Angle(currentMinuteAngle));
        hourHand.style.rotate = new Rotate(new Angle(currentHourAngle));
    }

    private void UpdateDigitalClock()
    {
        DateTime now = DateTime.Now;
        string hour = now.Hour % 12 == 0 ? "12" : (now.Hour % 12).ToString("00");
        string minute = now.Minute.ToString("00");
        string amPm = now.Hour >= 12 ? "PM" : "AM";

        if (digitalClockLabel != null)
            digitalClockLabel.text = $"{hour}:{minute}";
        if (digitalClockM != null)
            digitalClockM.text = amPm;
    }

    // ── Weather ────────────────────────────────────────────────────────────
    private IEnumerator GetWeather(string city)
    {
        string url = $"https://api.openweathermap.org/data/2.5/weather?q={UnityWebRequest.EscapeURL(city)}&appid={WEATHER_API_KEY}&units=metric";

        using var request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[ClockWidget] Weather request failed: {request.error}");
            if (statusLabel != null) statusLabel.text = "Error";
            if (locationLabel != null) locationLabel.text = fallbackCity;
            yield break;
        }

        string json = request.downloadHandler.text;

        if (json.Contains("\"cod\":401"))
        {
            Debug.LogWarning("[ClockWidget] Invalid weather API key.");
            if (statusLabel != null) statusLabel.text = "API Key Error";
            yield break;
        }

        var data = JsonUtility.FromJson<WeatherResponse>(json);
        if (data?.main == null || data.weather == null || data.weather.Length == 0)
        {
            Debug.LogWarning("[ClockWidget] Failed to parse weather data.");
            if (statusLabel != null) statusLabel.text = "Parse Error";
            yield break;
        }

        if (locationLabel != null) locationLabel.text = string.IsNullOrEmpty(data.name) ? fallbackCity : data.name;
        if (degreeLabel != null) degreeLabel.text = Mathf.RoundToInt(data.main.temp).ToString();
        if (celsiusLabel != null) celsiusLabel.text = "°C";
        if (statusLabel != null) statusLabel.text = data.weather[0].description;
    }

    // ── JSON DTOs ──────────────────────────────────────────────────────────
    [Serializable] public class WeatherResponse { public string name; public Weather[] weather; public Main main; }
    [Serializable] public class Weather { public string description; public string icon; }
    [Serializable] public class Main { public float temp; }
}
