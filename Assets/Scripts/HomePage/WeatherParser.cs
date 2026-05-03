using System;
using UnityEngine;

public struct WeatherResult
{
    public bool Success;
    public string ErrorMessage;
    public string CityName;
    public int Temperature;
    public string Description;
}

public static class WeatherParser
{
    [Serializable]
    private class WeatherResponse
    {
        public string name;
        public Weather[] weather;
        public Main main;
        public int cod;
        public string message;
    }

    [Serializable]
    private class Weather { public string description; }

    [Serializable]
    private class Main { public float temp; }

    public static WeatherResult Parse(string json, bool isMetric = true)
    {
        if (string.IsNullOrEmpty(json))
            return new WeatherResult { Success = false, ErrorMessage = "Empty response" };

        try
        {
            var data = JsonUtility.FromJson<WeatherResponse>(json);
            
            if (data == null)
                return new WeatherResult { Success = false, ErrorMessage = "Parse error" };

            // cod can be int or string in some APIs, but JsonUtility needs a match.
            // OpenWeatherMap returns 200 as int.
            if (data.cod != 200)
            {
                string msg = !string.IsNullOrEmpty(data.message)
                    ? data.message
                    : $"API Error (cod={data.cod})";
                return new WeatherResult { Success = false, ErrorMessage = msg };
            }

            if (data.main == null || data.weather == null || data.weather.Length == 0)
                return new WeatherResult { Success = false, ErrorMessage = "Missing data fields" };

            float temp = data.main.temp;
            // Basic heuristic: if it's over 100 and we didn't ask for metric, it's likely Kelvin
            // Or better: follow the user's prompt to "convert Kelvin to Celsius if requested"
            // If the code doesn't use &units=metric, Kelvin is default.
            // Here we'll assume Kelvin if not metric.
            if (!isMetric)
            {
                temp -= 273.15f;
            }

            return new WeatherResult
            {
                Success = true,
                CityName = data.name,
                Temperature = Mathf.RoundToInt(temp),
                Description = data.weather[0].description
            };
        }
        catch (Exception ex)
        {
            return new WeatherResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}
