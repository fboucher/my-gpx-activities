using System.Globalization;
using System.Text;
using System.Text.Json;
using Dapper;
using my_gpx_activities.ApiService.Models;
using my_gpx_activities.ApiService.Models.Strava;
using Npgsql;

namespace my_gpx_activities.ApiService.Services;

public interface IStravaImportService
{
    Task<ImportResult> ImportAsync(JsonElement activity, JsonElement? streams, NpgsqlConnection conn);
}

public class StravaImportService : IStravaImportService
{
    private readonly ILogger<StravaImportService> _logger;

    private static readonly Dictionary<string, string> SportTypeMapping = new()
    {
        ["NordicSki"] = "Nordic Ski",
        ["VirtualRide"] = "Virtual Ride",
        ["Ride"] = "Cycling",
        ["Run"] = "Running",
        ["Walk"] = "Walking",
        ["Hike"] = "Hiking",
        ["Swim"] = "Swimming",
        ["AlpineSki"] = "Alpine Ski",
        ["Workout"] = "Workout",
        ["WeightTraining"] = "Weight Training",
        ["Yoga"] = "Yoga",
        ["RockClimbing"] = "Rock Climbing",
        ["Snowboard"] = "Snowboard"
    };

    public StravaImportService(ILogger<StravaImportService> logger)
    {
        _logger = logger;
    }

