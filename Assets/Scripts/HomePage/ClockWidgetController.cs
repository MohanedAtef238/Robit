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

    private IClockLogic clockLogic = new ClockLogic();
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
        var angles = clockLogic.CalculateAngles(DateTime.Now);

        currentMinuteAngle = Mathf.LerpAngle(currentMinuteAngle, angles.minuteAngle, Time.deltaTime * 8f);
        currentHourAngle = Mathf.LerpAngle(currentHourAngle, angles.hourAngle, Time.deltaTime * 8f);

        minuteHand.style.rotate = new Rotate(new Angle(currentMinuteAngle));
        hourHand.style.rotate = new Rotate(new Angle(currentHourAngle));
    }

    private void UpdateDigitalClock()
    {
        DateTime now = DateTime.Now;
        if (digitalClockLabel != null)
            digitalClockLabel.text = clockLogic.GetDigitalTime(now);
        if (digitalClockM != null)
            digitalClockM.text = clockLogic.GetAmPm(now);
    }

    // ── Weather ────────────────────────────────────────────────────────────
    private IEnumerator GetWeather(string city)
    {
        string url = $"https://api.openweathermap.org/data/2.5/weather?q={UnityWebRequest.EscapeURL(city)}&appid={WEATHER_API_KEY}&units=metric";

        using var request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            RobitLogger.LogWarning($"[ClockWidget] Weather request failed: {request.error}");
            if (statusLabel != null) statusLabel.text = "Error";
            if (locationLabel != null) locationLabel.text = fallbackCity;
            yield break;
        }

        var result = WeatherParser.Parse(request.downloadHandler.text, isMetric: true);
        
        if (!result.Success)
        {
            RobitLogger.LogWarning($"[ClockWidget] Weather error: {result.ErrorMessage}");
            if (statusLabel != null) statusLabel.text = result.ErrorMessage;
            yield break;
        }

        if (locationLabel != null) locationLabel.text = result.CityName;
        if (degreeLabel != null) degreeLabel.text = result.Temperature.ToString();
        if (celsiusLabel != null) celsiusLabel.text = "°C";
        if (statusLabel != null) statusLabel.text = result.Description;
    }
}

