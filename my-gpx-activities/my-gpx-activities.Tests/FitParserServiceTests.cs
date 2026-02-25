using my_gpx_activities.ApiService.Services;

namespace my_gpx_activities.Tests;

/// <summary>
/// Unit tests for FitParserService — exercises FIT binary parsing without Aspire infrastructure.
/// </summary>
[TestFixture]
public class FitParserServiceTests
{
    private FitParserService _service = null!;

    [SetUp]
    public void SetUp() => _service = new FitParserService();

    /// <summary>
    /// Builds a minimal but valid FIT binary containing one record message (global msg 20)
    /// with fields: timestamp (253), heart_rate (3), and cadence (4).
    /// </summary>
    private static byte[] BuildMinimalFitWithCadence(byte heartRate = 140, byte cadence = 85)
    {
        // --- Data records ---
        var records = new List<byte>();

        // Definition message for local type 0, global message 20 (record)
        // Header: 0x40 = definition message, local type 0
        records.Add(0x40);
        records.Add(0x00); // reserved
        records.Add(0x00); // little-endian
        records.Add(20);   // global msg number low byte
        records.Add(0x00); // global msg number high byte
        records.Add(3);    // number of fields

        // Field 253: timestamp, size 4, base type 0x86 (uint32)
        records.Add(253); records.Add(4); records.Add(0x86);
        // Field 3: heart_rate, size 1, base type 0x02 (uint8)
        records.Add(3);   records.Add(1); records.Add(0x02);
        // Field 4: cadence, size 1, base type 0x02 (uint8)
        records.Add(4);   records.Add(1); records.Add(0x02);

        // Data message for local type 0
        // Header: 0x00 = data message, local type 0
        records.Add(0x00);
        // timestamp: FIT epoch seconds + a small offset (e.g. 1_000_000 seconds after 1989-12-31)
        uint ts = 1_000_000;
        records.Add((byte)(ts & 0xFF));
        records.Add((byte)((ts >> 8) & 0xFF));
        records.Add((byte)((ts >> 16) & 0xFF));
        records.Add((byte)((ts >> 24) & 0xFF));
        records.Add(heartRate);
        records.Add(cadence);

        var dataBytes = records.ToArray();

        // --- FIT file header (14 bytes) ---
        uint dataSize = (uint)dataBytes.Length;
        var header = new byte[14];
        header[0] = 14;   // header size
        header[1] = 0x10; // protocol version
        header[2] = 0x08; // profile version low
        header[3] = 0x64; // profile version high
        // data size (little-endian)
        header[4] = (byte)(dataSize & 0xFF);
        header[5] = (byte)((dataSize >> 8) & 0xFF);
        header[6] = (byte)((dataSize >> 16) & 0xFF);
        header[7] = (byte)((dataSize >> 24) & 0xFF);
        // magic bytes
        header[8]  = (byte)'.';
        header[9]  = (byte)'F';
        header[10] = (byte)'I';
        header[11] = (byte)'T';
        // CRC (not validated by parser, set to 0)
        header[12] = 0x00;
        header[13] = 0x00;

        return [.. header, .. dataBytes];
    }

    [Test]
    public async Task ParseFitAsync_RecordWithCadenceField_ReturnsCadenceValue()
    {
        var fitBytes = BuildMinimalFitWithCadence(heartRate: 140, cadence: 85);
        using var stream = new MemoryStream(fitBytes);

        var points = await _service.ParseFitAsync(stream);

        Assert.That(points, Has.Count.EqualTo(1), "Should parse exactly one record");

        var point = points[0];
        Assert.That(point.Cadence, Is.Not.Null, "Cadence should be non-null when FIT field 4 is present");
        Assert.That(point.Cadence, Is.EqualTo(85), "Cadence value should match the byte written to field 4");
    }

    [Test]
    public async Task ParseFitAsync_RecordWithCadenceField_AlsoReturnsHeartRate()
    {
        var fitBytes = BuildMinimalFitWithCadence(heartRate: 140, cadence: 85);
        using var stream = new MemoryStream(fitBytes);

        var points = await _service.ParseFitAsync(stream);

        var point = points[0];
        Assert.That(point.HeartRate, Is.Not.Null, "HeartRate should be non-null");
        Assert.That(point.HeartRate, Is.EqualTo(140));
    }

    [Test]
    public async Task ParseFitAsync_CadenceInvalidValue_ReturnsNull()
    {
        // 0xFF is the FIT invalid sentinel for uint8 fields
        var fitBytes = BuildMinimalFitWithCadence(heartRate: 120, cadence: 0xFF);
        using var stream = new MemoryStream(fitBytes);

        var points = await _service.ParseFitAsync(stream);

        Assert.That(points[0].Cadence, Is.Null,
            "Cadence 0xFF is the FIT invalid sentinel and should be treated as null");
    }

    [Test]
    public async Task ParseFitAsync_TimestampIsCorrectlyDecoded()
    {
        // FIT epoch = 1989-12-31 UTC; 1_000_000 seconds later ≈ 2001-09-08
        var fitBytes = BuildMinimalFitWithCadence();
        using var stream = new MemoryStream(fitBytes);

        var points = await _service.ParseFitAsync(stream);

        var expected = new DateTime(1989, 12, 31, 0, 0, 0, DateTimeKind.Utc).AddSeconds(1_000_000);
        Assert.That(points[0].Timestamp, Is.EqualTo(expected));
    }
}
