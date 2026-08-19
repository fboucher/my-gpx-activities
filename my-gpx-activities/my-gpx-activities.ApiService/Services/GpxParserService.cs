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
    private readonly IActivityMetricsService _metricsService;

    public GpxParserService(IActivityMetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    public async Task<GpxActivityData> ParseGpxAsync(Stream gpxStream)
    {
        using var reader = new StreamReader(gpxStream);
        var gpxContent = await reader.ReadToEndAsync();

        var doc = XDocument.Parse(gpxContent);

        var title = doc.Root?.Element(XName.Get("trk", "http://www.topografix.com/GPX/1/1"))
                          ?.Element(XName.Get("name", "http://www.topografix.com/GPX/1/1"))?.Value
                   ?? "Untitled Activity";

        var activityType = doc.Root?.Element(XName.Get("trk", "http://www.topografix.com/GPX/1/1"))
                             ?.Element(XName.Get("type", "http://www.topografix.com/GPX/1/1"))?.Value
                        ?? "Unknown";

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
            return null;
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

        var metrics = _metricsService.CalculateMetrics(
            validPoints.Select(p => p.Latitude).ToList(),
            validPoints.Select(p => p.Longitude).ToList(),
            validPoints.Select(p => p.Elevation).ToList(),
            validPoints.Select(p => p.Time!.Value).ToList());

        return new GpxActivityData
        {
            Title = title,
            StartDateTime = startTime,
            EndDateTime = endTime,
            ActivityType = activityType,
            DistanceMeters = metrics.DistanceMeters,
            ElevationGainMeters = metrics.ElevationGainMeters,
            ElevationLossMeters = metrics.ElevationLossMeters,
            AverageSpeedMs = metrics.AverageSpeedMs,
            MaxSpeedMs = metrics.MaxSpeedMs,
            TrackPoints = trackPoints
        };
    }
}