using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace my_gpx_activities.Tests;

/// <summary>
/// Integration tests for GET /api/activities/heatmap (Issue #10).
/// Returns array of HeatMapActivity { activityId, activityName, sportType, trackPoints: [[lat,lon],...] }
/// </summary>
[TestFixture]
public class HeatMapApiTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private DistributedApplication? _app;
    private HttpClient? _httpClient;

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

    [Test]
    public async Task GetHeatmap_NoActivities_Returns200WithEmptyArray()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var response = await _httpClient!.GetAsync("/api/activities/heatmap", cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array),
            "Response should be a JSON array");
    }

    [Test]
    public async Task GetHeatmap_DateFromFilterInFuture_ReturnsEmptyArray()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        var futureDate = DateTime.UtcNow.AddYears(100).ToString("yyyy-MM-dd");

        var response = await _httpClient!.GetAsync(
            $"/api/activities/heatmap?dateFrom={futureDate}", cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(0),
            "No activities should exist in the far future");
    }

    [Test]
    public async Task GetHeatmap_DateToFilterInPast_ReturnsEmptyArray()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var response = await _httpClient!.GetAsync(
            "/api/activities/heatmap?dateTo=1900-01-01", cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(0),
            "No activities should exist before 1900");
    }

    [Test]
    public async Task GetHeatmap_UnknownSportTypeFilter_ReturnsEmptyArray()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        // Import an activity so there is data, then filter on a non-matching sport type
        var gpxPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "morning_GPX.gpx");
        Assume.That(File.Exists(gpxPath), "Test fixture morning_GPX.gpx must be present");
        await ImportGpxAsync(gpxPath, cancellationToken);

        var response = await _httpClient!.GetAsync(
            "/api/activities/heatmap?sportTypes=NonExistentSport12345", cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(0),
            "Filter on unknown sport type should return empty array");
    }

    [Test]
    public async Task GetHeatmap_CombinedDateAndSportFilter_ReturnsEmptyForNonMatchingCriteria()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var response = await _httpClient!.GetAsync(
            "/api/activities/heatmap?dateFrom=1900-01-01&dateTo=1900-12-31&sportTypes=Cycling",
            cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(0),
            "Combined filters with no match should yield empty array");
    }

    [Test]
    public async Task GetHeatmap_WithImportedActivity_TrackPointsAreLatLonArrays()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        var gpxPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "morning_GPX.gpx");
        Assume.That(File.Exists(gpxPath), "Test fixture morning_GPX.gpx must be present");

        await ImportGpxAsync(gpxPath, cancellationToken);

        var response = await _httpClient!.GetAsync("/api/activities/heatmap", cancellationToken);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var activities = doc.RootElement;

        // Validate structure of each returned HeatMapActivity
        foreach (var activity in activities.EnumerateArray())
        {
            Assert.That(activity.TryGetProperty("trackPoints", out var trackPoints), Is.True,
                "Each heatmap activity should have a 'trackPoints' property");
            Assert.That(trackPoints.ValueKind, Is.EqualTo(JsonValueKind.Array));

            foreach (var point in trackPoints.EnumerateArray())
            {
                // trackPoints is double[][] — each element is [lat, lon]
                Assert.That(point.ValueKind, Is.EqualTo(JsonValueKind.Array),
                    "Each track point should be a [lat, lon] array");
                Assert.That(point.GetArrayLength(), Is.GreaterThanOrEqualTo(2),
                    "Each track point must have at least lat and lon");
                Assert.That(point[0].ValueKind, Is.EqualTo(JsonValueKind.Number), "lat must be numeric");
                Assert.That(point[1].ValueKind, Is.EqualTo(JsonValueKind.Number), "lon must be numeric");
            }
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task ImportGpxAsync(string gpxFilePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(gpxFilePath);
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        content.Add(fileContent, "gpx", Path.GetFileName(gpxFilePath));
        await _httpClient!.PostAsync("/api/activities/import", content, cancellationToken);
    }
}
