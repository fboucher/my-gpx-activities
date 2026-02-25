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
