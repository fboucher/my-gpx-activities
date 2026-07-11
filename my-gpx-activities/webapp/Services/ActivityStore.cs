namespace webapp.Services;

public class ActivityStore
{
    private readonly List<ActivitySummary> _activities = new();

    public IReadOnlyList<ActivitySummary> Activities => _activities.AsReadOnly();

    public void Add(ActivitySummary activity)
    {
        _activities.Add(activity);
    }

    public void AddOrUpdate(ActivitySummary activity)
    {
        var existing = GetById(activity.Id);
        if (existing != null)
        {
            _activities.Remove(existing);
        }
        _activities.Add(activity);
    }

    public ActivitySummary? GetById(Guid id)
    {
        return _activities.FirstOrDefault(a => a.Id == id);
    }

    public void Update(Guid id, string? title, string? activityType)
    {
        var activity = GetById(id);
        if (activity != null)
        {
            if (title != null)
                activity.Title = title;
            if (activityType != null)
                activity.ActivityType = activityType;
        }
    }

    public void Remove(Guid id)
    {
        var activity = GetById(id);
        if (activity != null)
            _activities.Remove(activity);
    }

    public void Clear()
    {
        _activities.Clear();
    }
}

public class ActivitySummary
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public double DistanceMeters { get; set; }
    public double ElevationGainMeters { get; set; }
    public double ElevationLossMeters { get; set; }
    public double AverageSpeedMs { get; set; }
    public double MaxSpeedMs { get; set; }
    public int TrackPoints { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<double[]>? TrackCoordinates { get; set; }
    public List<double?[]>? TrackData { get; set; }
    public double? AverageHeartRate { get; set; }
    public double? MaxHeartRate { get; set; }
    public double? Calories { get; set; }
    public WeatherRecordDto? Weather { get; set; }
}

public class WeatherRecordDto
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

public class BestSegmentDto
{
    public string Id { get; set; } = string.Empty;
    public string ActivityId { get; set; } = string.Empty;
    public int DistanceMeters { get; set; }
    public double SpeedMs { get; set; }
    public double TotalTimeSeconds { get; set; }
    public int StartTrackPointIndex { get; set; }
    public int EndTrackPointIndex { get; set; }
}

public class ActivityRecordDto
{
    public string Id { get; set; } = string.Empty;
    public string ActivityId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public double Value { get; set; }
    public int? Year { get; set; }
    public DateTime AchievedAt { get; set; }
}