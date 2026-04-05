namespace my_gpx_activities.ApiService.Models.Strava;

public record ImportResult
{
    public bool IsSuccess { get; init; }
    public Guid? ActivityId { get; init; }
    public string? Message { get; init; }

    public static ImportResult Success(Guid activityId) => new()
    {
        IsSuccess = true,
        ActivityId = activityId
    };

    public static ImportResult Duplicate(string message) => new()
    {
        IsSuccess = false,
        Message = message
    };
}
