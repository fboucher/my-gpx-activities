using my_gpx_activities.ApiService.Services;

namespace my_gpx_activities.Tests;

[TestFixture]
public class FitImportTests
{
    private FitParserService _service = null!;

    [SetUp]
    public void SetUp() => _service = new FitParserService();

    private static byte[] BuildFitWithGps(double lat, double lon, double ele, DateTime time)
    {
        var records = new List<byte>();

        // Definition: local type 0, global message 20 (record) with timestamp, lat, lon, elevation
        records.Add(0x40);
        records.Add(0x00);
        records.Add(0x00);
        records.Add(20);
        records.Add(0x00);
        records.Add(4); // 4 fields

        // Field 253: timestamp (uint32)
        records.Add(253); records.Add(4); records.Add(0x86);
        // Field 0: position_lat (sint32, semicircles)
        records.Add(0); records.Add(4); records.Add(0x86);
        // Field 1: position_long (sint32, semicircles)
        records.Add(1); records.Add(4); records.Add(0x86);
        // Field 2: altitude (uint16, 0.5m units)
        records.Add(2); records.Add(2); records.Add(0x86);

        // Data record
        records.Add(0x00);

        // timestamp: seconds since FIT epoch
        var ts = (uint)(time - new DateTime(1989, 12, 31, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        records.Add((byte)(ts & 0xFF));
        records.Add((byte)((ts >> 8) & 0xFF));
        records.Add((byte)((ts >> 16) & 0xFF));
        records.Add((byte)((ts >> 24) & 0xFF));

        // lat: convert to semicircles (sint32) = lat * (2^31 / 180)
        var latSemi = (int)(lat * (2147483648.0 / 180.0));
        records.Add((byte)(latSemi & 0xFF));
        records.Add((byte)((latSemi >> 8) & 0xFF));
        records.Add((byte)((latSemi >> 16) & 0xFF));
        records.Add((byte)((latSemi >> 24) & 0xFF));

        // lon: convert to semicircles
        var lonSemi = (int)(lon * (2147483648.0 / 180.0));
        records.Add((byte)(lonSemi & 0xFF));
        records.Add((byte)((lonSemi >> 8) & 0xFF));
        records.Add((byte)((lonSemi >> 16) & 0xFF));
        records.Add((byte)((lonSemi >> 24) & 0xFF));

        // elevation: uint16 in 0.5m units
        var eleUnits = (ushort)(ele * 2);
        records.Add((byte)(eleUnits & 0xFF));
        records.Add((byte)((eleUnits >> 8) & 0xFF));

        var dataBytes = records.ToArray();

        // FIT header
        var dataSize = (uint)dataBytes.Length;
        var header = new byte[14];
        header[0] = 14;
        header[1] = 0x10;
        header[2] = 0x08;
        header[3] = 0x64;
        header[4] = (byte)(dataSize & 0xFF);
        header[5] = (byte)((dataSize >> 8) & 0xFF);
        header[6] = (byte)((dataSize >> 16) & 0xFF);
        header[7] = (byte)((dataSize >> 24) & 0xFF);
        header[8] = (byte)'.';
        header[9] = (byte)'F';
        header[10] = (byte)'I';
        header[11] = (byte)'T';
        header[12] = 0x00;
        header[13] = 0x00;

        return [.. header, .. dataBytes];
    }

    [Test]
    public async Task ParseFitAsync_WithGpsCoordinates_ReturnsLatitudeAndLongitude()
    {
        var fitBytes = BuildFitWithGps(
            lat: 47.6062,
            lon: -122.3321,
            ele: 50.0,
            time: new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));
        using var stream = new MemoryStream(fitBytes);

        var points = await _service.ParseFitAsync(stream);

        Assert.That(points, Has.Exactly(1).Items);
        Assert.That(points[0].Latitude, Is.Not.Null);
        Assert.That(points[0].Longitude, Is.Not.Null);
        Assert.That(points[0].Latitude, Is.EqualTo(47.6062).Within(0.001));
        Assert.That(points[0].Longitude, Is.EqualTo(-122.3321).Within(0.001));
    }

    [Test]
    public async Task ParseFitAsync_WithGpsCoordinates_ReturnsElevation()
    {
        var fitBytes = BuildFitWithGps(
            lat: 47.6062,
            lon: -122.3321,
            ele: 100.5,
            time: new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));
        using var stream = new MemoryStream(fitBytes);

        var points = await _service.ParseFitAsync(stream);

        Assert.That(points[0].Elevation, Is.Not.Null);
        Assert.That(points[0].Elevation, Is.EqualTo(100.5).Within(0.5));
    }

    [Test]
    public async Task ParseFitAsync_FileWithGps_CanBeImportedAsActivity()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "Morning_Nordic_Ski_part_1_.fit");
        if (!File.Exists(path))
            Assert.Ignore("Morning_Nordic_Ski_part_1_.fit not found");

        await using var stream = File.OpenRead(path);
        var points = await _service.ParseFitAsync(stream);

        var gpsPoints = points.Where(p => p.Latitude.HasValue && p.Longitude.HasValue).ToList();
        Console.WriteLine($"Total points: {points.Count}");
        Console.WriteLine($"Points with GPS: {gpsPoints.Count}");

        Assert.That(gpsPoints.Count, Is.GreaterThan(0),
            "FIT file should contain GPS coordinates to be importable as activity");
    }
}