# Team Decisions

---

### 2026-02-25: Dev container setup for zero-install development

**By:** Bobbie | **Issue:** #37 | **PR:** #38

- Use `mcr.microsoft.com/devcontainers/base:ubuntu` + dotnet feature `version: "10.0"` (pre-built images max out at .NET 9)
- Include `docker-in-docker` — Aspire needs it to spin up PostgreSQL, pgAdmin, etc.
- Forward ports: 15888 (Aspire dashboard), 15889 (API), 15890 (pgAdmin), 8080, 8081
- Install Aspire workload via `postCreateCommand` (adds ~5 min to first startup — acceptable)
- Add Codespaces badge to README for one-click cloud dev

---

### 2026-02-25: README improvements — screenshots, prerequisites, tech stack, API docs, roadmap

**By:** Holden | **Issue:** #36 | **PR:** #35

- `docs/screenshots/` placeholder created; contributions welcome but not required
- Prerequisites section placed before Getting Started (explicit dependency listing reduces friction)
- Tech stack badges (.NET 10, Blazor Server, PostgreSQL 16, Docker, MudBlazor) placed after description
- Swagger tip added after port listing in Running Locally
- Roadmap section placed before Contributing; no dates/owners to manage expectations

---

### 2026-02-25: Squad state (.squad/) committed to dev only — never to feature branches

**By:** fboucher (via Coordinator)

Squad state files (history.md, decisions inbox, orchestration logs, session logs) must only be committed on the `dev` branch. Committing them on feature branches pollutes PR diffs with unrelated file changes.

**Rules for all agents:**
- On a feature branch: write `.squad/` changes to disk only — do NOT `git add .squad/` or commit them
- Scribe: always commit `.squad/` state on `dev`. If currently on a feature branch, switch to dev first:
  ```bash
  git stash           # if any uncommitted work on feature branch
  git checkout dev
  git add .squad/
  git commit -m "chore: squad state update [skip ci]"
  git checkout -      # return to feature branch
  git stash pop       # if stashed
  ```
- Feature branch commits must contain only the actual work files

**Why:** Every PR was showing 30+ changed files due to squad state. PRs should only show the files that matter for review.
# Decisions Log

## Issue #23: Chart Hover → Map Sync Bug

**Author:** Alex  
**Date:** 2026-02-25  
**Issue:** #23

### Problem
When hovering through a 30-minute activity timeline, the map's blue sync marker only moved halfway through the route. At 29 minutes (97% of chart duration), the marker was at the route midpoint instead of 97% along the GPS track.

### Root Cause
Charts are downsampled from ~3000 points to 500 for performance. The hover handler used the downsampled chart index (0-499) directly as an index into the original `trackData` array. When hovering at chart position 485/500 (97%), it accessed `trackData[485]` instead of `trackData[2910]`.

### Solution
Store the downsampling index map for each chart (`dsIndexMaps.elevation = [0, 6, 12, 18, ..., 2994]`). When hovering at chart index `i`, look up `indexMap[i]` to get the original trackData index, then use `trackData[indexMap[i]][0,1]` for lat/lon.

**Changes:**
- `chartSync.js`: Added `dsIndexMaps` object to store index mappings for all four charts
- Modified `downsample()` to document that it returns `{ data, indices }` (already did this)
- Updated `makeHoverHandler(charts, indexMap)` to accept index map parameter
- Pass `indexMap` when creating handler: use elevation/pace/hr/cadence map (all identical since same source data)

### Why Not Alternative Fixes?
- **Interpolate lat/lon by percentage**: Would require computing cumulative distance at every point — expensive and inexact when GPS jitter causes non-monotonic distances
- **Remove downsampling**: Would make charts sluggish on 10k+ point activities

---

## Issue #5: Activities Page UX

**Author:** Alex  
**Date:** 2026-02-25  
**Issue:** #5

### Requirements
1. Add filters to find activities easily
2. Move action buttons to left side
3. Highlight rows on hover

### Design Decisions

#### Filter System
**Choice:** Text search + activity type dropdown (not date range, distance range, etc.)

**Rationale:**
- Most users search by name ("Sunday run") or type ("Cycling")
- Advanced filters (date/distance/elevation) can come later if needed
- Two filters provide 80% value with minimal UI complexity

**Implementation:**
- `searchText` field with 300ms debounce to avoid re-filtering on every keystroke
- `selectedActivityType` dropdown populated from distinct types in current activity list
- `FilteredActivities` computed property chains both filters
- Wrapped in `MudPaper` with elevation for visual separation

#### Button Position
**Choice:** Move "View" button from right column to left column (first column)

**Rationale:**
- On narrow screens, right-aligned buttons require horizontal scroll
- Left position makes primary action immediately visible
- Users scan left-to-right; seeing action button first aids discoverability

#### Row Hover
**Choice:** Use MudBlazor's built-in `Hover="true"` on `MudDataGrid`

**Rationale:**
- Zero custom CSS required
- Consistent with MudBlazor design language
- Automatic ARIA accessibility support

#### Filter Persistence
**Decision:** No persistence — filters reset on page reload

**Rationale:**
- Activities page is session-level UI, not long-term workspace
- Users typically visit page, filter, view one activity, then leave
- If usage patterns show users repeatedly applying same filters, can add localStorage later

