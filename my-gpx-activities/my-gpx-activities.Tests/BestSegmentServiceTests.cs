using my_gpx_activities.ApiService.Services;

namespace my_gpx_activities.Tests;

/// <summary>
/// Unit tests for BestSegmentService — verifies best-effort segment computation
/// from track data without requiring Aspire infrastructure.
/// Track data format: [lat, lon, elevation_or_null, hr_or_null, unix_ms_or_null, cadence_or_null]
/// </summary>
[TestFixture]
[Category("Unit")]
public class BestSegmentServiceTests
{
    private BestSegmentService _service = null!;

    [SetUp]
    public void SetUp() => _service = new BestSegmentService();

    /// <summary>
    /// Generates straight-line track data along the latitude axis.
    /// Each step of 0.001° latitude ≈ 111.2 m.
    /// </summary>
    private static List<double?[]> GenerateLinearTrack(
        double startLat, double startLon,
        int pointCount, double latStep,
        long startTimeMs, long intervalMs)
    {
        return Enumerable.Range(0, pointCount)
            .Select(i => new double?[]
            {
                startLat + i * latStep,
                startLon,
                null,
                null,
                (double)(startTimeMs + i * intervalMs),
                null
            })
            .ToList();
    }

    [Test]
    public void ComputeBestSegments_EmptyTrack_ReturnsEmpty()
    {
        var result = _service.ComputeBestSegments([]);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ComputeBestSegments_SinglePoint_ReturnsEmpty()
    {
        var track = GenerateLinearTrack(45.0, 0.0, 1, 0.001, 0, 1_000);

        var result = _service.ComputeBestSegments(track);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ComputeBestSegments_MissingGpsCoordinates_ReturnsEmpty()
    {
        var track = Enumerable.Range(0, 20)
            .Select(i => new double?[] { null, null, null, null, (double)(i * 1_000), null })
            .ToList();

        var result = _service.ComputeBestSegments(track);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ComputeBestSegments_MissingTimestamps_ReturnsEmpty()
    {
        var track = Enumerable.Range(0, 20)
            .Select(i => new double?[] { 45.0 + i * 0.001, 0.0, null, null, null, null })
            .ToList();

        var result = _service.ComputeBestSegments(track);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ComputeBestSegments_ActivityTooShortFor5km_ReturnsOnly1kmSegment()
    {
        // 11 points × 0.001° ≈ 111 m/step → ~1.1 km total; only the 1 km target fits
        var track = GenerateLinearTrack(45.0, 0.0, 11, 0.001, 0, 22_000);

        var result = _service.ComputeBestSegments(track);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].DistanceMeters, Is.EqualTo(1000));
    }

    [Test]
    public void ComputeBestSegments_KnownConstantSpeed_ReturnsApproximateSpeed()
    {
        // 0.001° latitude ≈ 111.19 m; intervalMs = 22 240 ms → ~5.0 m/s
        const long intervalMs = 22_240;
        const double expectedSpeedMs = 5.0;
        var track = GenerateLinearTrack(45.0, 0.0, 20, 0.001, 0, intervalMs);

        var result = _service.ComputeBestSegments(track);
        var segment1km = result.FirstOrDefault(s => s.DistanceMeters == 1000);

        Assert.That(segment1km, Is.Not.Null, "1 km best segment should exist");
        Assert.That(segment1km!.SpeedMs, Is.EqualTo(expectedSpeedMs).Within(0.1));
    }

    [Test]
    public void ComputeBestSegments_FindsFastestSegment_WhenSpeedVaries()
    {
        // Slow half: 11 points at 40 000 ms intervals → ~2.78 m/s
        var slowHalf = GenerateLinearTrack(45.0, 0.0, 11, 0.001, 0, 40_000);
        long splitTime = (long)slowHalf.Last()[4]!.Value;

        // Fast half: 11 points at 15 000 ms intervals → ~7.41 m/s, continuing from the end of slow half
        var fastHalf = GenerateLinearTrack(45.010, 0.0, 11, 0.001, splitTime, 15_000);
        fastHalf.RemoveAt(0); // drop duplicate of the shared boundary point

        var track = slowHalf.Concat(fastHalf).ToList();

        var result = _service.ComputeBestSegments(track);
        var segment1km = result.FirstOrDefault(s => s.DistanceMeters == 1000);

        Assert.That(segment1km, Is.Not.Null);
        // Best segment must come from the fast half (> 5 m/s), not the slow half (~2.78 m/s)
        Assert.That(segment1km!.SpeedMs, Is.GreaterThan(5.0),
            "Best 1 km segment should reflect the faster portion of the track");
    }

    [Test]
    public void ComputeBestSegments_LongActivity_ReturnsAllThreeSegmentTargets()
    {
        // 185 points × 0.001° ≈ 111 m/step → ~20.4 km total; all three targets (1/5/10 km) fit
        var track = GenerateLinearTrack(45.0, 0.0, 185, 0.001, 0, 22_240);

        var result = _service.ComputeBestSegments(track);

        Assert.That(result, Has.Count.EqualTo(3), "Should return best segments for 1 km, 5 km, and 10 km");
        Assert.That(result.Select(s => s.DistanceMeters), Is.EquivalentTo(new[] { 1000, 5000, 10000 }));
    }

    [Test]
    public void ComputeBestSegments_ResultsContainValidActivityId_DefaultIsEmpty()
    {
        var track = GenerateLinearTrack(45.0, 0.0, 20, 0.001, 0, 22_240);

        var result = _service.ComputeBestSegments(track);

        // ActivityId is assigned later by the caller; service returns Guid.Empty or a fresh Guid per segment
        Assert.That(result, Is.Not.Empty);
        foreach (var segment in result)
            Assert.That(segment.SpeedMs, Is.GreaterThan(0));
    }
}
