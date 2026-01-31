namespace my_gpx_activities.ApiService.Models;

public record SportStatistics(
    string SportName,
    string? Icon,
    string? Color,
    int TotalActivities,
    double TotalDistanceMeters,
    double TotalDurationSeconds,
    double AverageSpeedMs,
    double MaxSpeedMs,
    double MaxDurationSeconds,
    double TotalElevationGainMeters
)
{
    public double TotalDistanceKm => TotalDistanceMeters / 1000.0;
    public double AverageSpeedKmh => AverageSpeedMs * 3.6;
    public double MaxSpeedKmh => MaxSpeedMs * 3.6;
    public TimeSpan TotalDuration => TimeSpan.FromSeconds(TotalDurationSeconds);
    public TimeSpan MaxDuration => TimeSpan.FromSeconds(MaxDurationSeconds);
}
