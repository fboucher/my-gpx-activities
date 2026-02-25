using System.Text;
using System.Xml.Linq;

namespace my_gpx_activities.ApiService.Services;

public interface ISmartMergeService
{
    /// <summary>
    /// Merges heart-rate data from a FIT file into a GPX file, matching points by nearest timestamp.
    /// </summary>
    /// <param name="gpxStream">GPX file stream containing coordinates and timestamps.</param>
    /// <param name="fitStream">FIT file stream containing heart-rate and timestamps.</param>
    /// <param name="toleranceSeconds">Maximum time difference in seconds to consider a match (default 5s).</param>
    /// <returns>Merged GPX content as a UTF-8 string.</returns>
    Task<string> MergeAsync(Stream gpxStream, Stream fitStream, int toleranceSeconds = 5);
}

public class SmartMergeService : ISmartMergeService
{
    private static readonly XNamespace GpxNs = "http://www.topografix.com/GPX/1/1";
    private static readonly XNamespace TpxNs = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";

    private readonly IFitParserService _fitParser;

    public SmartMergeService(IFitParserService fitParser)
    {
        _fitParser = fitParser;
    }

    public async Task<string> MergeAsync(Stream gpxStream, Stream fitStream, int toleranceSeconds = 5)
    {
        var fitPoints = await _fitParser.ParseFitAsync(fitStream);

        // Build sorted list for binary search
        var fitByTime = fitPoints
            .Where(p => p.HeartRate.HasValue)
            .OrderBy(p => p.Timestamp)
            .ToList();

        using var reader = new StreamReader(gpxStream, Encoding.UTF8);
        var gpxContent = await reader.ReadToEndAsync();
        var doc = XDocument.Parse(gpxContent);

        var trkpts = doc.Descendants(GpxNs + "trkpt");
        int mergedCount = 0;

        foreach (var trkpt in trkpts)
        {
            var timeElement = trkpt.Element(GpxNs + "time");
            if (timeElement == null) continue;

            if (!DateTime.TryParse(timeElement.Value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var gpxTime))
                continue;

            var match = FindNearestFitPoint(fitByTime, gpxTime, toleranceSeconds);
            if (match == null) continue;

            EnsureHeartRateExtension(trkpt, match.HeartRate!.Value);
            mergedCount++;
        }

        // Add/update namespace declarations on root if we merged anything
        if (mergedCount > 0)
        {
            EnsureNamespaceDeclarations(doc.Root!);
        }

        return doc.Declaration != null
            ? doc.Declaration + "\n" + doc.Root
            : doc.ToString();
    }

    private FitDataPoint? FindNearestFitPoint(List<FitDataPoint> sorted, DateTime target, int toleranceSeconds)
    {
        if (sorted.Count == 0) return null;

        var tolerance = TimeSpan.FromSeconds(toleranceSeconds);

        // Binary search for the closest timestamp
        int lo = 0, hi = sorted.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (sorted[mid].Timestamp < target)
                lo = mid + 1;
            else
                hi = mid;
        }

        // Check lo and lo-1 to find the nearest
        FitDataPoint? best = null;
        TimeSpan bestDiff = TimeSpan.MaxValue;

        for (int i = Math.Max(0, lo - 1); i <= Math.Min(sorted.Count - 1, lo + 1); i++)
        {
            var diff = (sorted[i].Timestamp - target).Duration();
            if (diff < bestDiff)
            {
                bestDiff = diff;
                best = sorted[i];
            }
        }

        return bestDiff <= tolerance ? best : null;
    }

    private void EnsureHeartRateExtension(XElement trkpt, int heartRate)
    {
        var extensions = trkpt.Element(GpxNs + "extensions");
        if (extensions == null)
        {
            extensions = new XElement(GpxNs + "extensions");
            trkpt.Add(extensions);
        }

        var tpx = extensions.Element(TpxNs + "TrackPointExtension");
        if (tpx == null)
        {
            tpx = new XElement(TpxNs + "TrackPointExtension");
            extensions.Add(tpx);
        }

        var hrElement = tpx.Element(TpxNs + "hr");
        if (hrElement != null)
            hrElement.Value = heartRate.ToString();
        else
            tpx.Add(new XElement(TpxNs + "hr", heartRate));
    }

    private void EnsureNamespaceDeclarations(XElement root)
    {
        const string tpxPrefix = "gpxtpx";
        const string tpxUri = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";

        // Check if namespace is already declared
        if (root.Attributes().All(a => a.Value != tpxUri))
        {
            root.Add(new XAttribute(XNamespace.Xmlns + tpxPrefix, tpxUri));
        }
    }
}