### Future Enhancements (Not Implemented)
- Date range filter (calendar picker)
- Distance/elevation sliders
- Sort by distance/elevation gain
- Multi-select activity types
- "Save filter" presets
- URL query params for shareable filtered views

### Testing Approach
Both fixes are visual/interactive — no unit tests added.

**Manual testing:**
- #23: Load activity with 30+ min duration, hover through chart timeline, verify map marker moves along entire route at correct position
- #5: Add multiple activity types, test text search (partial match), test type dropdown, test clearing filters, verify row hover styling, check button position on narrow viewport

**Regression risk:** Low. Changes are isolated to two pages (`ActivityDetail.razor` via `chartSync.js` and `Activities.razor`). No shared components or backend APIs modified.

---

## CI Test Fix: Remove --no-build Flag

**Author:** Amos  
**Date:** 2026-02-25  
**Issue:** CI test report generation failure

### Problem
CI workflow `.github/workflows/ci.yml` was failing with:
```
No test report files were found matching **/test-results.trx
```
The `dorny/test-reporter@v1` action could not locate the TRX file generated by `dotnet test`.

### Root Cause
The `dotnet test` command used `--no-build`, creating an implicit dependency on the prior `dotnet build` step completing correctly. If the build step didn't produce test binaries in the right location or state, `dotnet test --no-build` would:
1. Fail to find the test binary
2. Exit with an error before generating the TRX file
3. Leave the reporter with nothing to parse

Tests pass locally when run with `dotnet test` (without `--no-build`), confirming the root cause.

### Solution
Removed the `--no-build` flag from line 37 of `.github/workflows/ci.yml`:

**Before:**
```bash
dotnet test my-gpx-activities/my-gpx-activities.Tests --no-build --configuration Release --filter "FullyQualifiedName~FitParserServiceTests" --logger "trx;LogFileName=test-results.trx" --logger "console;verbosity=detailed"
```

**After:**
```bash
dotnet test my-gpx-activities/my-gpx-activities.Tests --configuration Release --filter "FullyQualifiedName~FitParserServiceTests" --logger "trx;LogFileName=test-results.trx" --logger "console;verbosity=detailed"
```

This makes `dotnet test` self-contained—it ensures test binaries are built (using cached NuGet packages) before running tests and generating the TRX report.

### Trade-offs
- **Pro:** Reliable TRX generation; no implicit dependency on prior build step.
- **Con:** Slight extra build time (mitigated by NuGet cache; acceptable for CI reliability).

---

## Docker Hub Publish Strategy

**Author:** Bobbie  
**Date:** 2026-02-25  
**Issue:** #32  
**Status:** Implemented

### Context
Issue #32 required publishing Docker images to Docker Hub on release and beta images on every push to `dev`. The project has two deployable services (ApiService + webapp) and no existing Dockerfiles.

### Decision: Two separate image repositories
Rather than a single `fboucher/my-gpx-activities` image, two images are published:

- `fboucher/my-gpx-activities-api` — the Minimal API backend
- `fboucher/my-gpx-activities-webapp` — the Blazor Server frontend

**Rationale:**
The project is a multi-service Aspire application. A single image cannot represent both services. Naming them with `-api` and `-webapp` suffixes keeps the Docker Hub namespace clean and makes it obvious which image serves which role. Docker Compose consumes both.

### Decision: Build context at solution root
Both Dockerfiles live inside their respective project folders but use `./my-gpx-activities` as the Docker build context (set in the workflow). This allows the `COPY` instructions to reach the shared `my-gpx-activities.ServiceDefaults` project without duplicating it.

### Decision: Beta versioning via git tag increment
Beta tag computation:
1. `git describe --tags --abbrev=0` → latest release tag (e.g. `v0.2.0`)
2. Strip `v`, split on `.`, increment patch component
3. Append `-beta` → `v0.2.1-beta`
4. Fall back to `v0.0.1-beta` if no tags exist

No `latest` tag is written for beta builds to avoid polluting the stable `latest` pointer.

### Secrets required
| Secret | Purpose |
|--------|---------|
| `DOCKER_USERNAME` | Docker Hub login (must be set in repo settings) |
| `DOCKER_PAT` | Docker Hub personal access token (write scope) |

---

## OSS Documentation & Project Files

**Author:** Holden  
**Date:** 2026-02-25  
**PR:** #35  
**Branch:** `squad/readme-oss`  
**Status:** Awaiting review → merge to dev

### Decision: README Badge & Vibe-Coding Note

**Vibe-Coding Note Tone:**
Placed immediately below badges/title:
> "This project was built with heavy AI assistance. The author knows how to code but embraced AI-assisted development throughout. The code is production-ready and follows modern .NET best practices."

**Rationale:**
- Honest without self-deprecation
- Acknowledges AI use transparently
- Reassures users of code quality
- Frames AI as a tool choice, not a limitation

**Badge Order & Content:**
```
CI (ci.yml) → Docker Publish (docker-publish.yml) → Docker Beta (docker-beta.yml) → Docker Hub pulls → License
```

