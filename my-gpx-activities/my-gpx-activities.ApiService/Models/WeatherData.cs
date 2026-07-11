namespace my_gpx_activities.ApiService.Models;

public class WeatherDataResponse
{
    public double TemperatureCelsius { get; set; }
    public int WeatherCode { get; set; }
    public string ConditionText { get; set; } = string.Empty;
    public double WindSpeedKmh { get; set; }
    public double WindDirectionDegrees { get; set; }
    public double HumidityPercent { get; set; }
    public double VisibilityKm { get; set; }
    public string WindDirectionText { get; set; } = string.Empty;
}

public class WeatherRecord
{
    public required double TemperatureCelsius { get; init; }
    public required int WeatherCode { get; init; }
    public required string ConditionText { get; init; }
    public required double WindSpeedKmh { get; init; }
    public required double WindDirectionDegrees { get; init; }
    public required double HumidityPercent { get; init; }
    public required double VisibilityKm { get; init; }
    public string WindDirectionText { get; set; } = string.Empty;
}

public class WeatherSettings
{
    public const string SectionName = "Weather";
    public bool Enabled { get; set; } = true;
}
