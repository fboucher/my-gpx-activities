using System.Net;
using System.Text;
using my_gpx_activities.ApiService.Services;

namespace my_gpx_activities.Tests;

/// <summary>
/// Unit tests for OpenMeteoWeatherService — verifies weather data parsing,
/// wind direction text mapping, and weather code descriptions
/// without making real HTTP calls.
/// </summary>
[TestFixture]
[Category("Unit")]
public class WeatherServiceTests
{
    // ────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────

    private static HttpClient CreateMockHttpClient(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpClient(new MockHttpMessageHandler(responseJson, status));
    }

    /// <summary>
    /// Builds a 24-entry Open-Meteo hourly response, overriding the specific hour's values.
    /// </summary>
    private static string BuildOpenMeteoResponse(
        int targetHour,
        double temperature,
        double windSpeed,
        double windDirection,
        int weatherCode,
        double humidity = 60.0,
        double visibilityMeters = 10_000.0)
    {
        static string DoubleArray(double[] values) =>
            "[" + string.Join(",", values.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";

        var times = Enumerable.Range(0, 24).Select(h => $"2024-01-15T{h:D2}:00").ToArray();
        var temps = Enumerable.Repeat(20.0, 24).ToArray();
        var windSpeeds = Enumerable.Repeat(10.0, 24).ToArray();
        var windDirs = Enumerable.Repeat(90.0, 24).ToArray();
        var codes = Enumerable.Repeat(0.0, 24).ToArray();
        var humidities = Enumerable.Repeat(humidity, 24).ToArray();
        var visibilities = Enumerable.Repeat(visibilityMeters, 24).ToArray();

        temps[targetHour] = temperature;
        windSpeeds[targetHour] = windSpeed;
        windDirs[targetHour] = windDirection;
        codes[targetHour] = weatherCode;

        var timesJson = "[" + string.Join(",", times.Select(t => $"\"{t}\"")) + "]";

        return $$"""
            {
              "hourly": {
                "time": {{timesJson}},
                "temperature_2m": {{DoubleArray(temps)}},
                "relative_humidity_2m": {{DoubleArray(humidities)}},
                "wind_speed_10m": {{DoubleArray(windSpeeds)}},
                "wind_direction_10m": {{DoubleArray(windDirs)}},
                "weather_code": {{DoubleArray(codes)}},
                "visibility": {{DoubleArray(visibilities)}}
              }
            }
            """;
    }

    // ────────────────────────────────────────────────────────────────
    // Basic response handling
    // ────────────────────────────────────────────────────────────────

    [Test]
    public async Task GetWeatherAsync_WithValidResponse_ReturnsWeatherRecord()
    {
        var json = BuildOpenMeteoResponse(targetHour: 10, temperature: 15.5, windSpeed: 20.0, windDirection: 90.0, weatherCode: 2);
        var service = new OpenMeteoWeatherService(CreateMockHttpClient(json));

        var result = await service.GetWeatherAsync(45.0, 0.0, new DateTime(2024, 1, 15, 10, 30, 0));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TemperatureCelsius, Is.EqualTo(15.5).Within(0.01));
        Assert.That(result.WindSpeedKmh, Is.EqualTo(20.0).Within(0.01));
        Assert.That(result.WindDirectionDegrees, Is.EqualTo(90.0).Within(0.01));
    }

