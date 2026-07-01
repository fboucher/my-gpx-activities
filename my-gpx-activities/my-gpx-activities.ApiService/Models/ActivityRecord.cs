namespace my_gpx_activities.ApiService.Models;

public class ActivityRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActivityId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public double Value { get; set; }
    public int? Year { get; set; }
    public DateTime AchievedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class BestSegment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActivityId { get; set; }
    public int DistanceMeters { get; set; }
    public double SpeedMs { get; set; }
    public double TotalTimeSeconds { get; set; }
    public int StartTrackPointIndex { get; set; }
    public int EndTrackPointIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
