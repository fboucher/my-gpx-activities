using System.Net.Http.Json;

namespace webapp.Services;

public class ActivityApiClient(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _client = httpClientFactory.CreateClient("ApiService");

    public record ActivityTypeDto(string Name, string Icon);

    public record MergePreviewDto(
        Guid ActivityAId,
        Guid ActivityBId,
        string SuggestedMode,
        string SuggestedName,
        List<string> ActivityAChannels,
        List<string> ActivityBChannels,
        string ActivityASportType,
        string ActivityBSportType);

    public record MergeRequest(Guid ActivityAId, Guid ActivityBId, string Mode, string SportType, string Name);

    public record SportStatisticsDto(
        string SportName,
        string? Icon,
        string? Color,
        int TotalActivities,
        double TotalDistanceMeters,
        double TotalDurationSeconds,
        double AverageSpeedMs,
        double MaxSpeedMs,
        double MaxDurationSeconds,
        double TotalElevationGainMeters);

    private record UpdateActivityRequest(string? Title, string? ActivityType);
    private record MergeResponse(Guid Id);

    public async Task<List<ActivitySummary>> GetAllActivitiesAsync(CancellationToken cancellationToken = default)
    {
        var activities = await _client.GetFromJsonAsync<List<ActivitySummary>>("/api/activities", cancellationToken);
        return activities ?? [];
    }

    public async Task<ActivitySummary?> GetActivityByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.GetFromJsonAsync<ActivitySummary>($"/api/activities/{id}", cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<ActivityTypeDto>> GetActivityTypesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _client.GetFromJsonAsync<List<ActivityTypeDto>>("/api/activity-types", cancellationToken);
        return result ?? [];
    }

    public async Task<ActivitySummary?> UpdateActivityAsync(Guid id, string? title, string? activityType, CancellationToken cancellationToken = default)
    {
        var request = new UpdateActivityRequest(title, activityType);
        var response = await _client.PatchAsJsonAsync($"/api/activities/{id}", request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ActivitySummary>(cancellationToken);
    }

    public async Task<MergePreviewDto?> GetMergePreviewAsync(Guid activityAId, Guid activityBId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.GetFromJsonAsync<MergePreviewDto>(
                $"/api/activities/merge/preview?activityAId={activityAId}&activityBId={activityBId}",
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<Guid?> MergeActivitiesAsync(MergeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync("/api/activities/merge", request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var result = await response.Content.ReadFromJsonAsync<MergeResponse>(cancellationToken);
        return result?.Id;
    }

    public async Task<bool> DeleteActivityAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _client.DeleteAsync($"/api/activities/{id}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<SportStatisticsDto>> GetStatisticsBySportAsync(CancellationToken cancellationToken = default)
    {
        var result = await _client.GetFromJsonAsync<List<SportStatisticsDto>>("/api/statistics/by-sport", cancellationToken);
        return result ?? [];
    }
}
