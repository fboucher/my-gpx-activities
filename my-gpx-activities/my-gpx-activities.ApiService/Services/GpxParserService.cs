using System.Xml.Linq;

namespace my_gpx_activities.ApiService.Services;

public class GpxActivityData
{
    public string Title { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public double DistanceMeters { get; set; }
    public double ElevationGainMeters { get; set; }
    public double ElevationLossMeters { get; set; }
    public double AverageSpeedMs { get; set; }
    public double MaxSpeedMs { get; set; }
    public List<GpxTrackPoint> TrackPoints { get; set; } = new();
    public string RawGpxData { get; set; } = string.Empty;
}

public class GpxTrackPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Elevation { get; set; }
    public DateTime? Time { get; set; }
    public int? HeartRate { get; set; }
    public int? Cadence { get; set; }
}

public interface IGpxParserService
{
    Task<GpxActivityData> ParseGpxAsync(Stream gpxStream);
}

public class GpxParserService : IGpxParserService
{
    public async Task<GpxActivityData> ParseGpxAsync(Stream gpxStream)
    {
        using var reader = new StreamReader(gpxStream);
        var gpxContent = await reader.ReadToEndAsync();

        var doc = XDocument.Parse(gpxContent);

        // Extract basic metadata
        var metadata = doc.Root?.Element(XName.Get("metadata", "http://www.topografix.com/GPX/1/1"));
        var title = doc.Root?.Element(XName.Get("trk", "http://www.topografix.com/GPX/1/1"))
                          ?.Element(XName.Get("name", "http://www.topografix.com/GPX/1/1"))?.Value
                   ?? "Untitled Activity";

        var activityType = doc.Root?.Element(XName.Get("trk", "http://www.topografix.com/GPX/1/1"))
                             ?.Element(XName.Get("type", "http://www.topografix.com/GPX/1/1"))?.Value
                        ?? "Unknown";

        // Extract track points
        var trackPoints = new List<GpxTrackPoint>();
        var trackSegments = doc.Root?.Elements(XName.Get("trk", "http://www.topografix.com/GPX/1/1"))
                              .SelectMany(trk => trk.Elements(XName.Get("trkseg", "http://www.topografix.com/GPX/1/1")));

        foreach (var segment in trackSegments ?? Enumerable.Empty<XElement>())
        {
            foreach (var trkpt in segment.Elements(XName.Get("trkpt", "http://www.topografix.com/GPX/1/1")))
            {
                var point = ParseTrackPoint(trkpt);
                if (point != null)
                {
                    trackPoints.Add(point);
                }
            }
        }

        // Calculate activity metrics
        var activityData = CalculateActivityMetrics(title, activityType, trackPoints);
        activityData.RawGpxData = gpxContent;

        return activityData;
    }

    private GpxTrackPoint? ParseTrackPoint(XElement trkpt)
    {
        try
        {
            var lat = double.Parse(trkpt.Attribute("lat")?.Value ?? "0");
            var lon = double.Parse(trkpt.Attribute("lon")?.Value ?? "0");

            var elevation = trkpt.Element(XName.Get("ele", "http://www.topografix.com/GPX/1/1"));
            var time = trkpt.Element(XName.Get("time", "http://www.topografix.com/GPX/1/1"));

            // Parse Garmin extensions for heart rate and cadence
            int? heartRate = null;
            int? cadence = null;

            var extensions = trkpt.Element(XName.Get("extensions", "http://www.topografix.com/GPX/1/1"));
            if (extensions != null)
            {
                var trackPointExtension = extensions.Element(XName.Get("TrackPointExtension", "http://www.garmin.com/xmlschemas/TrackPointExtension/v1"));
                if (trackPointExtension != null)
                {
                    var hrElement = trackPointExtension.Element(XName.Get("hr", "http://www.garmin.com/xmlschemas/TrackPointExtension/v1"));
                    if (hrElement != null && int.TryParse(hrElement.Value, out var hr))
                    {
                        heartRate = hr;
                    }

                    var cadElement = trackPointExtension.Element(XName.Get("cad", "http://www.garmin.com/xmlschemas/TrackPointExtension/v1"));
                    if (cadElement != null && int.TryParse(cadElement.Value, out var cad))
                    {
                        cadence = cad;
                    }
                }
            }

            return new GpxTrackPoint
            {
                Latitude = lat,
                Longitude = lon,
                Elevation = elevation != null ? double.Parse(elevation.Value) : null,
                Time = time != null ? DateTime.Parse(time.Value) : null,
                HeartRate = heartRate,
                Cadence = cadence
            };
        }
        catch
        {
            return null; // Skip invalid track points
        }
    }

    private GpxActivityData CalculateActivityMetrics(string title, string activityType, List<GpxTrackPoint> trackPoints)
    {
        if (!trackPoints.Any())
        {
            return new GpxActivityData
            {
                Title = title,
                ActivityType = activityType,
                StartDateTime = DateTime.UtcNow,
                EndDateTime = DateTime.UtcNow
            };
        }

        var validPoints = trackPoints.Where(p => p.Time.HasValue).OrderBy(p => p.Time!.Value).ToList();

        if (!validPoints.Any())
        {
            return new GpxActivityData
            {
                Title = title,
                ActivityType = activityType,
                StartDateTime = DateTime.UtcNow,
                EndDateTime = DateTime.UtcNow,
                TrackPoints = trackPoints
            };
        }

        var startTime = validPoints.First().Time!.Value;
        var endTime = validPoints.Last().Time!.Value;

        // Calculate distance using Haversine formula
        double totalDistance = 0;
        for (int i = 1; i < validPoints.Count; i++)
        {
            var prev = validPoints[i - 1];
            var curr = validPoints[i];
            totalDistance += CalculateDistance(prev.Latitude, prev.Longitude, curr.Latitude, curr.Longitude);
        }

        // Calculate elevation gain/loss
        double elevationGain = 0;
        double elevationLoss = 0;
        for (int i = 1; i < validPoints.Count; i++)
        {
            var prev = validPoints[i - 1];
            var curr = validPoints[i];

            if (prev.Elevation.HasValue && curr.Elevation.HasValue)
            {
                var elevationDiff = curr.Elevation.Value - prev.Elevation.Value;
                if (elevationDiff > 0)
                    elevationGain += elevationDiff;
                else
                    elevationLoss += Math.Abs(elevationDiff);
            }
        }

        // Calculate speeds
        var duration = endTime - startTime;
        var averageSpeed = totalDistance / duration.TotalSeconds; // meters per second
        var maxSpeed = validPoints.Any(p => p.Time.HasValue)
            ? validPoints.Zip(validPoints.Skip(1), (a, b) =>
            {
                if (!a.Time.HasValue || !b.Time.HasValue) return 0.0;
                var distance = CalculateDistance(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
                var timeDiff = (b.Time.Value - a.Time.Value).TotalSeconds;
                return timeDiff > 0 ? distance / timeDiff : 0.0;
            }).Max()
            : 0.0;

        return new GpxActivityData
        {
            Title = title,
            StartDateTime = startTime,
            EndDateTime = endTime,
            ActivityType = activityType,
            DistanceMeters = totalDistance,
            ElevationGainMeters = elevationGain,
            ElevationLossMeters = elevationLoss,
            AverageSpeedMs = averageSpeed,
            MaxSpeedMs = maxSpeed,
            TrackPoints = trackPoints
        };
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // Earth's radius in meters

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }

    private double ToRadians(double degrees) => degrees * Math.PI / 180;
}