**Rationale:**
- Left-to-right flow: build health → deployment health → artifact accessibility → legal
- All workflows linked to GitHub Actions for transparency
- Docker Hub badge uses pull count (user interest signal)
- MIT license badge for legal clarity

**README Structure:**
1. Title + Badges
2. Brief description
3. Vibe-coding note
4. Features
5. Getting Started (local + Docker Compose combined)
6. Development (links to AGENTS.md)
7. Contributing (links to CONTRIBUTING.md)
8. License (links to LICENSE)

**Rationale:**
- Readers see project health immediately
- AI transparency at top prevents surprises
- Docker Compose co-located with local setup (same section = same intent)
- AGENTS.md is source of truth for code style—no duplication
- Concise; readers can drill into AGENTS.md or CONTRIBUTING.md for details

**Contributing.md Guidelines:**
- Branch naming: `squad/*`, `feature/*`, `fix/*`
- Test locally with `dotnet test` before pushing
- PR against `dev` (not `main`)
- Code style via AGENTS.md link (single source of truth)

**Rationale:**
- Squad branch naming matches internal workflow
- Simple, frictionless (fork → branch → test → PR)
- Avoids duplicating AGENTS.md—one source of truth prevents style drift

**CODE_OF_CONDUCT.md:**
Contributor Covenant 2.1 standard text, contact via GitHub Issues.

**Rationale:**
- Industry standard; contributors recognize it
- GitHub Issues is natural contact point for a GitHub-hosted project

**Issue Templates:**
Two templates: `bug_report.md` and `feature_request.md`

**Rationale:**
- Guides issue creators to provide actionable info
- Reduces back-and-forth for common question categories

---

## ActivityDetail: Fetch Full Data When Store Has Summary Only

**Author:** Naomi  
**Date:** 2026-02-25  
**Status:** Implemented

### Context
`ActivityDetail.razor` showed empty charts and map when users navigated from the Activities list page to a detail page. The bug occurred because:

1. `/api/activities` (list endpoint) returns summary data without `TrackData` or `TrackCoordinates`
2. Activities list page populates `ActivityStore` with these summary-only objects
3. `ActivityDetail.razor` checks store first via `ActivityStore.GetById(Id)`
4. When found in store, the page used the data directly — but `TrackData = null` meant no charts/map rendered

### Solution
Modified `ActivityDetail.razor` to **always fetch from API when TrackData is missing**, even if the activity exists in the store. This ensures full data (with TrackData and TrackCoordinates) is loaded for the detail page.

**Changes:**

**ActivityDetail.razor (line 296):**
```csharp
// Before:
if (storedActivity == null)

// After:
if (storedActivity == null || storedActivity.TrackData == null)
```

This condition now fetches from API in two scenarios:
- Activity not in store at all (original behavior)
- Activity in store but only has summary data (new fix)

**ActivityStore.cs (new method):**
Added `AddOrUpdate` method to prevent duplicate entries:
```csharp
public void AddOrUpdate(ActivitySummary activity)
{
    var existing = GetById(activity.Id);
    if (existing != null)
    {
        _activities.Remove(existing);
    }
    _activities.Add(activity);
}
```

Changed detail page to call `AddOrUpdate()` instead of `Add()` when caching full data from API.

### Behavior After Fix
1. **First visit to list page:** Store populated with summary data (no TrackData)
2. **Navigate to detail page:** Condition detects `TrackData == null` → fetches full data from `/api/activities/{id}`
3. **Store updated:** `AddOrUpdate()` replaces summary entry with full data
4. **Subsequent detail visits:** Store has full data → no API call needed (fast path works correctly)

### Rationale
- **Minimal change:** Only modified the condition and added one method
- **Performance:** Store caching still works when full data is present
- **No API changes:** Backend endpoints remain unchanged
- **No duplicates:** AddOrUpdate ensures clean store state
- **Backward compatible:** Existing `Add()` method unchanged; edit dialog still works

### Alternative Considered
Could have changed list endpoint to always return full data — rejected because:
- List page doesn't need TrackData (only displays summary cards)
- Would increase payload size and DB load unnecessarily
- Current approach is more efficient (lazy loading pattern)

---

## Activity Persistence Strategy

**Author:** Naomi  
**Date:** 2026-02-25  
**Issue:** #25  
**PR:** #29

### Context
Activities were being saved to PostgreSQL on import, but the webapp's Activities page only displayed activities from the in-memory `ActivityStore` singleton. After an app restart, `ActivityStore` was empty, so activities disappeared from the list (but were still visible in the heat map, which reads directly from the database).

### Decision
Implement a hybrid approach where the API (PostgreSQL) is the source of truth, but the in-memory `ActivityStore` is used as a session cache:

1. **Created `ActivityApiClient` service** with methods to fetch all activities and individual activities from the API
2. **Activities page loads from API first** on `OnInitializedAsync()`, then populates `ActivityStore` as a cache
3. **ActivityDetail page checks store first** (fast path), then fetches from API if not found
4. **Import workflows continue to add to store** after successful API calls for immediate display
5. **Graceful fallback**: If API call fails, pages fall back to reading from the in-memory store

### Alternatives Considered
1. **Remove ActivityStore entirely** — Would require API call on every page navigation, poor UX
2. **Persist ActivityStore to browser storage** — Adds complexity, data could become stale
3. **Background sync job** — Over-engineered for this use case