    [Test]
    public async Task GetWeatherAsync_WithHumidityAndVisibility_ReturnsCorrectValues()
    {
        var json = BuildOpenMeteoResponse(
            targetHour: 14, temperature: 22.0, windSpeed: 5.0, windDirection: 180.0,
            weatherCode: 1, humidity: 75.0, visibilityMeters: 8_000.0);
        var service = new OpenMeteoWeatherService(CreateMockHttpClient(json));

        var result = await service.GetWeatherAsync(48.5, 2.3, new DateTime(2024, 1, 15, 14, 0, 0));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.HumidityPercent, Is.EqualTo(75.0).Within(0.01));
        Assert.That(result.VisibilityKm, Is.EqualTo(8.0).Within(0.01));
    }

    [Test]
    public async Task GetWeatherAsync_WithHttpError_ReturnsNull()
    {
        var service = new OpenMeteoWeatherService(CreateMockHttpClient("{}", HttpStatusCode.ServiceUnavailable));

        var result = await service.GetWeatherAsync(45.0, 0.0, DateTime.UtcNow);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetWeatherAsync_WithEmptyJsonResponse_ReturnsNull()
    {
        var service = new OpenMeteoWeatherService(CreateMockHttpClient("{}"));

        var result = await service.GetWeatherAsync(45.0, 0.0, DateTime.UtcNow);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetWeatherAsync_WithMalformedJson_ReturnsNull()
    {
        var service = new OpenMeteoWeatherService(CreateMockHttpClient("not-valid-json"));

        var result = await service.GetWeatherAsync(45.0, 0.0, DateTime.UtcNow);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetWeatherAsync_WithCancellation_ReturnsNull()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var service = new OpenMeteoWeatherService(CreateMockHttpClient("{}"));

        var result = await service.GetWeatherAsync(45.0, 0.0, DateTime.UtcNow, cts.Token);

        Assert.That(result, Is.Null);
    }

    // ────────────────────────────────────────────────────────────────
    // Wind direction text
    // ────────────────────────────────────────────────────────────────

    [TestCase(0.0, "N", Description = "Due North")]
    [TestCase(22.4, "N", Description = "Still North (boundary)")]
    [TestCase(22.5, "NE", Description = "NE starts at 22.5")]
    [TestCase(45.0, "NE", Description = "Mid NE")]
    [TestCase(67.4, "NE", Description = "NE ends just before 67.5")]
    [TestCase(67.5, "E", Description = "E starts at 67.5")]
    [TestCase(90.0, "E", Description = "Due East")]
    [TestCase(112.4, "E", Description = "E ends just before 112.5")]
    [TestCase(112.5, "SE", Description = "SE starts at 112.5")]
    [TestCase(135.0, "SE", Description = "Mid SE")]
    [TestCase(157.4, "SE", Description = "SE ends just before 157.5")]
    [TestCase(157.5, "S", Description = "S starts at 157.5")]
    [TestCase(180.0, "S", Description = "Due South")]
    [TestCase(202.4, "S", Description = "S ends just before 202.5")]
    [TestCase(202.5, "SW", Description = "SW starts at 202.5")]
    [TestCase(225.0, "SW", Description = "Mid SW")]
    [TestCase(247.4, "SW", Description = "SW ends just before 247.5")]
    [TestCase(247.5, "W", Description = "W starts at 247.5")]
    [TestCase(270.0, "W", Description = "Due West")]
    [TestCase(292.4, "W", Description = "W ends just before 292.5")]
    [TestCase(292.5, "NW", Description = "NW starts at 292.5")]
    [TestCase(315.0, "NW", Description = "Mid NW")]
    [TestCase(337.4, "NW", Description = "NW ends just before 337.5")]
    [TestCase(337.5, "N", Description = "Back to N at 337.5")]
    [TestCase(359.9, "N", Description = "Almost full circle, still N")]
    public async Task GetWeatherAsync_WindDirection_ReturnsCorrectCardinalText(double degrees, string expectedText)
    {
        var json = BuildOpenMeteoResponse(targetHour: 8, temperature: 20.0, windSpeed: 10.0, windDirection: degrees, weatherCode: 0);
        var service = new OpenMeteoWeatherService(CreateMockHttpClient(json));

        var result = await service.GetWeatherAsync(45.0, 0.0, new DateTime(2024, 1, 15, 8, 0, 0));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.WindDirectionText, Is.EqualTo(expectedText),
            $"Wind at {degrees}° should map to '{expectedText}'");
    }

    // ────────────────────────────────────────────────────────────────
    // Weather code descriptions
    // ────────────────────────────────────────────────────────────────

    [TestCase(0, "Clear sky")]
    [TestCase(1, "Mainly clear")]
    [TestCase(2, "Partly cloudy")]
    [TestCase(3, "Overcast")]
    [TestCase(45, "Foggy")]
    [TestCase(51, "Light drizzle")]
    [TestCase(61, "Slight rain")]
    [TestCase(63, "Moderate rain")]
    [TestCase(65, "Heavy rain")]
    [TestCase(71, "Slight snow")]
    [TestCase(80, "Slight rain showers")]
    [TestCase(95, "Thunderstorm")]
    [TestCase(99, "Thunderstorm with heavy hail")]
    public async Task GetWeatherAsync_WeatherCode_ReturnsCorrectConditionText(int code, string expectedText)
    {
        var json = BuildOpenMeteoResponse(targetHour: 12, temperature: 15.0, windSpeed: 10.0, windDirection: 90.0, weatherCode: code);
        var service = new OpenMeteoWeatherService(CreateMockHttpClient(json));

        var result = await service.GetWeatherAsync(45.0, 0.0, new DateTime(2024, 1, 15, 12, 0, 0));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ConditionText, Is.EqualTo(expectedText),
            $"Weather code {code} should map to '{expectedText}'");
    }

    [Test]
    public async Task GetWeatherAsync_UnknownWeatherCode_ReturnsUnknown()
    {
        var json = BuildOpenMeteoResponse(targetHour: 6, temperature: 10.0, windSpeed: 5.0, windDirection: 0.0, weatherCode: 999);
        var service = new OpenMeteoWeatherService(CreateMockHttpClient(json));

        var result = await service.GetWeatherAsync(45.0, 0.0, new DateTime(2024, 1, 15, 6, 0, 0));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ConditionText, Is.EqualTo("Unknown"));
    }

    // ────────────────────────────────────────────────────────────────
    // Mock HTTP infrastructure
    // ────────────────────────────────────────────────────────────────

    private sealed class MockHttpMessageHandler(string responseContent, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
