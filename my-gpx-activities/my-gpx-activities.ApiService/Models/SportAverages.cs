namespace my_gpx_activities.ApiService.Models;

public record SportAverages(
    string SportType,
    double GlobalAvgSpeedMs,
    double GlobalMaxSpeedMs,
    int ActivityCount
)
{
    public double GlobalAvgSpeedKmh => GlobalAvgSpeedMs * 3.6;
    public double GlobalMaxSpeedKmh => GlobalMaxSpeedMs * 3.6;
}
