using my_gpx_activities.ApiService.Models;

namespace my_gpx_activities.ApiService.Data;

public interface IActivityRepository
{
    Task<IEnumerable<Activity>> GetAllActivitiesAsync();
    Task<Activity?> GetActivityByIdAsync(Guid id);
    Task<Guid> CreateActivityAsync(Activity activity);
    Task<bool> UpdateActivityAsync(Activity activity);
    Task<bool> DeleteActivityAsync(Guid id);
}

public interface IActivityTypeRepository
{
    Task<IEnumerable<ActivityType>> GetAllActivityTypesAsync();
    Task<ActivityType?> GetActivityTypeByIdAsync(Guid id);
    Task<ActivityType?> GetActivityTypeByNameAsync(string name);
    Task<Guid> CreateActivityTypeAsync(ActivityType activityType);
    Task<bool> DeleteActivityTypeAsync(Guid id);
}
