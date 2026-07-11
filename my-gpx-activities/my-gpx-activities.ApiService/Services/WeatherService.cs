using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using my_gpx_activities.ApiService.Models;

namespace my_gpx_activities.ApiService.Services;

public interface IWeatherService
{
    Task<WeatherRecord?> GetWeatherAsync(double latitude, double longitude, DateTime timestamp, CancellationToken cancellationToken = default);
}

public class OpenMeteoWeatherService(HttpClient httpClient, ILogger<OpenMeteoWeatherService>? logger = null) : IWeatherService
{
    private static readonly Dictionary<int, string> WeatherCodes = new()
    {
        [0] = "Clear sky",
        [1] = "Mainly clear",
        [2] = "Partly cloudy",
        [3] = "Overcast",
        [45] = "Foggy",
        [48] = "Depositing rime fog",
        [51] = "Light drizzle",
        [53] = "Moderate drizzle",
        [55] = "Dense drizzle",
        [56] = "Light freezing drizzle",
        [57] = "Dense freezing drizzle",
        [61] = "Slight rain",
        [63] = "Moderate rain",
        [65] = "Heavy rain",
        [66] = "Light freezing rain",
        [67] = "Heavy freezing rain",
        [71] = "Slight snow",
        [73] = "Moderate snow",
        [75] = "Heavy snow",
        [77] = "Snow grains",
        [80] = "Slight rain showers",
        [81] = "Moderate rain showers",
        [82] = "Violent rain showers",
        [85] = "Slight snow showers",
        [86] = "Heavy snow showers",
        [95] = "Thunderstorm",
        [96] = "Thunderstorm with slight hail",
        [99] = "Thunderstorm with heavy hail",
    };

    private static string WindDirectionText(double degrees) => degrees switch
    {
        >= 337.5 or < 22.5 => "N",
        >= 22.5 and < 67.5 => "NE",
        >= 67.5 and < 112.5 => "E",
        >= 112.5 and < 157.5 => "SE",
        >= 157.5 and < 202.5 => "S",
        >= 202.5 and < 247.5 => "SW",
        >= 247.5 and < 292.5 => "W",
        _ => "NW",
    };

    public async Task<WeatherRecord?> GetWeatherAsync(double latitude, double longitude, DateTime timestamp, CancellationToken cancellationToken = default)
    {
        var date = timestamp.ToString("yyyy-MM-dd");
        try
        {
            var url = $"https://archive-api.open-meteo.com/v1/archive?" +
                      $"latitude={latitude:F4}&longitude={longitude:F4}" +
                      $"&start_date={date}&end_date={date}" +
                      $"&hourly=temperature_2m,relative_humidity_2m,wind_speed_10m,wind_direction_10m,weather_code,visibility" +
                      $"&timezone=UTC";

            var response = await httpClient.GetFromJsonAsync<OpenMeteoResponse>(url, cancellationToken);
            if (response?.Hourly == null) return null;

            var hour = timestamp.Hour;
            var times = response.Hourly.Time;
            var index = -1;
            for (int i = 0; i < (times?.Length ?? 0); i++)
            {
                DateTime parsed;
                if (DateTime.TryParse(times![i], out parsed) && parsed.Hour == hour)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0 || index >= (response.Hourly.Temperature2M?.Length ?? 0))
                return null;

            var weatherCode = (int)(response.Hourly.WeatherCode?[index] ?? 0);
            var conditionText = WeatherCodes.GetValueOrDefault(weatherCode, "Unknown");
            var windDegrees = response.Hourly.WindDirection10M?[index] ?? 0;

            return new WeatherRecord
            {
                TemperatureCelsius = response.Hourly.Temperature2M?[index] ?? 0,
                WeatherCode = weatherCode,
                ConditionText = conditionText,
                WindSpeedKmh = response.Hourly.WindSpeed10M?[index] ?? 0,
                WindDirectionDegrees = windDegrees,
                WindDirectionText = WindDirectionText(windDegrees),
                HumidityPercent = response.Hourly.RelativeHumidity2M?[index] ?? 0,
                VisibilityKm = (response.Hourly.Visibility?[index] ?? 0) / 1000.0,
            };
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to fetch weather data from Open-Meteo for latitude={Latitude}, longitude={Longitude}, date={Date}", latitude, longitude, date);
            return null;
        }
    }

    private class OpenMeteoResponse
    {
        [JsonPropertyName("hourly")]
        public OpenMeteoHourly? Hourly { get; set; }
    }

    private class OpenMeteoHourly
    {
        [JsonPropertyName("time")]
        public string[]? Time { get; set; }

        [JsonPropertyName("temperature_2m")]
        public double?[]? Temperature2M { get; set; }

        [JsonPropertyName("relative_humidity_2m")]
        public double?[]? RelativeHumidity2M { get; set; }

        [JsonPropertyName("wind_speed_10m")]
        public double?[]? WindSpeed10M { get; set; }

        [JsonPropertyName("wind_direction_10m")]
        public double?[]? WindDirection10M { get; set; }

        [JsonPropertyName("weather_code")]
        public double?[]? WeatherCode { get; set; }

        [JsonPropertyName("visibility")]
        public double?[]? Visibility { get; set; }
    }
}
