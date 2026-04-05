namespace my_gpx_activities.ApiService.Models.Merge;

public record MergePreviewResponse(
    Guid ActivityAId,
    Guid ActivityBId,
    string SuggestedMode,
    string SuggestedName,
    string[] ActivityAChannels,
    string[] ActivityBChannels,
    string ActivityASportType,
    string ActivityBSportType
);
