using my_gpx_activities.ApiService.Models;

namespace my_gpx_activities.ApiService.Services;

public interface IBestSegmentService
{
    List<BestSegment> ComputeBestSegments(List<double?[]> trackData);
}

public class BestSegmentService : IBestSegmentService
{
    private static readonly int[] SegmentDistances = [1000, 5000, 10000];

    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    public List<BestSegment> ComputeBestSegments(List<double?[]> trackData)
    {
        if (trackData.Count < 2) return [];

        // Build cumulative distance and time arrays
        var points = trackData
            .Select(p => new
            {
                Lat = p.Length > 0 ? p[0] : null,
                Lon = p.Length > 1 ? p[1] : null,
                Time = p.Length > 4 ? p[4] : null
            })
            .ToList();

        // Filter to points with GPS data
        var gpsPoints = points.Where(p => p.Lat.HasValue && p.Lon.HasValue).ToList();
        if (gpsPoints.Count < 2) return [];

        var cumulativeDistances = new double[gpsPoints.Count];
        cumulativeDistances[0] = 0;
        for (int i = 1; i < gpsPoints.Count; i++)
        {
            var prev = gpsPoints[i - 1];
            var curr = gpsPoints[i];
            cumulativeDistances[i] = cumulativeDistances[i - 1] +
                CalculateDistance(prev.Lat!.Value, prev.Lon!.Value, curr.Lat!.Value, curr.Lon!.Value);
        }

        var results = new List<BestSegment>();
        foreach (var targetDistance in SegmentDistances)
        {
            BestSegment? best = null;
            for (int start = 0; start < gpsPoints.Count; start++)
            {
                var end = start;
                while (end < gpsPoints.Count && cumulativeDistances[end] - cumulativeDistances[start] < targetDistance)
                    end++;

                if (end >= gpsPoints.Count) break;

                var segmentDistance = cumulativeDistances[end] - cumulativeDistances[start];

                double? startTime = gpsPoints[start].Time;
                double? endTime = gpsPoints[end].Time;
                if (!startTime.HasValue || !endTime.HasValue) continue;

                var timeSeconds = (endTime.Value - startTime.Value) / 1000.0;
                if (timeSeconds <= 0) continue;

                var speed = segmentDistance / timeSeconds;

                if (best == null || speed > best.SpeedMs)
                {
                    best = new BestSegment
                    {
                        Id = Guid.NewGuid(),
                        DistanceMeters = targetDistance,
                        SpeedMs = speed,
                        TotalTimeSeconds = timeSeconds,
                        StartTrackPointIndex = start,
                        EndTrackPointIndex = end,
                        CreatedAt = DateTime.UtcNow
                    };
                }
            }

            if (best != null)
                results.Add(best);
        }

        return results;
    }
}