    public async Task<ImportResult> ImportAsync(JsonElement activity, JsonElement? streams, NpgsqlConnection conn)
    {
        try
        {
            var stravaId = activity.GetProperty("id").GetInt64();
            var name = activity.GetProperty("name").GetString() ?? "Untitled Activity";
            var sportType = activity.GetProperty("sport_type").GetString() ?? "Unknown";
            var startDateStr = activity.GetProperty("start_date").GetString() ?? throw new InvalidOperationException("Missing start_date");
            var elapsedTime = activity.GetProperty("elapsed_time").GetInt32();
            var distance = activity.TryGetProperty("distance", out var distProp) ? distProp.GetDouble() : 0;
            var totalElevationGain = activity.TryGetProperty("total_elevation_gain", out var elevProp) ? elevProp.GetDouble() : 0;
            var averageSpeed = activity.TryGetProperty("average_speed", out var avgSpeedProp) ? avgSpeedProp.GetDouble() : 0;
            var maxSpeed = activity.TryGetProperty("max_speed", out var maxSpeedProp) ? maxSpeedProp.GetDouble() : 0;

            var startDate = DateTime.Parse(startDateStr, null, DateTimeStyles.RoundtripKind);
            var endDate = startDate.AddSeconds(elapsedTime);

            var activityType = MapSportType(sportType);

            // Check for duplicate
            var existingActivity = await conn.QueryFirstOrDefaultAsync<Guid?>(
                "SELECT id FROM activities WHERE title = @Title AND start_date_time = @StartDate",
                new { Title = name, StartDate = startDate });

            if (existingActivity.HasValue)
            {
                await LogImportError(conn, "strava", stravaId.ToString(), $"Duplicate activity: {name} at {startDate}");
                return ImportResult.Duplicate($"Activity already exists: {name} at {startDate}");
            }

            // Build track data
            var trackData = await BuildTrackDataAsync(activity, streams, startDate);

            // Build track coordinates (simplified lat/lon only)
            var trackCoordinates = trackData.Select(tp => new[] { tp[0], tp[1] }).ToList();

            // Create activity
            var activityId = Guid.NewGuid();
            var trackDataJson = JsonSerializer.Serialize(trackData);
            var trackCoordinatesJson = JsonSerializer.Serialize(trackCoordinates);

            await conn.ExecuteAsync("""
                INSERT INTO activities (
                    id, title, start_date_time, end_date_time, activity_type, 
                    distance_meters, elevation_gain_meters, elevation_loss_meters,
                    average_speed_ms, max_speed_ms, track_point_count,
                    track_coordinates_json, track_data_json, created_at
                ) VALUES (
                    @Id, @Title, @StartDateTime, @EndDateTime, @ActivityType,
                    @DistanceMeters, @ElevationGainMeters, 0,
                    @AverageSpeedMs, @MaxSpeedMs, @TrackPointCount,
                    @TrackCoordinatesJson::jsonb, @TrackDataJson::jsonb, @CreatedAt
                )
                """, new
            {
                Id = activityId,
                Title = name,
                StartDateTime = startDate,
                EndDateTime = endDate,
                ActivityType = activityType,
                DistanceMeters = distance,
                ElevationGainMeters = totalElevationGain,
                AverageSpeedMs = averageSpeed,
                MaxSpeedMs = maxSpeed,
                TrackPointCount = trackData.Count,
                TrackCoordinatesJson = trackCoordinatesJson,
                TrackDataJson = trackDataJson,
                CreatedAt = DateTime.UtcNow
            });

            _logger.LogInformation("Successfully imported Strava activity {StravaId} as {ActivityId}", stravaId, activityId);
            return ImportResult.Success(activityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing Strava activity");
            throw;
        }
    }

    private async Task<List<double?[]>> BuildTrackDataAsync(JsonElement activity, JsonElement? streams, DateTime startDate)
    {
        if (streams.HasValue && streams.Value.ValueKind != JsonValueKind.Undefined && streams.Value.ValueKind != JsonValueKind.Null)
        {
            return BuildTrackDataFromStreams(streams.Value, startDate);
        }
        else if (activity.TryGetProperty("map", out var mapProp) && mapProp.TryGetProperty("polyline", out var polylineProp))
        {
            var polyline = polylineProp.GetString();
            if (!string.IsNullOrEmpty(polyline))
            {
                return BuildTrackDataFromPolyline(polyline);
            }
        }

        return new List<double?[]>();
    }

    private List<double?[]> BuildTrackDataFromStreams(JsonElement streams, DateTime startDate)
    {
        // Extract latlng stream
        if (!streams.TryGetProperty("latlng", out var latlngStream) ||
            !latlngStream.TryGetProperty("data", out var latlngData))
        {
            return new List<double?[]>();
        }

        var latlngArray = latlngData.EnumerateArray().ToList();
        var count = latlngArray.Count;

        // Extract other streams (optional)
        var altitudeData = TryGetStreamData(streams, "altitude");
        var heartrateData = TryGetStreamData(streams, "heartrate");
        var cadenceData = TryGetStreamData(streams, "cadence");
        var timeData = TryGetStreamData(streams, "time");

        var trackData = new List<double?[]>();

        for (int i = 0; i < count; i++)
        {
            var latlng = latlngArray[i];
            if (latlng.GetArrayLength() < 2)
                continue;

            var lat = latlng[0].GetDouble();
            var lon = latlng[1].GetDouble();
            var elevation = altitudeData != null && i < altitudeData.Count ? (double?)altitudeData[i].GetDouble() : null;
            var heartrate = heartrateData != null && i < heartrateData.Count ? (double?)heartrateData[i].GetInt32() : null;
            var cadence = cadenceData != null && i < cadenceData.Count ? (double?)cadenceData[i].GetInt32() : null;

            double? unixMs = null;
            if (timeData != null && i < timeData.Count)
            {
                var offsetSeconds = timeData[i].GetInt32();
                var timestamp = startDate.AddSeconds(offsetSeconds);
                unixMs = new DateTimeOffset(timestamp).ToUnixTimeMilliseconds();
            }

            trackData.Add(new double?[] { lat, lon, elevation, heartrate, unixMs, cadence });
        }

        return trackData;
    }

    private List<JsonElement>? TryGetStreamData(JsonElement streams, string key)
    {
        if (streams.TryGetProperty(key, out var stream) &&
            stream.TryGetProperty("data", out var data))
        {
            return data.EnumerateArray().ToList();
        }
        return null;
    }

    private List<double?[]> BuildTrackDataFromPolyline(string polyline)
    {
        var points = DecodePolyline(polyline);
        return points.Select(p => new double?[] { p.Latitude, p.Longitude, null, null, null, null }).ToList();
    }

    private static List<(double Latitude, double Longitude)> DecodePolyline(string encoded)
    {
        var points = new List<(double, double)>();
        int index = 0;
        int lat = 0;
        int lng = 0;

        while (index < encoded.Length)
        {
            int shift = 0;
            int result = 0;
            int b;
            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);

            int dlat = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            lat += dlat;

            shift = 0;
            result = 0;
            do
            {
                b = encoded[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);

            int dlng = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            lng += dlng;

            points.Add((lat / 1e5, lng / 1e5));
        }

        return points;
    }

    private string MapSportType(string stravaSportType)
    {
        if (SportTypeMapping.TryGetValue(stravaSportType, out var mapped))
        {
            return mapped;
        }

        // Fallback: convert PascalCase/camelCase to space-separated
        return System.Text.RegularExpressions.Regex.Replace(stravaSportType, "([a-z])([A-Z])", "$1 $2");
    }

    private async Task LogImportError(NpgsqlConnection conn, string source, string externalId, string message)
    {
        await conn.ExecuteAsync("""
            INSERT INTO import_errors (source, external_id, message, created_at)
            VALUES (@Source, @ExternalId, @Message, @CreatedAt)
            """, new
        {
            Source = source,
            ExternalId = externalId,
            Message = message,
            CreatedAt = DateTime.UtcNow
        });
    }
}
