using NUnit.Framework;

public class WeatherParserTests
{
    [Test]
    public void Parse_ValidJson_ReturnsSuccess()
    {
        string json = "{\"name\":\"London\",\"main\":{\"temp\":15.5},\"weather\":[{\"description\":\"cloudy\"}],\"cod\":200}";
        var result = WeatherParser.Parse(json);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("London", result.CityName);
        Assert.AreEqual(16, result.Temperature); // Rounded
        Assert.AreEqual("cloudy", result.Description);
    }

    [Test]
    public void Parse_InvalidCod_ReturnsError()
    {
        string json = "{\"cod\":401,\"message\":\"Invalid API Key\"}";
        var result = WeatherParser.Parse(json);

        Assert.IsFalse(result.Success);
        StringAssert.Contains("Invalid API Key", result.ErrorMessage);
    }

    [Test]
    public void Parse_NullOrEmpty_ReturnsError()
    {
        Assert.IsFalse(WeatherParser.Parse(null).Success);
        Assert.IsFalse(WeatherParser.Parse("").Success);
    }

    [Test]
    public void Parse_MalformedJson_ReturnsError()
    {
        var result = WeatherParser.Parse("{ invalid }");
        Assert.IsFalse(result.Success);
    }

    [Test]
    public void Parse_KelvinToCelsius_ConversionWorks()
    {
        // 273.15 K = 0 C
        string json = "{\"name\":\"ColdCity\",\"main\":{\"temp\":273.15},\"weather\":[{\"description\":\"ice\"}],\"cod\":200}";
        var result = WeatherParser.Parse(json, isMetric: false);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.Temperature);
    }

    [Test]
    public void Parse_MissingFields_ReturnsError()
    {
        string json = "{\"name\":\"London\",\"cod\":200}"; // Missing main and weather
        var result = WeatherParser.Parse(json);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Missing data fields", result.ErrorMessage);
    }

    [Test]
    public void Parse_ValidJsonWithMissingCodField_StillSucceeds()
    {
        // Arrange — real OWM responses sometimes omit "cod" entirely
        // JsonUtility will deserialize missing int as 0
        // The parser must handle this — cod=0 should NOT be treated as success
        string json = "{\"name\":\"London\",\"main\":{\"temp\":15.5}," +
                      "\"weather\":[{\"description\":\"clear\"}]}"; // no cod field

        // Act
        var result = WeatherParser.Parse(json);

        // Assert — this will FAIL with current code, proving the cod=0 bug
        Assert.IsFalse(result.Success,
            "A response missing 'cod' must fail — cod=0 from JsonUtility is not a success code.");
    }
}
