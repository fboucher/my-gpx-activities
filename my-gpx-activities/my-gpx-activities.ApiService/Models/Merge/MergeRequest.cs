namespace my_gpx_activities.ApiService.Models.Merge;

public record MergeRequest(
    Guid ActivityAId,
    Guid ActivityBId,
    string Mode,
    string SportType,
    string Name
);
