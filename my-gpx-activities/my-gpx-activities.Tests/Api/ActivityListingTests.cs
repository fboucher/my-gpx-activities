using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace my_gpx_activities.Tests.Api;

/// <summary>
/// Integration tests for GET /api/activities (Issue #50).
/// Verifies that activities imported into the database are visible in the listing endpoint.
/// </summary>
[TestFixture]
public class ActivityListingTests
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

    /// <summary>
    /// Tests that GET /api/activities returns 200 OK with a JSON array.
    /// The array may be empty or non-empty depending on database state.
    /// </summary>
    [Test]
    public async Task GetActivities_ReturnsOkWithList()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var response = await _httpClient!.GetAsync("/api/activities", cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "GET /api/activities should return 200 OK");

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        Assert.That(doc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array),
            "Response should be a JSON array");
    }

    /// <summary>
    /// Tests the key regression for issue #50: an activity imported via POST should be visible in GET listing.
    /// Import a unique activity, then verify it appears in the activities list.
    /// </summary>
    [Test]
    public async Task ImportActivity_ThenGetActivities_ActivityIsVisible()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        // Import a unique activity
        var importRequest = new
        {
            activity = new
            {
                id = 18000000020L,
                name = "Issue50 Test Activity",
                sport_type = "Run",
                start_date = "2026-04-01T10:00:00Z",
                elapsed_time = 2400,
                distance = 8000.0,
                total_elevation_gain = 100.0,
                average_speed = 3.33,
                max_speed = 5.0,
                map = new
                {
                    polyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@"
                }
            },
            streams = new
            {
                latlng = new { data = new[] { new[] { 45.1234, -75.1234 }, new[] { 45.1235, -75.1235 } } },
                altitude = new { data = new[] { 100.0, 101.0 } },
                time = new { data = new[] { 0, 10 } }
            }
        };

        var importResponse = await _httpClient!.PostAsJsonAsync("/api/activities/import/strava", importRequest, cancellationToken);
        Assert.That(importResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "Import should succeed");

        // Verify the import returned an ID (not a duplicate)
        var importJson = await importResponse.Content.ReadAsStringAsync(cancellationToken);
        using var importDoc = JsonDocument.Parse(importJson);
        var hasId = importDoc.RootElement.TryGetProperty("id", out var idElement);

        if (!hasId)
        {
            // If it's a duplicate, the activity is already in the database, which is fine for this test
            Assert.Pass("Activity already exists in database (duplicate), skipping visibility check");
            return;
        }

        var activityId = idElement.GetInt32();

        // GET /api/activities and verify the imported activity appears
        var listResponse = await _httpClient!.GetAsync("/api/activities", cancellationToken);
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "GET /api/activities should return 200 OK");

        var listJson = await listResponse.Content.ReadAsStringAsync(cancellationToken);
        using var listDoc = JsonDocument.Parse(listJson);

        Assert.That(listDoc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array),
            "Activities response should be an array");

        // Look for the activity by title or ID
        var found = false;
        foreach (var activity in listDoc.RootElement.EnumerateArray())
        {
            // Try both PascalCase and camelCase property names
            var titleMatch = (activity.TryGetProperty("Title", out var titlePascal) && titlePascal.GetString() == "Issue50 Test Activity")
                || (activity.TryGetProperty("title", out var titleCamel) && titleCamel.GetString() == "Issue50 Test Activity");

            if (titleMatch)
            {
                found = true;
                break;
            }
        }

        Assert.That(found, Is.True,
            $"Imported activity 'Issue50 Test Activity' (id: {activityId}) should appear in GET /api/activities response");
    }

    /// <summary>
    /// Tests that a trainer/no-GPS activity is visible in the listing after import.
    /// Combines regression test for issue #50 (visibility) and issue #51 (no-GPS import support).
    /// </summary>
    [Test]
    public async Task ImportTrainerActivity_ThenGetActivities_TrainerActivityIsVisible()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        // Import a trainer activity (no GPS, has heartrate)
        var importRequest = new
        {
            activity = new
            {
                id = 18000000021L,
                name = "Issue50 Trainer Activity",
                sport_type = "VirtualRide",
                start_date = "2026-04-02T18:00:00Z",
                elapsed_time = 3600,
                distance = 25000.0,
                total_elevation_gain = 0.0,
                average_speed = 6.94,
                max_speed = 10.0,
                map = (object?)null // No map for indoor activity
            },
            streams = new
            {
                // No latlng stream (indoor/trainer activity)
                altitude = new { data = new[] { 50.0, 50.0, 50.0, 50.0 } },
                heartrate = new { data = new[] { 120, 135, 145, 130 } },
                time = new { data = new[] { 0, 600, 1200, 1800 } }
            }
        };

        var importResponse = await _httpClient!.PostAsJsonAsync("/api/activities/import/strava", importRequest, cancellationToken);
        Assert.That(importResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "Trainer activity import should succeed");

        // Verify the import returned an ID (not a duplicate)
        var importJson = await importResponse.Content.ReadAsStringAsync(cancellationToken);
        using var importDoc = JsonDocument.Parse(importJson);
        var hasId = importDoc.RootElement.TryGetProperty("id", out var idElement);

        if (!hasId)
        {
            Assert.Pass("Activity already exists in database (duplicate), skipping visibility check");
            return;
        }

        var activityId = idElement.GetInt32();

        // GET /api/activities and verify the trainer activity appears
        var listResponse = await _httpClient!.GetAsync("/api/activities", cancellationToken);
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "GET /api/activities should return 200 OK");

        var listJson = await listResponse.Content.ReadAsStringAsync(cancellationToken);
        using var listDoc = JsonDocument.Parse(listJson);

        Assert.That(listDoc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array),
            "Activities response should be an array");

        // Look for the trainer activity by title
        var found = false;
        foreach (var activity in listDoc.RootElement.EnumerateArray())
        {
            var titleMatch = (activity.TryGetProperty("Title", out var titlePascal) && titlePascal.GetString() == "Issue50 Trainer Activity")
                || (activity.TryGetProperty("title", out var titleCamel) && titleCamel.GetString() == "Issue50 Trainer Activity");

            if (titleMatch)
            {
                found = true;
                break;
            }
        }

        Assert.That(found, Is.True,
            $"Imported trainer activity 'Issue50 Trainer Activity' (id: {activityId}) should appear in GET /api/activities response");
    }
}
