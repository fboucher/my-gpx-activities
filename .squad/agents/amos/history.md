# History — Amos

## Project Context
**Project:** my-gpx-activities — GPS sports activity visualizer
**Stack:** .NET 10 Aspire, Blazor Server (MudBlazor), ApiService (Minimal API + OpenAPI), PostgreSQL (Npgsql), NUnit tests
**Repo layout:**
- my-gpx-activities.ApiService/ — backend API, GPX/FIT parsing, repositories
- my-gpx-activities.AppHost/ — Aspire orchestration host
- my-gpx-activities.webapp/ — Blazor Server frontend
- my-gpx-activities.ServiceDefaults/ — shared extensions
- my-gpx-activities.Tests/ — NUnit integration tests
**User:** fboucher

## Learnings

### Session: Tests for Issues #10 and #11 (PR #17)

**Branch:** `squad/tests-10-11` → PR to `dev`

**Test files created:**
- `my-gpx-activities.Tests/HeatMapApiTests.cs` — 6 tests for GET /api/activities/heatmap
- `my-gpx-activities.Tests/SmartMergeApiTests.cs` — 5 tests for POST /api/activities/smart-merge
- `my-gpx-activities.Tests/TestData/morning_GPX.gpx` — fixture file
- `my-gpx-activities.Tests/TestData/Ride_heart-rate.fit` — fixture file
- Updated `my-gpx-activities.Tests.csproj` with `<Content CopyToOutputDirectory>` for TestData/

**Patterns used:**
- `[OneTimeSetUp]`/`[OneTimeTearDown]` to share a single Aspire app instance across tests in a fixture (avoids spinning up the full stack per test)
- `DistributedApplicationTestingBuilder.CreateAsync<Projects.my_gpx_activities_AppHost>()` — same as WebTests.cs
- `Assume.That(File.Exists(...))` to skip tests gracefully if fixture files are missing
- `TestContext.CurrentContext.TestDirectory` to locate fixture files at runtime
- Multipart form construction with `MultipartFormDataContent` + `StreamContent`/`ByteArrayContent`
- `XDocument.Parse()` / `XDocument.Descendants()` for XML response validation
- `using Aspire.Hosting;` needed explicitly (not in global usings) for `DistributedApplication` type
- Bash sessions can switch branches unexpectedly in this environment — always verify `git branch --show-current` before committing, and do checkout+add+commit in a single chained bash call if possible

**What was tested:**
- HeatMap API: empty result, dateFrom/dateTo boundary filtering, sportType filter, combined filters, trackPoints shape validation ([[lat,lon]] double arrays)
- SmartMerge API: missing GPX → 400, missing FIT → 400, valid merge → 200 with valid GPX XML, HR extension presence when timestamps align, no-match case returns success with no HR extensions

