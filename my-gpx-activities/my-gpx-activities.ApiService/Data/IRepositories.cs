using my_gpx_activities.ApiService.Models;

namespace my_gpx_activities.ApiService.Data;

public interface IActivityRepository
{
    Task<IEnumerable<Activity>> GetAllActivitiesAsync();
    Task<Activity?> GetActivityByIdAsync(Guid id);
    Task<Guid> CreateActivityAsync(Activity activity);
    Task<bool> UpdateActivityAsync(Activity activity);
    Task<Activity?> UpdateActivityPartialAsync(Guid id, string? title, string? activityType);
    Task<bool> DeleteActivityAsync(Guid id);
    Task<IEnumerable<SportStatistics>> GetStatisticsBySportAsync();
    Task<IEnumerable<HeatMapActivity>> GetActivitiesForHeatMapAsync(DateOnly? from, DateOnly? to, string[]? sportTypes);
    Task<GlobalStatistics> GetGlobalStatisticsAsync();

    // Weather
    Task UpdateWeatherDataAsync(Guid activityId, string? weatherDataJson);

    // Best segments
    Task SaveBestSegmentsAsync(IEnumerable<BestSegment> segments);
    Task<IEnumerable<BestSegment>> GetBestSegmentsByActivityIdAsync(Guid activityId);

    // Records
    Task<IEnumerable<ActivityRecord>> GetAllRecordsAsync(string? activityType = null);
    Task UpsertRecordAsync(ActivityRecord record);
    Task DeleteRecordsForActivityAsync(Guid activityId);
    Task RecalculateRecordsAsync(string? activityType = null);
}

public interface IActivityTypeRepository
{
    Task<IEnumerable<ActivityType>> GetAllActivityTypesAsync();
    Task<ActivityType?> GetActivityTypeByIdAsync(Guid id);
    Task<ActivityType?> GetActivityTypeByNameAsync(string name);
    Task<Guid> CreateActivityTypeAsync(ActivityType activityType);
    Task<bool> DeleteActivityTypeAsync(Guid id);
}