### Rationale
- **API as source of truth** ensures data persists across restarts
- **In-memory cache** provides fast navigation without repeated API calls
- **Backward compatible** with existing Import page logic
- **Simple and pragmatic** for a single-user app

### Trade-offs
- **Cold start latency:** First page load after restart requires API call (acceptable for this app)
- **Cache invalidation:** Store isn't automatically updated if activities are deleted/modified elsewhere (not a concern for single-user app with no external modifications)

### Implementation Files
- `webapp/Services/ActivityApiClient.cs` (new)
- `webapp/Components/Pages/Activities.razor` (modified)
- `webapp/Components/Pages/ActivityDetail.razor` (modified)
- `webapp/Program.cs` (registered ActivityApiClient)

### Verification
Build succeeded. Activities now persist across app restarts and are immediately visible after import.

---

## Issue #19: Activity Detail Chart Sync

**Author:** Alex  
**Date:** 2026-02-25

### Chart order on page
Order: Elevation → Pace → Heart Rate → Cadence  
Rationale: Pace is GPS-derived like elevation; HR and Cadence are sensor data grouped at bottom.

### Pace Y-axis reversed
Lower numeric value = faster pace = top of chart.  
Rationale: Matches runner/cyclist mental model (going faster = going up).

### Pace outlier clamping at 30 min/km
Any pace > 30 min/km treated as null.  
Rationale: GPS jitter at stalled points produces nonsensical instant speeds; 30 min/km (~2 km/h) is safe upper bound for walking.

### Backward compatibility for 5-element TrackData
All JS reads `p.length > 5 && p[5] != null` before accessing cadence. Razor checks `p.Length > 5 && p[5] != null`.  
Rationale: Activities imported before schema extension have 5-element arrays; cadence chart won't show for those—no migration needed.

### Sync marker color changed to blue (#2563eb)
Map sync dot is now Tailwind blue-600.  
Rationale: User explicitly requested blue. Red could be confused with warning/error marker.

### spanGaps: true on pace and cadence charts
Gaps (null values) are bridged rather than breaking the line.  
Rationale: Sparse cadence data produces many nulls—bridging keeps chart readable. Rolling average fills most pace gaps.

---

## Issue #11: Smart Merge UI Design

**Author:** Alex  
**Date:** 2025-01-XX

Added separate "Smart Merge" section to Import page combining GPX + FIT files for HR enrichment.

### Design choices
1. **Independent workflow:** Visually and functionally separate from batch GPX import
2. **Visual separation:** `MudStack` with centered "— or —" text; avoided `MudDivider` analyzer warning
3. **Color scheme:** `Color.Secondary` for Smart Merge UI to distinguish from primary import workflow
4. **File selection:** Two independent `MudFileUpload T="IBrowserFile"` (single file each) with accept filters and state isolation
5. **API call:** `POST /api/activities/smart-merge/import` with multipart form fields (`gpx`, `fit`)
6. **Response handling:** Same `ActivitySummary` shape as regular import

### Rationale
- Separation of concerns keeps merge workflow distinct and intuitive
- Consistent with existing multipart upload patterns
- Visual clarity via color coding
- State isolation prevents cross-contamination

---

## Branch Protection: Copilot Config Files Blocked from main

**Author:** Bobbie  
**Date:** 2026-02-25  
**Status:** Implemented

### Context
Coordinator directive required `.squad/`, `.copilot/`, `.github/copilot-instructions.md` blocked from `main` in enforceable way without breaking `dev` workflow.

### Decision
Extended existing `squad-main-guard.yml` workflow to block:
- `.copilot/` — Copilot/MCP configuration
- `.github/copilot-instructions.md` — Copilot prompt/instructions file

Existing workflow already covered `.squad/`, `.ai-team/`, `.ai-team-templates/`, `team-docs/`, `docs/proposals/`.

### Why not .gitignore on main?
Adding to `.gitignore` on `main` would conflict on every merge from `dev`—CI guard avoids this.

### Enforcement
- Triggers on `pull_request` and `push` to `main`, `preview`, `insider`
- Fails with actionable error message
- Removals (deleting forbidden files) explicitly allowed
- Dev workflow unaffected; `.squad/` and `.copilot/` remain fully tracked on `dev` and feature branches

---

## Smart Merge Import Endpoint

**Author:** Naomi  
**Date:** 2025-02-25  
**Status:** Implemented

### Context
Users previously needed 3 steps to merge GPX+FIT and save enriched activity:
1. POST to `/api/activities/smart-merge` (get merged GPX XML)
2. Download result
3. POST to `/api/activities/import` (save to DB)

### Decision
Added `POST /api/activities/smart-merge/import` endpoint combining both:
- Accepts GPX + FIT files (same validation)
- Calls SmartMergeService → GpxParserService → ActivityRepository
- Returns 201 Created with full activity summary

### Implementation details
- **Location:** `my-gpx-activities.ApiService/Program.cs` lines 261-339
- **Key bridge:** Convert SmartMergeService string to Stream: `new MemoryStream(System.Text.Encoding.UTF8.GetBytes(mergedGpx))`
- **Response shape:** Identical to `/api/activities/import` (201 Created)
- **Backward compat:** Original `/api/activities/smart-merge` unchanged (returns XML only)