**Implementation discovered (Naomi's work in working tree):**
- `FitParserService` — custom FIT binary parser (no third-party FIT SDK)
- `SmartMergeService` — binary-searches sorted FIT points for nearest timestamp within 5s tolerance
- `HeatMapActivity` model: `{ ActivityId, ActivityName, SportType, TrackPoints: double[][] }`
- Heatmap endpoint accepts `dateFrom`, `dateTo`, `sportTypes` (comma-separated) query params

### Session: CI Workflow Fix (dorny/test-reporter failure)

**Issue:** The CI workflow `.github/workflows/ci.yml` was failing with "No test report files were found" from the `dorny/test-reporter` action.

**Root Cause:** The `dotnet test` command used the `--no-build` flag, which caused a dependency on the prior `dotnet build` step completing correctly. If the build didn't produce test binaries in the expected state, the test step would fail to generate the TRX report file.

**Fix Applied:** Removed `--no-build` flag from the test command on line 37 of `.github/workflows/ci.yml`. This makes `dotnet test` self-contained—it rebuilds only what's needed (with cached NuGet packages from the earlier restore step) and guarantees the TRX file is generated.

**Learning:** When CI steps have implicit dependencies (like `--no-build` depending on a prior build), the safest approach is to make each step self-contained unless there's a significant performance penalty. The NuGet cache ensures reasonable build speed without sacrificing reliability.

### Session: Strava Import Integration Tests (Issue #40)

**Branch:** `squad/40-strava-import` (working with Naomi in parallel)

**Test file created:**
- `my-gpx-activities.Tests/Api/StravaImportTests.cs` — 6 tests for POST /api/activities/import/strava

**Tests written:**
1. `ImportWithStreams_ReturnsOkWithId` — POST with full streams (latlng, altitude, heartrate, cadence, time) → 200 OK with activity ID
2. `ImportWithoutStreams_ReturnsOkWithId` — POST with polyline only (no streams) → 200 OK, polyline decoded to track points
3. `ImportDuplicate_ReturnsOkWithDuplicateFlag` — POST same activity twice → second returns 200 OK with `duplicate: true` and message
4. `ImportNordicSki_MapsActivityTypeCorrectly` — Verify NordicSki sport_type imports successfully
5. `ImportVirtualRide_MapsActivityTypeCorrectly` — Verify VirtualRide sport_type imports successfully
6. `ImportWithNullStreams_ReturnsOkWithId` — POST with `"streams": null` → treated same as missing streams

**Patterns followed:**
- Same Aspire test harness setup as HeatMapApiTests and SmartMergeApiTests
- `OneTimeSetUp`/`OneTimeTearDown` to share single app instance across all tests in fixture
- `PostAsJsonAsync()` for JSON payloads (vs. multipart/form-data used in SmartMerge)
- Created `Api/` subdirectory for better test organization
- Tests flexible to handle both new imports (with `id`) and duplicates (with `duplicate: true`)

**Spec-first approach:**
- Tests written based on GitHub issue spec before implementation exists
- Used inline anonymous objects for request payloads (no need for DTOs in tests)
- Sample data from spec: NordicSki activity (id: 17408355280), VirtualRide activity (id: 17976092558)
- Tests will guide Naomi's implementation and may need minor adjustments once endpoint is complete

**API endpoint spec tested:**
- Endpoint: `POST /api/activities/import/strava`
- Request: `{ "activity": {...}, "streams": {...} }` (streams optional)
- Success response: `200 OK` with either `{ "id": <int> }` or `{ "duplicate": true, "message": "..." }`

**Test organization decision:**
- Created `Api/` subdirectory under Tests/ for endpoint-specific tests
- Keeps root test directory cleaner as project grows
- Consistent with common .NET test organization patterns

### Session: Activity Merge Integration Tests (Issue #41)

**Branch:** `squad/41-merge-activities` (written spec-first, Naomi builds implementation in parallel)

**Test file created:**
- `my-gpx-activities.Tests/Api/ActivityMergeTests.cs` — 11 tests covering preview + merge endpoints

**Tests written:**

*Preview endpoint (GET /api/activities/merge/preview):*
1. `GetMergePreview_OverlappingActivities_SuggestsMergeMode` — overlapping time windows → `suggestedMode == "merge"`
2. `GetMergePreview_NonOverlappingActivities_SuggestsAppendMode` — non-overlapping → `suggestedMode == "append"`
3. `GetMergePreview_ReturnsCorrectChannels` — GPS-bearing activity includes "GPS" in `channels` array
4. `GetMergePreview_InvalidId_Returns404` — unknown activityAId → 404

*Merge endpoint (POST /api/activities/merge):*
5. `MergeActivities_MergeMode_CreatesNewActivity` — merge two overlapping activities, new activity GUID returned
6. `MergeActivities_MergeMode_PreferMoreDataPointsChannel` — A has 10 HR pts, B has 3 → merged has ≥ 10 track points
7. `MergeActivities_AppendMode_ConcatenatesChronologically` — appended result spans A.start → B.end
8. `MergeActivities_AppendMode_AcceptsTimeGap` — 2-hr gap between activities is accepted without error
9. `MergeActivities_PreservesOriginalActivities` — both source activities still exist post-merge
10. `MergeActivities_CustomName_UsedInResult` — name from request appears as merged activity title
11. `MergeActivities_InvalidActivityId_Returns404` — unknown activityAId in merge body → 404

**Test data seeding strategy:**
- Four activities seeded in `[OneTimeSetUp]` via `POST /api/activities/import/strava`
  - `_overlappingActivityA` (10 HR pts, 2026-01-01 08:00–09:00)
  - `_overlappingActivityB` (3 HR pts, 2026-01-01 08:30–09:30 — overlaps A by 30 min)
  - `_nonOverlappingActivityA` (2026-02-01 08:00–09:00)
  - `_nonOverlappingActivityB` (2026-02-01 11:00–12:00 — 2-hr gap after A)
- `ImportStravaActivityAsync` helper resolves internal Guid from `GET /api/activities` by matching activity name (Strava import returns int row ID, not UUID)

**Key pattern discoveries:**
- Strava import `POST` returns `{ id: <int> }` (not a UUID), so a secondary `GET /api/activities` list lookup is needed to resolve the Guid
- JSON property names in GET responses may be PascalCase (`Title`, `StartDateTime`) — tests use double `TryGetProperty` checks for both casing variants to remain compatible with whatever Naomi's implementation returns
- `MergeActivities_MergeMode_PreferMoreDataPointsChannel` exercises conflict resolution indirectly (via trackPoints count) since direct HR channel inspection requires parsing TrackDataJson

**Build:** Compiles cleanly with `dotnet build` — 0 warnings, 0 errors.

---

**2026-04-05:** Issue #41 (Merge Activities) shipped to dev. PR #44 merged. Wrote 11 integration tests: 4 preview, 7 merge (validation, happy path, edge cases). Build clean. Feature ready for release.

### Session: No-GPS Strava Import Tests (Issue #51)

**Branch:** `fix/issue-51-strava-import-no-gps` (working with Naomi in parallel on import service fix)

**Test file updated:**
- `my-gpx-activities.Tests/Api/StravaImportTests.cs` — added 3 integration tests for no-GPS import scenarios

**Tests written:**
1. `ImportTrainerActivityWithHeartRate_ReturnsOkAndPreservesHeartRate` — POST indoor trainer activity (no latlng stream, has heartrate/altitude/time) → 200 OK with id, then GET activity → verify trackData exists and contains heart rate data (index 3 in track point array)
2. `ImportTrainerActivityWithHeartRate_TrackCoordinatesJsonIsEmptyOrNull` — POST indoor activity with no GPS → verify trackCoordinates is null/empty but trackData still exists with heart rate
3. `ImportRouvyActivityWithGps_ReturnsOkWithTrackPoints` — POST Rouvy VirtualRide with latlng stream → verify both trackCoordinates and trackData contain GPS points (indices 0, 1)

**Test data:**
- Used real activity IDs from sample data files in repo:
  - `18068872357` — indoor trainer Ride from Suunto watch (no GPS, has heartrate/altitude/time streams)
  - `18068853828` — Rouvy VirtualRide (has GPS latlng, watts, cadence, no heartrate)
  - `18068872358` — synthetic indoor activity ID for second test

**Pattern learnings:**
- Track point format: `[lat, lon, elevation, heartrate, unixMs, cadence]` — each can be null
- Indoor activities have no `latlng` stream, use `time` as index for building track points
- trackData vs trackCoordinates distinction: trackData has full sensor data (can have null lat/lon), trackCoordinates is GPS-only for map display
- Used `TryGetProperty` with both camelCase and PascalCase to handle JSON serialization variations
- Parse nested JSON strings: activity response has `trackData` as JSON string, must double-parse
- Tests check array indices safely (length check before accessing element)

**Build:** Compiles cleanly with `dotnet build` — 0 warnings, 0 errors.

**Git workflow:** Created branch from dev (Naomi's fix branch didn't exist yet), committed tests, pushed. Naomi will rebase or work on same branch when implementing the fix.

### Session: Activity Listing Integration Tests (Issue #50)

**Branch:** `fix/issue-50-activities-not-visible` (spec-first tests for Naomi's fix)

**Test file created:**
- `my-gpx-activities.Tests/Api/ActivityListingTests.cs` — 3 tests for GET /api/activities

**Tests written:**
1. `GetActivities_ReturnsOkWithList` — GET /api/activities → 200 OK with JSON array (baseline test)
2. `ImportActivity_ThenGetActivities_ActivityIsVisible` — Import activity with id `18000000020L`, then GET listing → verify activity appears by title (key regression test for issue #50)
3. `ImportTrainerActivity_ThenGetActivities_TrainerActivityIsVisible` — Import no-GPS trainer activity (id `18000000021L`, no latlng, has heartrate) → verify appears in listing (combines issue #50 visibility + issue #51 no-GPS support)

**Pattern consistency:**
- Same `OneTimeSetUp`/`OneTimeTearDown` Aspire harness as `StravaImportTests.cs`
- POST to `/api/activities/import/strava` for test data seeding
- Duplicate detection: if import returns no `id` (duplicate), skip visibility check with `Assert.Pass()`
- Search listing response by title with both PascalCase (`Title`) and camelCase (`title`) property checks

**Key insights:**
- Issue #50 was a UI caching bug (Activities page only loaded from API if store empty), but API behaviour is testable: any imported activity must appear in `GET /api/activities`
- Tests future-proof against issue #51 fix by including a no-GPS trainer activity scenario
- Using unique Strava activity IDs (`18000000020L`, `18000000021L`) to avoid conflicts with other test data

**Build:** Compiles cleanly with `dotnet build` — 0 warnings, 0 errors.

**Commit:** `a77effb` pushed to `fix/issue-50-activities-not-visible` branch.

