using System.Text.Json;

namespace my_gpx_activities.ApiService.Models.Strava;

public record StravaImportRequest(
    JsonElement Activity,
    JsonElement? Streams
);
