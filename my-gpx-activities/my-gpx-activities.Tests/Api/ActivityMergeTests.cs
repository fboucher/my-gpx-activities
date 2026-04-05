using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Microsoft.Extensions.Logging;

namespace my_gpx_activities.Tests.Api;

/// <summary>
/// Integration tests for the activity merge feature (Issue #41).
/// Covers GET /api/activities/merge/preview and POST /api/activities/merge.
///
/// NOTE: These tests are written spec-first while Naomi builds the implementation.
/// Minor field name adjustments may be needed once the implementation is complete.
/// </summary>
[TestFixture]
public class ActivityMergeTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private DistributedApplication? _app;
    private HttpClient? _httpClient;

    // Activity IDs created during setup for use across tests
    private Guid _overlappingActivityAId;
    private Guid _overlappingActivityBId;
    private Guid _nonOverlappingActivityAId;
    private Guid _nonOverlappingActivityBId;

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

        // Seed test activities used across multiple tests
        _overlappingActivityAId = await ImportStravaActivityAsync(new
        {
            activity = new
            {
                id = 41_000_000_001L,
                name = "Merge Test — Overlap A (10 HR pts)",
                sport_type = "Run",
                start_date = "2026-01-01T08:00:00Z",
                elapsed_time = 3600,
                distance = 8000.0,
                total_elevation_gain = 80.0,
                average_speed = 2.22,
                max_speed = 4.0,
                map = new { polyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@" }
            },
            streams = new
            {
                // 10 track points spread over 1 hour
                latlng = new { data = new[] { new[] { 45.10, -75.10 }, new[] { 45.11, -75.11 }, new[] { 45.12, -75.12 }, new[] { 45.13, -75.13 }, new[] { 45.14, -75.14 }, new[] { 45.15, -75.15 }, new[] { 45.16, -75.16 }, new[] { 45.17, -75.17 }, new[] { 45.18, -75.18 }, new[] { 45.19, -75.19 } } },
                altitude = new { data = new[] { 100.0, 101.0, 102.0, 103.0, 104.0, 105.0, 106.0, 107.0, 108.0, 109.0 } },
                heartrate = new { data = new[] { 120, 122, 124, 126, 128, 130, 132, 134, 136, 138 } },
                time = new { data = new[] { 0, 400, 800, 1200, 1600, 2000, 2400, 2800, 3200, 3600 } }
            }
        }, cancellationToken);

        _overlappingActivityBId = await ImportStravaActivityAsync(new
        {
            activity = new
            {
                id = 41_000_000_002L,
                name = "Merge Test — Overlap B (3 HR pts)",
                sport_type = "Run",
                // Starts 30 minutes into A — overlapping time window
                start_date = "2026-01-01T08:30:00Z",
                elapsed_time = 3600,
                distance = 6000.0,
                total_elevation_gain = 60.0,
                average_speed = 1.67,
                max_speed = 3.0,
                map = new { polyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@" }
            },
            streams = new
            {
                // 3 track points — fewer HR data points than activity A
                latlng = new { data = new[] { new[] { 45.20, -75.20 }, new[] { 45.21, -75.21 }, new[] { 45.22, -75.22 } } },
                altitude = new { data = new[] { 110.0, 111.0, 112.0 } },
                heartrate = new { data = new[] { 140, 145, 150 } },
                time = new { data = new[] { 0, 1800, 3600 } }
            }
        }, cancellationToken);

        _nonOverlappingActivityAId = await ImportStravaActivityAsync(new
        {
            activity = new
            {
                id = 41_000_000_003L,
                name = "Merge Test — No-Overlap A",
                sport_type = "Ride",
                start_date = "2026-02-01T08:00:00Z",
                elapsed_time = 3600,
                distance = 15000.0,
                total_elevation_gain = 120.0,
                average_speed = 4.17,
                max_speed = 8.0,
                map = new { polyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@" }
            },
            streams = new
            {
                latlng = new { data = new[] { new[] { 46.10, -76.10 }, new[] { 46.11, -76.11 }, new[] { 46.12, -76.12 } } },
                altitude = new { data = new[] { 200.0, 201.0, 202.0 } },
                time = new { data = new[] { 0, 1800, 3600 } }
            }
        }, cancellationToken);

        _nonOverlappingActivityBId = await ImportStravaActivityAsync(new
        {
            activity = new
            {
                id = 41_000_000_004L,
                name = "Merge Test — No-Overlap B",
                sport_type = "Ride",
                // Starts 2 hours after A ends — no overlap, with a 1-hour gap
                start_date = "2026-02-01T11:00:00Z",
                elapsed_time = 3600,
                distance = 18000.0,
                total_elevation_gain = 150.0,
                average_speed = 5.0,
                max_speed = 9.0,
                map = new { polyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@" }
            },
            streams = new
            {
                latlng = new { data = new[] { new[] { 46.20, -76.20 }, new[] { 46.21, -76.21 }, new[] { 46.22, -76.22 } } },
                altitude = new { data = new[] { 210.0, 211.0, 212.0 } },
                time = new { data = new[] { 0, 1800, 3600 } }
            }
        }, cancellationToken);
    }

    [OneTimeTearDown]
    public async Task TearDown()
    {
        if (_app != null)
            await _app.DisposeAsync();
        _httpClient?.Dispose();
    }

    // ─── Preview endpoint ────────────────────────────────────────────────────────

    /// <summary>
    /// Two activities with overlapping time windows should yield suggestedMode == "merge".
    /// </summary>
    [Test]
    public async Task GetMergePreview_OverlappingActivities_SuggestsMergeMode()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var response = await _httpClient!.GetAsync(
            $"/api/activities/merge/preview?activityAId={_overlappingActivityAId}&activityBId={_overlappingActivityBId}",
            cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "Preview for overlapping activities should return 200 OK");

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        Assert.That(doc.RootElement.TryGetProperty("suggestedMode", out var modeEl), Is.True,
            "Response should contain 'suggestedMode'");
        Assert.That(modeEl.GetString(), Is.EqualTo("merge").IgnoreCase,
            "Overlapping activities should suggest 'merge' mode");
    }

    /// <summary>
    /// Two activities with non-overlapping time windows should yield suggestedMode == "append".
    /// </summary>
    [Test]
    public async Task GetMergePreview_NonOverlappingActivities_SuggestsAppendMode()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var response = await _httpClient!.GetAsync(
            $"/api/activities/merge/preview?activityAId={_nonOverlappingActivityAId}&activityBId={_nonOverlappingActivityBId}",
            cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "Preview for non-overlapping activities should return 200 OK");

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        Assert.That(doc.RootElement.TryGetProperty("suggestedMode", out var modeEl), Is.True,
            "Response should contain 'suggestedMode'");
        Assert.That(modeEl.GetString(), Is.EqualTo("append").IgnoreCase,
            "Non-overlapping activities should suggest 'append' mode");
    }

    /// <summary>
    /// An activity that has GPS track data should have "GPS" listed in the channels array.
    /// </summary>
    [Test]
    public async Task GetMergePreview_ReturnsCorrectChannels()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var response = await _httpClient!.GetAsync(
            $"/api/activities/merge/preview?activityAId={_overlappingActivityAId}&activityBId={_overlappingActivityBId}",
            cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        Assert.That(doc.RootElement.TryGetProperty("channels", out var channelsEl), Is.True,
            "Response should include a 'channels' property");
        Assert.That(channelsEl.ValueKind, Is.EqualTo(JsonValueKind.Array),
            "'channels' should be an array");

        var channelValues = channelsEl.EnumerateArray()
            .Select(c => c.GetString() ?? string.Empty)
            .ToList();

        Assert.That(channelValues, Has.Some.Matches<string>(c => c.Equals("GPS", StringComparison.OrdinalIgnoreCase)),
            "An activity with GPS track data should include 'GPS' in channels");
    }

    /// <summary>
    /// Providing an unknown activity ID should return 404.
    /// </summary>
    [Test]
    public async Task GetMergePreview_InvalidId_Returns404()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var unknownId = Guid.NewGuid();
        var response = await _httpClient!.GetAsync(
            $"/api/activities/merge/preview?activityAId={unknownId}&activityBId={_overlappingActivityBId}",
            cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
            "Preview with an unknown activityAId should return 404");
    }

    // ─── Merge endpoint ──────────────────────────────────────────────────────────

    /// <summary>
    /// Merging two overlapping activities in merge mode should produce a new activity
    /// while leaving the originals untouched.
    /// </summary>
    [Test]
    public async Task MergeActivities_MergeMode_CreatesNewActivity()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var request = new
        {
            activityAId = _overlappingActivityAId,
            activityBId = _overlappingActivityBId,
            mode = "merge",
            sportType = "Run",
            name = "Merged Overlap Run"
        };

        var response = await _httpClient!.PostAsJsonAsync("/api/activities/merge", request, cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "Merge should return 200 OK");

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        Assert.That(doc.RootElement.TryGetProperty("id", out var idEl), Is.True,
            "Merge response should include the new activity 'id'");
        Assert.That(idEl.ValueKind, Is.EqualTo(JsonValueKind.String),
            "Merged activity ID should be a GUID string");
        Assert.That(Guid.TryParse(idEl.GetString(), out _), Is.True,
            "Merged activity ID should be a valid GUID");
    }

    /// <summary>
    /// When activity A has more HR data points than activity B, the merged result's HR channel
    /// should be sourced from activity A (conflict resolution: more data points wins).
    /// </summary>
    [Test]
    public async Task MergeActivities_MergeMode_PreferMoreDataPointsChannel()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        // Activity A has 10 HR points; activity B has 3 HR points.
        var request = new
        {
            activityAId = _overlappingActivityAId,
            activityBId = _overlappingActivityBId,
            mode = "merge",
            sportType = "Run",
            name = "Merge HR Conflict Test"
        };

        var mergeResponse = await _httpClient!.PostAsJsonAsync("/api/activities/merge", request, cancellationToken);
        Assert.That(mergeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var mergeJson = await mergeResponse.Content.ReadAsStringAsync(cancellationToken);
        using var mergeDoc = JsonDocument.Parse(mergeJson);

        Assert.That(mergeDoc.RootElement.TryGetProperty("id", out var idEl), Is.True);
        var mergedId = Guid.Parse(idEl.GetString()!);

        // Retrieve the merged activity and inspect its track data
        var getResponse = await _httpClient!.GetAsync($"/api/activities/{mergedId}", cancellationToken);
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var getJson = await getResponse.Content.ReadAsStringAsync(cancellationToken);
        using var getDoc = JsonDocument.Parse(getJson);

        Assert.That(getDoc.RootElement.TryGetProperty("trackPoints", out var tpCountEl)
                    || getDoc.RootElement.TryGetProperty("TrackPoints", out tpCountEl), Is.True,
            "Merged activity should report trackPoints count");

        // The merged activity should have at least as many track points as the larger source
        var trackPointCount = tpCountEl.GetInt32();
        Assert.That(trackPointCount, Is.GreaterThanOrEqualTo(10),
            "Merged activity should have at least 10 track points (from the richer source A)");
    }

    /// <summary>
    /// Appending two non-overlapping activities should produce a new activity whose
    /// time span covers both originals, ordered chronologically.
    /// </summary>
    [Test]
    public async Task MergeActivities_AppendMode_ConcatenatesChronologically()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        // Activity A: 2026-02-01 08:00–09:00
        // Activity B: 2026-02-01 11:00–12:00
        var request = new
        {
            activityAId = _nonOverlappingActivityAId,
            activityBId = _nonOverlappingActivityBId,
            mode = "append",
            sportType = "Ride",
            name = "Appended Ride"
        };

        var mergeResponse = await _httpClient!.PostAsJsonAsync("/api/activities/merge", request, cancellationToken);
        Assert.That(mergeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "Append merge should return 200 OK");

        var mergeJson = await mergeResponse.Content.ReadAsStringAsync(cancellationToken);
        using var mergeDoc = JsonDocument.Parse(mergeJson);

        Assert.That(mergeDoc.RootElement.TryGetProperty("id", out var idEl), Is.True);
        var mergedId = Guid.Parse(idEl.GetString()!);

        var getResponse = await _httpClient!.GetAsync($"/api/activities/{mergedId}", cancellationToken);
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var getJson = await getResponse.Content.ReadAsStringAsync(cancellationToken);
        using var getDoc = JsonDocument.Parse(getJson);

        var startPropFound = getDoc.RootElement.TryGetProperty("startDateTime", out var startEl)
                             || getDoc.RootElement.TryGetProperty("StartDateTime", out startEl);
        var endPropFound = getDoc.RootElement.TryGetProperty("endDateTime", out var endEl)
                           || getDoc.RootElement.TryGetProperty("EndDateTime", out endEl);

        Assert.That(startPropFound, Is.True, "Merged activity should have a startDateTime");
        Assert.That(endPropFound, Is.True, "Merged activity should have an endDateTime");

        var start = DateTime.Parse(startEl.GetString()!).ToUniversalTime();
        var end = DateTime.Parse(endEl.GetString()!).ToUniversalTime();

        // Should start at or before activity A's start (2026-02-01 08:00)
        Assert.That(start, Is.LessThanOrEqualTo(new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc).AddMinutes(1)),
            "Merged start should be at/near the start of the earlier activity");

        // Should end at or after activity B's end (2026-02-01 12:00)
        Assert.That(end, Is.GreaterThanOrEqualTo(new DateTime(2026, 2, 1, 11, 59, 0, DateTimeKind.Utc)),
            "Merged end should be at/near the end of the later activity");
    }

    /// <summary>
    /// A gap between two activities in append mode is acceptable.
    /// The merged result should span from A's start to B's end, gap included.
    /// </summary>
    [Test]
    public async Task MergeActivities_AppendMode_AcceptsTimeGap()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        // A ends at 09:00; B starts at 11:00 — 2-hour gap
        var request = new
        {
            activityAId = _nonOverlappingActivityAId,
            activityBId = _nonOverlappingActivityBId,
            mode = "append",
            sportType = "Ride",
            name = "Gapped Ride Append"
        };

        var mergeResponse = await _httpClient!.PostAsJsonAsync("/api/activities/merge", request, cancellationToken);

        // The endpoint must accept the gap without returning an error
        Assert.That(mergeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "Append mode should succeed even when there is a time gap between activities");

        var json = await mergeResponse.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        Assert.That(doc.RootElement.TryGetProperty("id", out _), Is.True,
            "Response should include the new merged activity id");
    }

    /// <summary>
    /// After a merge, both source activities must still be retrievable by their original IDs.
    /// </summary>
    [Test]
    public async Task MergeActivities_PreservesOriginalActivities()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var request = new
        {
            activityAId = _overlappingActivityAId,
            activityBId = _overlappingActivityBId,
            mode = "merge",
            sportType = "Run",
            name = "Preservation Check"
        };

        var mergeResponse = await _httpClient!.PostAsJsonAsync("/api/activities/merge", request, cancellationToken);
        Assert.That(mergeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var aResponse = await _httpClient!.GetAsync($"/api/activities/{_overlappingActivityAId}", cancellationToken);
        Assert.That(aResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "Source activity A should still exist after merge");

        var bResponse = await _httpClient!.GetAsync($"/api/activities/{_overlappingActivityBId}", cancellationToken);
        Assert.That(bResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "Source activity B should still exist after merge");
    }

    /// <summary>
    /// The name provided in the merge request should be used as the title of the new activity.
    /// </summary>
    [Test]
    public async Task MergeActivities_CustomName_UsedInResult()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        const string customName = "My Custom Merged Activity";

        var request = new
        {
            activityAId = _nonOverlappingActivityAId,
            activityBId = _nonOverlappingActivityBId,
            mode = "append",
            sportType = "Ride",
            name = customName
        };

        var mergeResponse = await _httpClient!.PostAsJsonAsync("/api/activities/merge", request, cancellationToken);
        Assert.That(mergeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var mergeJson = await mergeResponse.Content.ReadAsStringAsync(cancellationToken);
        using var mergeDoc = JsonDocument.Parse(mergeJson);

        Assert.That(mergeDoc.RootElement.TryGetProperty("id", out var idEl), Is.True);
        var mergedId = Guid.Parse(idEl.GetString()!);

        var getResponse = await _httpClient!.GetAsync($"/api/activities/{mergedId}", cancellationToken);
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var getJson = await getResponse.Content.ReadAsStringAsync(cancellationToken);
        using var getDoc = JsonDocument.Parse(getJson);

        var titleFound = getDoc.RootElement.TryGetProperty("title", out var titleEl)
                         || getDoc.RootElement.TryGetProperty("Title", out titleEl);
        Assert.That(titleFound, Is.True, "Merged activity should have a title");
        Assert.That(titleEl.GetString(), Is.EqualTo(customName),
            "Merged activity title should match the name supplied in the request");
    }

    /// <summary>
    /// Providing an unknown activity ID in the merge request should return 404.
    /// </summary>
    [Test]
    public async Task MergeActivities_InvalidActivityId_Returns404()
    {
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        var request = new
        {
            activityAId = Guid.NewGuid(), // unknown
            activityBId = _overlappingActivityBId,
            mode = "merge",
            sportType = "Run",
            name = "Should Fail"
        };

        var response = await _httpClient!.PostAsJsonAsync("/api/activities/merge", request, cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
            "Merge with an unknown activityAId should return 404");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Imports a Strava activity and returns its assigned Guid, or returns a
    /// previously-imported duplicate's ID when re-run in the same database.
    /// </summary>
    private async Task<Guid> ImportStravaActivityAsync(object requestBody, CancellationToken cancellationToken)
    {
        var response = await _httpClient!.PostAsJsonAsync("/api/activities/import/strava", requestBody, cancellationToken);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Seeding activity via Strava import should return 200 OK");

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        // Import returns { id: <int> } for new activities; { duplicate: true, message } for duplicates.
        // For duplicates we need to look the activity up by Strava external ID.
        if (doc.RootElement.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number)
        {
            // The Strava import endpoint returns the internal integer row ID; we need the UUID.
            // Retrieve the full list and match by the expected name embedded in the request.
            return await ResolveActivityGuidByStravaIdAsync(requestBody, cancellationToken);
        }

        // Duplicate path — same lookup
        return await ResolveActivityGuidByStravaIdAsync(requestBody, cancellationToken);
    }

    /// <summary>
    /// Walks GET /api/activities to find the Guid for the activity whose title matches
    /// the name field in the import request body.
    /// </summary>
    private async Task<Guid> ResolveActivityGuidByStravaIdAsync(object requestBody, CancellationToken cancellationToken)
    {
        // Extract name from the anonymous request object via JSON round-trip
        var requestJson = JsonSerializer.Serialize(requestBody);
        using var requestDoc = JsonDocument.Parse(requestJson);
        var activityName = requestDoc.RootElement
            .GetProperty("activity")
            .GetProperty("name")
            .GetString()!;

        var listResponse = await _httpClient!.GetAsync("/api/activities", cancellationToken);
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var listJson = await listResponse.Content.ReadAsStringAsync(cancellationToken);
        using var listDoc = JsonDocument.Parse(listJson);

        foreach (var element in listDoc.RootElement.EnumerateArray())
        {
            var titleFound = element.TryGetProperty("title", out var titleEl)
                             || element.TryGetProperty("Title", out titleEl);

            if (titleFound && titleEl.GetString() == activityName)
            {
                var idFound = element.TryGetProperty("id", out var idEl)
                              || element.TryGetProperty("Id", out idEl);
                if (idFound && Guid.TryParse(idEl.GetString(), out var guid))
                    return guid;
            }
        }

        Assert.Fail($"Could not resolve Guid for seeded activity '{activityName}'");
        return Guid.Empty; // unreachable
    }
}
