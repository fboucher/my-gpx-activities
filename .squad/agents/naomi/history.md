# History — Naomi

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

### TrackData 6-Element Schema — Cadence (Issue #19)
**Change:** Extended `TrackDataJson` from 5 to 6 elements per trackpoint — added cadence at index 5.

**Schema:** `[lat, lon, elevation_or_null, hr_or_null, unix_ms_or_null, cadence_or_null]`

**Files changed:**
- `Activity.cs`: updated comment to document 6-element schema + backward compat note
- `FitParserService.cs`: `FitDataPoint` record gains `Cadence` property; field def 4 (UINT8, invalid=0xFF) parsed in both normal and compressed-timestamp handlers
- `SmartMergeService.cs`: `fitByTime` filter broadened to `HR.HasValue || Cadence.HasValue`; renamed `EnsureHeartRateExtension` → `EnsureTrackPointExtension` that writes both `<hr>` and `<cad>` extension elements
- `Program.cs`: all three `trackData` array assemblies (GPX import, batch import, smart-merge import) now include `tp.Cadence` at index 5

**Backward compat:** Existing stored 5-element arrays in DB remain as-is. Frontend must do `point.length > 5 ? point[5] : null`.

**GpxParserService:** already parsed `<cad>` from GPX extensions and set `GpxTrackPoint.Cadence` — no changes needed.

### Heat Map API Endpoint (Issue #10, PR #15)
**Endpoint shape:** `GET /api/activities/heatmap?dateFrom=YYYY-MM-DD&dateTo=YYYY-MM-DD&sportTypes=Running,Cycling`
- All query params optional; `sportTypes` is comma-separated, split in handler before calling repository
- Returns `IEnumerable<HeatMapActivity>` — lean response optimized for map rendering (no stats/elevation)

**Model design:** `HeatMapActivity` record with `ActivityId` (Guid), `ActivityName` (string), `SportType` (string), `TrackPoints` (double[][])
- `TrackPoints` is deserialized from `track_coordinates_json` (stored as PostgreSQL jsonb `[[lat,lon],...]`)
- Empty array `[]` when no track data

**Query patterns:**
- Dynamic SQL: build `WHERE` clause conditions list, join with `AND` — avoids ORM, stays consistent with Dapper pattern in codebase
- Date filtering: convert `DateOnly` → `DateTime` UTC; `dateTo` uses exclusive upper bound (`AddDays(1)` on To date)
- Sport type filtering: `activity_type = ANY(@SportTypes)` — Dapper/Npgsql handles string array parameter natively
- Private `HeatMapDto` inner class with snake_case property names for Dapper column mapping
- Only select needed columns (`id, title, activity_type, track_coordinates_json`) — no full row fetch

**git lesson:** In this multi-agent environment, `git checkout` is unreliable due to concurrent branch switching. Use `git worktree` to get an isolated working directory for a specific branch, avoiding race conditions with other agents.

### Smart Merge (Issue #11, PR #16)
**FIT file parsing (binary format):**
- FIT = Flexible and Interoperable Data Transfer; Garmin proprietary binary format
- No official .NET NuGet package available (Dynastream.Fit does not exist on nuget.org)
- Wrote minimal custom binary parser `FitParserService` — ~140 lines; handles all cases needed
- FIT epoch = December 31, 1989 00:00:00 UTC; field 253 timestamps are seconds since this epoch
- Protocol: 14-byte header (header size, protocol ver, profile ver, data size LE, ".FIT" magic, CRC)
- Records start with a header byte: bit7=0 normal, bit7=1 compressed timestamp
  - Normal: bit6=1 definition message, bit6=0 data message; bits3-0 = local message type (0-15)
  - Compressed: bits6-5 = local type (0-3), bits4-0 = time offset (rollover modulo 32)
- Definition messages: reserved byte, architecture (0=LE, 1=BE), global msg num, field count, field defs (num, size, base_type)
- Global message 20 (record) contains: field 253=timestamp (UINT32), field 3=heart_rate (UINT8, 0xFF=invalid)
- Suunto FIT files use big-endian (`architecture=1`) for record messages
- Compressed timestamp records do NOT include field 253 in the payload (must skip it when iterating fields)
- Sample file `Ride_heart-rate.fit`: 1718 HR records, 2026-02-24 12:40-13:09 UTC, 75-96 bpm

**Smart merge algorithm:**
- Build sorted list of FIT points with HR data → binary search for nearest timestamp per GPX trackpoint
- O(log n) matching; check lo-1, lo, lo+1 candidates after binary search
- Tolerance (default 5s) rejects matches too far apart
- Sample result: 1680/1680 GPX trackpoints matched successfully

**Key files:**
- `my-gpx-activities.ApiService/Services/FitParserService.cs` — FIT binary parser
- `my-gpx-activities.ApiService/Services/SmartMergeService.cs` — GPX+FIT merge logic
- `my-gpx-activities.ApiService/Program.cs` — service registration + POST endpoint

**Endpoint:** `POST /api/activities/smart-merge` (multipart: `gpx` + `fit` files) → returns `application/gpx+xml`

### Smart Merge Import Endpoint (Issue #11, PR #16)
**New endpoint:** `POST /api/activities/smart-merge/import` — combines smart merge with activity import in one operation

**Implementation pattern:**
1. Accept GPX + FIT files (same validation as existing `/api/activities/smart-merge`)
2. Call `SmartMergeService.MergeAsync(gpxStream, fitStream)` → returns string (merged GPX XML)
3. Convert merged XML string to Stream: `new MemoryStream(System.Text.Encoding.UTF8.GetBytes(mergedGpx))`
4. Parse merged GPX using `GpxParserService.ParseGpxAsync(mergedStream)`
5. Build Activity model and save via `ActivityRepository.CreateActivityAsync` (identical to `/api/activities/import`)
6. Return 201 Created with activity summary (same response shape as import endpoint)

**Why this endpoint:** Users want heart-rate enriched activities saved to the database, not just the merged XML file. This eliminates the manual step of downloading the merged GPX and re-uploading via import.

**Key implementation detail:** SmartMergeService returns a string, but GpxParserService expects a Stream — use MemoryStream with UTF-8 encoding to bridge them.

**Location:** `my-gpx-activities.ApiService/Program.cs` line 261-339 (immediately after existing smart-merge endpoint)

### ActivityDetail Page: Fetch Full Data When Store Has Summary Only
**Problem:** ActivityDetail showed empty charts/map when activity was previously loaded via the list page. Root cause: `/api/activities` (list endpoint) returns summary data WITHOUT `TrackData` and `TrackCoordinates`, but `ActivityDetail.razor` checked the store first and used the incomplete data directly.

**Fix applied:**
1. Changed condition in `ActivityDetail.razor` line 296 from `if (storedActivity == null)` to `if (storedActivity == null || storedActivity.TrackData == null)`
2. Added `ActivityStore.AddOrUpdate()` method that removes existing entry before adding new one (prevents duplicates)
3. Updated detail page to call `AddOrUpdate()` instead of `Add()` when caching full data from API

**Behavior after fix:**
- Store check is used only when full data (TrackData present) is cached
- When store has summary-only data, we fetch from `/api/activities/{id}` to get full data including TrackData and TrackCoordinates
- AddOrUpdate ensures the store is updated with full data, not duplicated
- Subsequent visits to same detail page use cached full data (no redundant API calls)

**Files changed:**
- `my-gpx-activities/webapp/Components/Pages/ActivityDetail.razor` — updated LoadActivityAsync condition and method call
- `my-gpx-activities/webapp/Services/ActivityStore.cs` — added AddOrUpdate method
