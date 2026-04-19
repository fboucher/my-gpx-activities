using System.Text.Json;
using my_gpx_activities.ApiService.Data;
using my_gpx_activities.ApiService.Models;
using my_gpx_activities.ApiService.Models.Merge;

namespace my_gpx_activities.ApiService.Services;

public interface IActivityMergeService
{
    Task<MergePreviewResponse> GetMergePreviewAsync(Guid activityAId, Guid activityBId);
    Task<Guid> MergeActivitiesAsync(MergeRequest request);
}

public class ActivityMergeService(IActivityRepository repository, ILogger<ActivityMergeService> logger) : IActivityMergeService
{
    // Channel index → name mapping
    private static readonly (int Index, string Name)[] ChannelDefs =
    [
        (0, "gps"),
        (2, "elevation"),
        (3, "heart_rate"),
        (4, "timestamp"),
        (5, "cadence")
    ];

    public async Task<MergePreviewResponse> GetMergePreviewAsync(Guid activityAId, Guid activityBId)
    {
        var (actA, actB) = await FetchBothAsync(activityAId, activityBId);

        var pointsA = ParseTrackData(actA.TrackDataJson);
        var pointsB = ParseTrackData(actB.TrackDataJson);

        var channelsA = DetectChannels(pointsA);
        var channelsB = DetectChannels(pointsB);

        var suggestedMode = TimeRangesOverlap(actA, actB) ? "merge" : "append";

        var suggestedName = suggestedMode == "append"
            ? $"{actA.Title} + {actB.Title}"
            : $"{actA.Title} (merged)";

        return new MergePreviewResponse(
            activityAId,
            activityBId,
            suggestedMode,
            suggestedName,
            channelsA,
            channelsB,
            actA.ActivityType,
            actB.ActivityType
        );
    }

    public async Task<Guid> MergeActivitiesAsync(MergeRequest request)
    {
        if (request.Mode is not ("merge" or "append"))
            throw new ArgumentException($"Mode must be 'merge' or 'append', got '{request.Mode}'.");

        var (actA, actB) = await FetchBothAsync(request.ActivityAId, request.ActivityBId);

        var pointsA = ParseTrackData(actA.TrackDataJson);
        var pointsB = ParseTrackData(actB.TrackDataJson);

        List<double?[]> mergedPoints;
        Activity baseActivity;

        if (request.Mode == "append")
        {
            (mergedPoints, baseActivity) = BuildAppend(actA, actB, pointsA, pointsB);
        }
        else
        {
            (mergedPoints, baseActivity) = BuildMerge(actA, actB, pointsA, pointsB, request.ChannelSources);
        }

        var trackCoordinates = mergedPoints
            .Where(p => p.Length > 1 && p[0].HasValue && p[1].HasValue)
            .Select(p => new[] { p[0]!.Value, p[1]!.Value })
            .ToList();

        var newActivity = new Activity
        {
            Id = Guid.NewGuid(),
            Title = request.Name,
            ActivityType = request.SportType,
            StartDateTime = baseActivity.StartDateTime,
            EndDateTime = baseActivity.EndDateTime,
            DistanceMeters = baseActivity.DistanceMeters,
            ElevationGainMeters = baseActivity.ElevationGainMeters,
            ElevationLossMeters = baseActivity.ElevationLossMeters,
            AverageSpeedMs = baseActivity.AverageSpeedMs,
            MaxSpeedMs = baseActivity.MaxSpeedMs,
            TrackPointCount = mergedPoints.Count,
            TrackCoordinatesJson = JsonSerializer.Serialize(trackCoordinates),
            TrackDataJson = JsonSerializer.Serialize(mergedPoints),
            CreatedAt = DateTime.UtcNow
        };

        var id = await repository.CreateActivityAsync(newActivity);
        logger.LogInformation("Created merged activity {Id} (mode={Mode}) from {A} + {B}", id, request.Mode, request.ActivityAId, request.ActivityBId);
        return id;
    }

    // ── Append ────────────────────────────────────────────────────────────────

    private static (List<double?[]> points, Activity stats) BuildAppend(
        Activity actA, Activity actB,
        List<double?[]> pointsA, List<double?[]> pointsB)
    {
        // Order chronologically by start time
        bool aFirst = actA.StartDateTime <= actB.StartDateTime;
        var (first, second, ptsFirst, ptsSecond) = aFirst
            ? (actA, actB, pointsA, pointsB)
            : (actB, actA, pointsB, pointsA);

        var merged = new List<double?[]>(ptsFirst.Count + ptsSecond.Count);
        merged.AddRange(ptsFirst);
        merged.AddRange(ptsSecond);

        var stats = new Activity
        {
            StartDateTime = first.StartDateTime,
            EndDateTime = second.EndDateTime,
            DistanceMeters = first.DistanceMeters + second.DistanceMeters,
            ElevationGainMeters = first.ElevationGainMeters + second.ElevationGainMeters,
            ElevationLossMeters = first.ElevationLossMeters + second.ElevationLossMeters,
            AverageSpeedMs = (first.AverageSpeedMs + second.AverageSpeedMs) / 2.0,
            MaxSpeedMs = Math.Max(first.MaxSpeedMs, second.MaxSpeedMs)
        };

        return (merged, stats);
    }

