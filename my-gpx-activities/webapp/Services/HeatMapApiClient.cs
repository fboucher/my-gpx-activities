using System.Net.Http.Json;

namespace webapp.Services;

public record HeatMapTrackActivity(
    Guid ActivityId,
    string ActivityName,
    string SportType,
    double[][] TrackPoints);

public class HeatMapApiClient(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _client = httpClientFactory.CreateClient("ApiService");

    public async Task<List<HeatMapTrackActivity>> GetHeatMapActivitiesAsync(
        DateOnly? dateFrom,
        DateOnly? dateTo,
        IEnumerable<string>? sportTypes,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (dateFrom.HasValue)
            query.Add($"dateFrom={dateFrom.Value:yyyy-MM-dd}");
        if (dateTo.HasValue)
            query.Add($"dateTo={dateTo.Value:yyyy-MM-dd}");
        if (sportTypes != null)
        {
            var types = string.Join(",", sportTypes);
            if (!string.IsNullOrWhiteSpace(types))
                query.Add($"sportTypes={Uri.EscapeDataString(types)}");
        }

        var url = "/api/activities/heatmap" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        var result = await _client.GetFromJsonAsync<List<HeatMapTrackActivity>>(url, cancellationToken);
        return result ?? [];
    }

    public async Task<List<string>> GetSportTypesAsync(CancellationToken cancellationToken = default)
    {
        var activities = await _client.GetFromJsonAsync<List<ActivitySummaryDto>>("/api/activities", cancellationToken);
        return activities?
            .Select(a => a.ActivityType)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .Order()
            .ToList() ?? [];
    }

    private record ActivitySummaryDto(string ActivityType);
}
