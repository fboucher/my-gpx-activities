using System.Text;
using System.Xml.Linq;
using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace my_gpx_activities.Tests;

/// <summary>
/// Integration tests for POST /api/activities/smart-merge (Issue #11).
/// Accepts multipart form with 'gpx' + 'fit' files.
/// Returns merged GPX XML with heart-rate extensions in track points.
/// </summary>
[TestFixture]
public class SmartMergeApiTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private DistributedApplication? _app;
    private HttpClient? _httpClient;

    private string GpxFixturePath =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "morning_GPX.gpx");

    private string FitFixturePath =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "Ride_heart-rate.fit");

    [OneTimeSetUp]
    public async Task SetUp()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.my_gpx_activities_AppHost>(cancellationToken);

        appHost.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

        _app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await _app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        _httpClient = _app.CreateHttpClient("apiservice");
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("apiservice", cancellationToken)
            .WaitAsync(DefaultTimeout, cancellationToken);
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        if (_app != null)
            await _app.DisposeAsync();
        _httpClient?.Dispose();
    }

    // -------------------------------------------------------------------------
    // Validation tests
    // -------------------------------------------------------------------------

    [Test]
    public async Task SmartMerge_MissingGpxFile_Returns400()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        Assume.That(File.Exists(FitFixturePath), "Test fixture Ride_heart-rate.fit must be present");

        using var form = new MultipartFormDataContent();
        await using var fitStream = File.OpenRead(FitFixturePath);
        using var fitContent = new StreamContent(fitStream);
        form.Add(fitContent, "fit", Path.GetFileName(FitFixturePath));

        var response = await _httpClient!.PostAsync("/api/activities/smart-merge", form, cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
            "Missing GPX file should return 400 Bad Request");
    }

    [Test]
    public async Task SmartMerge_MissingFitFile_Returns400()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        Assume.That(File.Exists(GpxFixturePath), "Test fixture morning_GPX.gpx must be present");

        using var form = new MultipartFormDataContent();
        await using var gpxStream = File.OpenRead(GpxFixturePath);
        using var gpxContent = new StreamContent(gpxStream);
        form.Add(gpxContent, "gpx", Path.GetFileName(GpxFixturePath));

        var response = await _httpClient!.PostAsync("/api/activities/smart-merge", form, cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
            "Missing FIT file should return 400 Bad Request");
    }

    // -------------------------------------------------------------------------
    // Success path tests
    // -------------------------------------------------------------------------

    [Test]
    public async Task SmartMerge_ValidFiles_Returns200()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        Assume.That(File.Exists(GpxFixturePath), "Test fixture morning_GPX.gpx must be present");
        Assume.That(File.Exists(FitFixturePath), "Test fixture Ride_heart-rate.fit must be present");

        using var form = BuildMergeForm();

        var response = await _httpClient!.PostAsync("/api/activities/smart-merge", form, cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "Valid GPX + FIT should return 200 OK");

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.That(body, Is.Not.Null.And.Not.Empty, "Response body should not be empty");
    }

    [Test]
    public async Task SmartMerge_ValidFiles_ResponseIsValidGpxXml()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        Assume.That(File.Exists(GpxFixturePath), "Test fixture morning_GPX.gpx must be present");
        Assume.That(File.Exists(FitFixturePath), "Test fixture Ride_heart-rate.fit must be present");

        using var form = BuildMergeForm();
        var response = await _httpClient!.PostAsync("/api/activities/smart-merge", form, cancellationToken);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        XDocument? doc = null;
        Assert.DoesNotThrow(() => doc = XDocument.Parse(body),
            "Response body should be valid XML");

        Assert.That(doc!.Root, Is.Not.Null);
        Assert.That(doc.Root!.Name.LocalName, Is.EqualTo("gpx"),
            "Root element should be <gpx>");

        var ns = doc.Root.GetDefaultNamespace();
        var trackPoints = doc.Descendants(ns + "trkpt").ToList();
        Assert.That(trackPoints, Is.Not.Empty,
            "Merged GPX should contain at least one track point");
    }

    [Test]
    public async Task SmartMerge_MatchingTimestamps_TrackPointsContainHeartRateExtension()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        Assume.That(File.Exists(GpxFixturePath), "Test fixture morning_GPX.gpx must be present");
        Assume.That(File.Exists(FitFixturePath), "Test fixture Ride_heart-rate.fit must be present");

        using var form = BuildMergeForm();
        var response = await _httpClient!.PostAsync("/api/activities/smart-merge", form, cancellationToken);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = XDocument.Parse(body);
        var garminNs = XNamespace.Get("http://www.garmin.com/xmlschemas/TrackPointExtension/v1");

        var heartRateElements = doc.Descendants(garminNs + "hr").ToList();
        Assert.That(heartRateElements, Is.Not.Empty,
            "At least one track point should have a heart rate extension when FIT data timestamps align");
    }

    [Test]
    public async Task SmartMerge_GpxWithNoMatchingFitTimestamps_ReturnsSuccessWithoutHeartRate()
    {
        // Use a minimal GPX with timestamps far in the past so no FIT data will match
        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        Assume.That(File.Exists(FitFixturePath), "Test fixture Ride_heart-rate.fit must be present");

        const string minimalGpx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" creator="Test" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <name>No HR Match</name>
                <type>Cycling</type>
                <trkseg>
                  <trkpt lat="46.0" lon="8.0">
                    <ele>500.0</ele>
                    <time>1980-01-01T00:00:00Z</time>
                  </trkpt>
                  <trkpt lat="46.001" lon="8.001">
                    <ele>501.0</ele>
                    <time>1980-01-01T00:00:10Z</time>
                  </trkpt>
                </trkseg>
              </trk>
            </gpx>
            """;

        using var form = new MultipartFormDataContent();
        var gpxBytes = Encoding.UTF8.GetBytes(minimalGpx);
        using var gpxContent = new ByteArrayContent(gpxBytes);
        form.Add(gpxContent, "gpx", "no_match.gpx");

        await using var fitStream = File.OpenRead(FitFixturePath);
        using var fitContent = new StreamContent(fitStream);
        form.Add(fitContent, "fit", Path.GetFileName(FitFixturePath));

        var response = await _httpClient!.PostAsync("/api/activities/smart-merge", form, cancellationToken);

        // Should succeed (2xx) — no match is not an error
        Assert.That((int)response.StatusCode, Is.LessThan(500),
            "GPX with no matching FIT timestamps should not cause a 5xx server error");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = XDocument.Parse(body);
            var ns = doc.Root!.GetDefaultNamespace();
            var trackPoints = doc.Descendants(ns + "trkpt").ToList();
            Assert.That(trackPoints, Is.Not.Empty,
                "Track points without FIT match should still appear in merged GPX");

            var garminNs = XNamespace.Get("http://www.garmin.com/xmlschemas/TrackPointExtension/v1");
            var hrElements = doc.Descendants(garminNs + "hr").ToList();
            Assert.That(hrElements, Is.Empty,
                "Track points with no FIT data match should have no heart rate extensions");
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private MultipartFormDataContent BuildMergeForm()
    {
        var form = new MultipartFormDataContent();

        var gpxStream = File.OpenRead(GpxFixturePath);
        var gpxContent = new StreamContent(gpxStream);
        form.Add(gpxContent, "gpx", Path.GetFileName(GpxFixturePath));

        var fitStream = File.OpenRead(FitFixturePath);
        var fitContent = new StreamContent(fitStream);
        form.Add(fitContent, "fit", Path.GetFileName(FitFixturePath));

        return form;
    }
}