    // ── Merge (overlapping) ───────────────────────────────────────────────────

    private static (List<double?[]> points, Activity stats) BuildMerge(
        Activity actA, Activity actB,
        List<double?[]> pointsA, List<double?[]> pointsB,
        Dictionary<string, string>? userChannelSources)
    {
        var channelNameToIndex = ChannelDefs.ToDictionary(c => c.Name, c => c.Index);

        var channelSources = new Dictionary<int, List<double?[]>>();

        foreach (var def in ChannelDefs)
        {
            List<double?[]> source;
            if (userChannelSources != null && userChannelSources.TryGetValue(def.Name, out var userChoice))
            {
                source = userChoice.ToUpperInvariant() == "B" ? pointsB : pointsA;
            }
            else
            {
                source = CountChannel(pointsA, def.Index) >= CountChannel(pointsB, def.Index) ? pointsA : pointsB;
            }
            channelSources[def.Index] = source;
        }

        // GPS uses both indices 0 and 1 — decide once based on index 0
        var gpsSource = channelSources[0];

        // Use the GPS-source activity as the "spine" — its point count drives the result length
        var spinePoints = gpsSource;

        // Build merged point list: one entry per spine point, filling each channel from its preferred source
        var merged = new List<double?[]>(spinePoints.Count);
        for (int i = 0; i < spinePoints.Count; i++)
        {
            var pt = new double?[6];

            // GPS (0,1) — always from gpsSource spine
            pt[0] = Get(spinePoints, i, 0);
            pt[1] = Get(spinePoints, i, 1);

            // Remaining channels — from their preferred source (aligned by index)
            foreach (var (idx, src) in channelSources)
            {
                if (idx == 0) continue; // already handled as part of GPS pair
                pt[idx] = Get(src, i, idx);
            }

            merged.Add(pt);
        }

        // Stats from whichever activity had more GPS points
        var dominant = gpsSource == pointsA ? actA : actB;
        var other = gpsSource == pointsA ? actB : actA;

        var stats = new Activity
        {
            StartDateTime = dominant.StartDateTime < other.StartDateTime ? dominant.StartDateTime : other.StartDateTime,
            EndDateTime = dominant.EndDateTime > other.EndDateTime ? dominant.EndDateTime : other.EndDateTime,
            DistanceMeters = dominant.DistanceMeters,
            ElevationGainMeters = dominant.ElevationGainMeters,
            ElevationLossMeters = dominant.ElevationLossMeters,
            AverageSpeedMs = dominant.AverageSpeedMs,
            MaxSpeedMs = Math.Max(dominant.MaxSpeedMs, other.MaxSpeedMs)
        };

        return (merged, stats);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(Activity a, Activity b)> FetchBothAsync(Guid idA, Guid idB)
    {
        var (actA, actB) = await (repository.GetActivityByIdAsync(idA), repository.GetActivityByIdAsync(idB))
            .WhenBoth();

        if (actA is null) throw new KeyNotFoundException($"Activity {idA} not found.");
        if (actB is null) throw new KeyNotFoundException($"Activity {idB} not found.");

        return (actA, actB);
    }

    private static List<double?[]> ParseTrackData(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        return JsonSerializer.Deserialize<List<double?[]>>(json) ?? [];
    }

    private static string[] DetectChannels(List<double?[]> points)
    {
        var channels = new List<string>();
        foreach (var (idx, name) in ChannelDefs)
        {
            if (points.Any(p => p.Length > idx && p[idx].HasValue))
                channels.Add(name);
        }
        return [.. channels];
    }

    private static bool TimeRangesOverlap(Activity a, Activity b) =>
        a.StartDateTime < b.EndDateTime && b.StartDateTime < a.EndDateTime;

    private static int CountChannel(List<double?[]> points, int idx) =>
        points.Count(p => p.Length > idx && p[idx].HasValue);

    private static double? Get(List<double?[]> points, int pointIdx, int channelIdx)
    {
        if (pointIdx >= points.Count) return null;
        var pt = points[pointIdx];
        return pt.Length > channelIdx ? pt[channelIdx] : null;
    }
}

// Tiny extension to await two tasks in parallel without Task.WhenAll boxing
file static class TaskExtensions
{
    public static async Task<(T1, T2)> WhenBoth<T1, T2>(this (Task<T1> t1, Task<T2> t2) tasks)
    {
        await Task.WhenAll(tasks.t1, tasks.t2);
        return (tasks.t1.Result, tasks.t2.Result);
    }
}
