using System.Net.Http.Json;

namespace webapp.Services;

public class ActivityApiClient(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _client = httpClientFactory.CreateClient("ApiService");

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
}