### Rationale
- Reduces user friction: one API call instead of three
- Reuses existing validation, parsing, repository logic
- Consistent with existing patterns
- Keeps original endpoint for XML-only use cases

---

## TrackData 6-Element Schema + Cadence

**Author:** Naomi  
**Date:** 2026-02-25  
**Context:** Issue #19

### New TrackData schema
`TrackDataJson` now stores **6 elements** per trackpoint:

```
[lat, lon, elevation_or_null, hr_or_null, unix_ms_or_null, cadence_or_null]
  0    1         2               3              4                 5
```

| Index | Field | Type | Notes |
|-------|-------|------|-------|
| 0 | Latitude | number | degrees |
| 1 | Longitude | number | degrees |
| 2 | Elevation | number \| null | metres |
| 3 | Heart rate | number \| null | bpm |
| 4 | Timestamp | number \| null | Unix ms (UTC) |
| 5 | Cadence | number \| null | raw steps/min from FIT field def 4 |

### Backward compatibility ⚠️
Activities imported **before this change** have **5-element arrays** in DB (no cadence). No migration happening.

**Frontend must handle both lengths:**
```js
const cadence = point.length > 5 ? point[5] : null;
```

Always check `point.length` before accessing index 5.

### Cadence source
- **FIT file** (smart-merge import): field def `4` in global message `20` (record), steps/min
- **GPX file** (direct import): parsed from `<gpxtpx:cad>` if present, `null` otherwise

### Pace
Pace is **not** stored in TrackData. Compute in frontend from lat/lon (0, 1) + timestamp (4).

---

## Issue #41: Activity Merge — ID type, point alignment, stats strategy

**Author:** Naomi  
**Date:** 2026-04-05  
**Issue:** #41 | **PR:** #44

### ID Type
The spec called for `int ActivityAId / ActivityBId` in `MergeRequest`, but the `activities` table and all existing models use `Guid` as the primary key. **Decision: use `Guid`** in `MergeRequest` to stay consistent with the rest of the codebase. Frontend passes GUIDs.

### Merge Mode — Point Alignment
For "merge" mode (overlapping time ranges), each channel is taken from the activity with more data points for that channel. Implementation uses **array-index alignment**: point `i` in the merged output corresponds to point `i` in the GPS-spine activity. This works correctly when both activities have the same number of trackpoints (typical for overlapping activities recorded with the same device). If lengths differ, the merged output is trimmed to the GPS-spine length.

This is a pragmatic simplification. Timestamp-based alignment would be more accurate but significantly more complex — deferred unless fboucher requests it.

### Stats for Merged Activity
Stats (distance, elevation, speed) are **inherited** from source activities rather than recomputed from raw GPS points. This avoids a full haversine pass over merged trackpoints at insert time. Recomputation can be added later if accuracy becomes a concern.

---

## Issue #41: Merge UI — navigation and API client patterns

**Author:** Alex  
**Date:** 2026-04-05  
**Issue:** #41 | **PR:** #44

### NavMenu Integration
The `/merge` page is a transient workflow page (only reachable via activity list after selecting 2 items). It was intentionally **not** added to the `NavMenu` — consistent with other utility pages (e.g., `/import`, `/activities/{id}`). Navigation happens from within the app rather than from the sidebar.

### API DTOs as Inner Records
New API DTOs (`MergePreviewDto`, `MergeRequest`, `MergeResponse`) were added as inner `record` types on `ActivityApiClient`, following the existing pattern of `ActivityTypeDto`. This keeps all API shapes co-located with the HTTP client and avoids separate DTO files for lightweight contract types.

---

## Issue #40: Test Organization with Api/ Subdirectory

**Author:** Amos  
**Date:** 2026-04-05  
**Issue:** #40  
**Context:** Creating integration tests for API endpoints

### Decision
Create an `Api/` subdirectory under `my-gpx-activities.Tests/` for API endpoint-specific integration tests.

**Structure:**
```
my-gpx-activities.Tests/
├── Api/
│   ├── ActivityMergeTests.cs
│   └── StravaImportTests.cs
├── HeatMapApiTests.cs
├── SmartMergeApiTests.cs
├── FitParserServiceTests.cs
└── WebTests.cs
```

### Rationale
1. **Scalability**: As more API endpoints are added, test directory avoids clutter
2. **Organization**: Grouping related tests makes structure clearer
3. **Consistency**: Common .NET convention to organize tests by type/layer
4. **Future-proofing**: Leaves room for `Services/`, `Parsers/`, etc.

### Migration Path
Existing API tests (`HeatMapApiTests.cs`, `SmartMergeApiTests.cs`) can optionally move to `Api/` in future refactoring. No mandate during transition.

### Notes
- Namespace: `my_gpx_activities.Tests.Api` matches directory structure
- Build handles subdirectories automatically
- Applies only to new tests; no mandate to migrate existing tests

---

### 2026-04-05: Strava Import Support for No-GPS Activities

**By:** Naomi | **Issue:** #51 | **PR:** #52

