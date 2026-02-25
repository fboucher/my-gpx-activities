# Decisions Log

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
