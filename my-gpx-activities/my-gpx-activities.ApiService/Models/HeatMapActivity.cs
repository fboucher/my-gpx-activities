namespace my_gpx_activities.ApiService.Models;

public class HeatMapActivity
{
    public Guid ActivityId { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public string SportType { get; set; } = string.Empty;

    // Array of [lat, lon] pairs — optimized for map rendering
    public double[][] TrackPoints { get; set; } = [];
}