Indoor/trainer activities from Strava often lack GPS (`latlng` stream absent) but contain valuable metrics (heartrate, cadence, altitude). The fix makes GPS optional:

- When `latlng` absent, determine point count from any available stream (priority: latlng → time → altitude → heartrate → cadence)
- Build 6-element track points with null lat/lon, preserving metrics
- Filter `track_coordinates_json` to non-null GPS only for heatmap compatibility
- Result: empty array for no-GPS activities; graceful degradation
- No database schema changes needed
- Tests cover both GPS and no-GPS sample files

---

### 2026-04-05: Footer implemented with MudPaper (fixed position) instead of MudAppBar Bottom

**By:** Alex | **Issue:** #39

`MudAppBar` does not expose `Bottom="true"` property in current MudBlazor version. Solution:
- Use `MudPaper` with inline style: `position:fixed;bottom:0;left:0;right:0;z-index:1300`
- Visually equivalent and compiles cleanly
- Future: consider migrating if MudBlazor adds `Bottom` property

---

### 2026-04-05: README Improvements — screenshots, prerequisites, tech stack, API docs, roadmap

**By:** Holden | **Issue:** #36 | **PR:** #35

Five improvements to README:
- **Screenshots placeholder:** New `docs/screenshots/README.md` with contributor guidelines
- **Prerequisites section:** .NET 10, Docker, Aspire workload before Getting Started
- **Tech stack badges:** .NET 10, Blazor Server, PostgreSQL 16, Docker, MudBlazor after description
- **Swagger tip:** Added 💡 callout at `/swagger` endpoint location
- **Roadmap section:** Five checkbox items before Contributing (no dates/owners to manage expectations)

---

### 2026-02-25: Dev Container Setup for Zero-Install Development

**By:** Bobbie | **Issue:** #37 | **PR:** #38

GitHub Codespaces + VS Code Dev Containers:
- Base: `mcr.microsoft.com/devcontainers/base:ubuntu` + dotnet feature `version: "10.0"` (pre-built images max out at .NET 9)
- Include `docker-in-docker` for Aspire orchestration
- Forward ports: 15888 (dashboard), 15889 (API), 15890 (pgAdmin), 8080, 8081, 15000, 15001, 15002
- VS Code extensions: C# Dev Kit, OmniSharp, Docker, GitHub Copilot
- Aspire workload via `postCreateCommand` (~5 min first startup)
- Codespaces badge in README for one-click cloud dev

---

### 2026-02-25: Strava Import Design

**By:** Naomi | **Issue:** #40

External Strava sync:
- **Flexible requests:** Use `JsonElement` instead of SDK models for forward compatibility
- **Dual track data:** Support both streams (full data) and polyline fallback (GPS only)
- **Polyline decoder:** Inline implementation (~20 lines), no NuGet package
- **Duplicate detection:** Query by title + start_date (no `strava_id` column needed)
- **Import errors table:** Audit log for rejected imports; enables retry feature
- **Sport type mapping:** Static dictionary with regex fallback for new sport types
- Open question: Should we add `strava_id` column for future dedup improvement?

---

### 2026-04-05: No-GPS Strava Import Test Strategy (Issue #51)

**By:** Amos | **Date:** 2026-04-05 | **Context:** Issue #51

Three integration tests for StravaImportService:
1. **ImportTrainerActivityWithHeartRate_ReturnsOkAndPreservesHeartRate** — Verifies HR data preserved when no GPS
2. **ImportTrainerActivityWithHeartRate_TrackCoordinatesJsonIsEmptyOrNull** — Ensures null/empty coords for no-GPS
3. **ImportRouvyActivityWithGps_ReturnsOkWithTrackPoints** — Regression test for GPS activities

Test approach: real activity IDs from sample-data/, dual JSON property checks (camelCase + PascalCase), array validation

---

### 2026-06-13: Version info service lives in webapp

**By:** Naomi | **Issue:** #39

`AppVersionService` interface and implementation live in `my-gpx-activities/webapp/Services/` (not shared library). Version display is a UI concern.

Interface contract:
```csharp
public record AppVersionInfo(string Version, string Build)
{
    public string Display => $"Version: {Version}+{Build}";
}
```

Build value resolution order:
1. `GITHUB_RUN_ID` env var (CI/CD)
2. `AssemblyInformationalVersion` suffix
3. Fallback: `"dev"`

Registered as singleton in `webapp/Program.cs`.

---

### 2026-04-12: MudBlazor Grid Sorting Strategy

**Date:** 2026-04-12  
**Author:** Alex (Frontend Dev)  
**Context:** Issue #56 - Activities not sorted by date

## Problem

Activities page was displaying activities in inconsistent order. The API returns activities sorted by `start_date_time DESC` (newest first), but the MudDataGrid was allowing users to override this order through interactive column sorting.

## Root Cause

`MudDataGrid` had `SortMode="Multiple"` and `SortBy` attributes on all PropertyColumns, enabling clickable column headers that could re-sort the data client-side.

## Decision

**Disable MudDataGrid sorting entirely** when displaying API data that should maintain a specific order.

- Set `SortMode="None"` on MudDataGrid
- Remove all `SortBy` attributes from PropertyColumns
- Grid displays data in the exact order provided by the backend

## Rationale

