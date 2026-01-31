namespace webapp.Services;

public class ActivityStore
{
    private readonly List<ActivitySummary> _activities = new();

    public IReadOnlyList<ActivitySummary> Activities => _activities.AsReadOnly();

    public void Add(ActivitySummary activity)
    {
        _activities.Add(activity);
    }

    public ActivitySummary? GetById(Guid id)
    {
        return _activities.FirstOrDefault(a => a.Id == id);
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
}