1. **Single source of truth**: The API/backend owns the sort logic, especially when pagination or database-level sorting is involved
2. **Prevents user confusion**: Users don't expect to be able to re-sort a chronological activity list
3. **Simplicity**: No need to synchronize client and server sort state
4. **Performance**: Avoids client-side re-sorting of large datasets

## When to Use Interactive Sorting

Consider enabling `SortMode="Multiple"` when:
- Data is fully loaded client-side (no pagination)
- Users benefit from sorting by different columns (e.g., a product catalog)
- There's no canonical backend sort order that must be preserved

## Pattern for Future Grids

**For activity/chronological lists:** `SortMode="None"`, let API control order  
**For reference/lookup data:** `SortMode="Multiple"` with `SortBy` attributes for flexibility

## Files Changed

- `webapp/Components/Pages/Activities.razor` - MudDataGrid configuration

## Related

- Issue #56
- PR #61

---

### 2026-04-12: Reuse Existing /api/statistics/by-sport for Activity Detail Comparisons

**Date:** 2026-04-12  
**Issue:** #4 — Mixing Statistics  
**Author:** Naomi (backend) + Alex (frontend)  
**PR:** #64

## Context
Issue #4 required showing how an activity's speed metrics compare to global averages for that sport type. The initial plan suggested creating a new endpoint `GET /api/statistics/sport-averages/{sportType}`.

## Decision
**Reused existing `/api/statistics/by-sport` endpoint** instead of creating a new single-sport endpoint.

## Rationale

### Why reuse?
1. **Already exists**: The endpoint was built for issue #3 (global statistics page) and returns all needed data: `AverageSpeedMs` and `MaxSpeedMs` per sport
2. **Small dataset**: Typically < 20 sport types per user — fetching all sports is cheap (< 1KB response)
3. **Client-side filtering**: Simple `allStats.FirstOrDefault(s => s.SportName == activity.ActivityType)` is fast and efficient
4. **Future-proof**: If we cache sport statistics client-side later, multiple pages can reuse the same cached data

### Why NOT create a new endpoint?
1. **Redundant query**: `SELECT AVG(avg_speed), MAX(max_speed) FROM activities WHERE activity_type = @sportType` is a subset of what `/api/statistics/by-sport` already does
2. **API surface area**: Adding endpoints increases maintenance burden — prefer reuse when data needs overlap
3. **No performance gain**: Single-sport query would save ~500 bytes but adds a round-trip for setup/teardown

## Implementation
- **Frontend**: Added `GetStatisticsBySportAsync()` to `ActivityApiClient` to call existing endpoint
- **Backend**: No changes needed — existing `GetStatisticsBySportAsync()` in `ActivityRepository` already does the work
- **UI**: Conditionally show comparison only when `sportStats.TotalActivities > 1` (meaningful baseline exists)

## Lessons
- Always audit existing endpoints before designing new ones
- Small datasets (< 100 records) favor client-side filtering over creating specialized queries
- Feature infrastructure from prior issues can serve new features — good API design compounds

---

### 2026-04-13: Concurrent Agent Branches and Silent Merge Artifacts

**Date:** 2026-04-13  
**Author:** Alex (Infrastructure/DevOps)  
**Context:** v0.3.2 sprint PR conflict resolution (PRs #61, #62)

## Pattern Observed

During v0.3.2, seven agents ran concurrently. All branched from the same state of `dev`. Five PRs merged cleanly. Two (`squad/55-final` / PR #62 and `squad/56-fix` / PR #61) were blocked because two subsequent PRs (#63 global statistics, #64 sport comparison) landed on `dev` before those branches were merged.

The divergence affected two files:
- **ActivityRepository.cs** — both branches had a manually applied brace fix that dev also had, plus dev gained new GlobalStatistics methods. Rebase auto-merged but produced a duplicate `StreakWeek` inner class — a **silent merge artifact** that only failed at build time.
- **ActivityDetail.razor** — one branch had HR chart features; dev gained sport comparison features. These were independent, non-overlapping additions that had to coexist in the merged result.

## Resolution Applied

**PR #62 (`squad/55-final`):** Rebased onto `origin/dev`. Git auto-merged `ActivityDetail.razor` correctly (both feature sets present). The duplicate `StreakWeek` in `ActivityRepository.cs` was fixed by checking out `origin/dev`'s version and amending the top commit. Build verified before push.

**PR #61 (`squad/56-fix`):** The branch's only meaningful change was `SortMode="None"` in `Activities.razor`, sitting on top of many diverged commits. Used the clean branch approach: created `squad/56-fix-clean` from `origin/dev`, applied only the `Activities.razor` change, committed, built, then force-pushed to `squad/56-fix`. Clean 1-commit diff.

## Recommendation

When concurrent agents are expected, consider staggering branch creation so each agent starts from the most recent `dev`. Alternatively, establish a post-sprint rebase step as part of the PR checklist. Always run `dotnet build` after rebase even when git reports no conflicts — silent merge artifacts (duplicate class definitions) are not caught by git.


---

## 2026-04-13: Dapper SQL-to-C# Naming Convention

**Date:** 2026-04-13  
**Author:** Naomi (Backend Dev)  
**Related fix:** commit 33e15f5 (Dapper materialization error on `/api/statistics/global`)

### Context

The statistics endpoint threw `System.InvalidOperationException` at runtime because `GetGlobalStatisticsAsync` queried directly into positional C# records (`DayActivityCount`, `MonthActivityCount`, `YearSummary`, `SportCount`). Dapper's constructor-parameter matching is case-insensitive but **underscore-sensitive**, so `week_number` (SQL) does not bind to `WeekNumber` (C# parameter).

### Decision

**Never query Dapper directly into public record types that have PascalCase constructor parameters.**

Always use a private DTO class with snake_case property names that match SQL column output exactly, then project to the public type in application code. This is already the established pattern in `ActivityRepository` (`ActivityDto`, `SportStatisticsDto`, `HeatMapDto`, `StreakWeek`).

### Rule

> Any new Dapper query result type must be either:
> 1. A private DTO class with snake_case properties (preferred for SQL-facing types), **or**
> 2. A plain class/record whose property/parameter names exactly match the SQL column names (case-insensitive, no underscore mismatch).
>
> Public records with PascalCase constructors must **not** be used as direct Dapper query targets.

### Rationale

- Prevents silent runtime failures that only surface when the endpoint is actually called.
- Keeps public API models clean and decoupled from SQL naming conventions.
- Consistent with existing repo patterns — reduces surprise for future contributors.

### Alternatives Considered

- **Rename SQL aliases to PascalCase** (e.g., `week_number` → `WeekNumber`): Works but mixes naming conventions in SQL, harder to read at the DB level.
- **Add a Dapper type handler / column mapping**: Adds infrastructure complexity that isn't needed when the DTO pattern already works.

---

## 2026-04-13: LINQ Collection Materialization in API Endpoints

**Date:** 2026-04-13  
**Author:** Naomi (Backend Dev)  
**Related issues:** #57 (activities list truncation), #59 (fix implementation)

### Context

Chained deferred LINQ execution (.Select() on .Select()) can cause silent response truncation when exceptions occur during JSON serialization.

### Problem

When API endpoints return `IEnumerable<T>` with deferred LINQ operations, ASP.NET Core's JSON serializer enumerates the collection during response writing. If an exception occurs during enumeration (null reference, data corruption, serialization error), it can cause silent truncation mid-stream rather than proper error handling.

**Problematic pattern:**
```csharp
// Repository
return activities.Select(MapToActivity);  // Deferred

// Endpoint
var response = activities.Select(a => new { ... });  // Deferred on deferred
return Results.Ok(response);  // Serializer enumerates here
```

If MapToActivity throws or serialization fails partway through, you get partial results (e.g., 11 out of 20 items).

### Decision

Always materialize collections with `.ToList()` or `.ToArray()` before returning from API endpoints and repositories.

**Correct pattern:**
```csharp
// Repository
return activities.Select(MapToActivity).ToList();  // Materialized

// Endpoint
var response = activities.Select(a => new { ... }).ToList();  // Materialized
return Results.Ok(response);
```

### Benefits

1. Exceptions occur within the async method's scope and are caught by exception handler middleware
2. Proper 500 errors instead of silent truncation
3. Better debuggability — full stack trace instead of mid-serialization failure
4. More predictable behavior

### Applies To

- All GET endpoints returning collections
- Repository methods returning `IEnumerable<T>`
- Any LINQ projection that will be JSON-serialized

---

### 2026-04-14: Blazor StreamRendering + JSInterop Initialization Pattern

**Author:** Alex  
**Status:** Accepted

#### Context

`ActivityDetail.razor` uses `@attribute [StreamRendering(true)]` to show a loading spinner while activity data loads. With streaming rendering, the component renders in two phases:

1. **Phase 1** — `activity == null` → spinner rendered. `OnAfterRenderAsync(firstRender: true)` fires here. Map `<div>` and chart `<canvas>` elements do **not** exist in the DOM yet.
2. **Phase 2** — Data arrives → real content rendered. `OnAfterRenderAsync(firstRender: false)` fires.

The original guard `if (firstRender && activity != null)` was always false: on firstRender, activity is null; on subsequent renders, firstRender is false. JS initialization never ran on SPA navigation. F5 bypassed streaming (renders in one shot), so it always worked.

#### Decision

Use a `_initialized` flag to gate JS initialization on data presence, not render count:

```csharp
private bool _initialized = false;

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (activity != null && !_initialized)
    {
        _initialized = true;
        await InitializeMap();
        await InitializeCharts();
    }
}
```

Reset `_initialized = false` in:
- `DisposeAsync` — so navigating away and back re-initializes correctly
- `OnParametersSetAsync` (when `Id` changes) — handles Blazor component reuse between different activity pages

#### Consequences

- Map and charts initialize after activity data is present and DOM elements exist
- Works correctly on both SPA navigation and full page reload
- No double-initialization: the `destroyActivityCharts()` call in `DisposeAsync` (and at the top of `initActivityCharts` in JS) handles cleanup

#### Rule

> When combining `@attribute [StreamRendering(true)]` with JS interop on Blazor Server, **never guard JS init on `firstRender` alone**. Always use a content-ready check (`data != null`) combined with a one-shot `_initialized` flag.
