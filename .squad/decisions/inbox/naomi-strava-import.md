# Decision: Strava Import Design

**Date:** 2026-02-25  
**Author:** Naomi  
**Issue:** #40

## Context

External Strava sync process needs to push activity data to the app. Strava provides two data sources:
1. DetailedActivity JSON (metadata + summary polyline)
2. StreamSet JSON (detailed trackpoint data — optional, costs API quota)

## Decisions

### 1. Flexible Request Model with JsonElement

**Choice:** Use `JsonElement` (System.Text.Json) instead of full Strava SDK models

**Rationale:**
- Strava API evolves — new fields added regularly (e.g., `suffer_score`, `weighted_average_watts`)
- We only need ~10 fields now, but want forward compatibility
- `JsonElement` lets us parse any field without recompiling
- No tight coupling to Strava SDK version

**Trade-off:** More verbose property access (`activity.GetProperty("name").GetString()`) vs compile-time safety

### 2. Dual Track Data Strategy

**Choice:** Support both streams and polyline fallback

**Implementation:**
```
IF streams present → parse latlng + altitude + HR + cadence + time arrays
ELSE IF polyline present → decode polyline (lat/lon only, null for other fields)
ELSE → empty track data
```

**Rationale:**
- Streams cost API quota — not always available
- Polyline is always present (except manual activities)
- Graceful degradation: some data better than no data

### 3. Google Polyline Decoder (Custom Implementation)

**Choice:** Implement polyline decoder inline (~20 lines), no NuGet package

**Rationale:**
- Algorithm is simple (5-bit chunks, sign extension, delta encoding)
- No quality NuGet packages (most are abandoned or overweight)
- Reduces dependency surface area
- Algorithm is stable (Google Maps standard since 2008)

### 4. Duplicate Detection by Title + Start Date

**Choice:** Query `WHERE title = @Title AND start_date_time = @StartDate`

**Rationale:**
- Activities table has no `strava_id` or `external_id` column
- Adding one would require schema migration + updating all existing activities
- Title + timestamp combo is unique enough (user unlikely to start two identically-named activities at same second)
- Duplicate imports are logged to `import_errors` table for auditing

**Future consideration:** If supporting multiple import sources (Garmin, Polar, etc.), consider adding `external_source` and `external_id` columns

### 5. Import Errors Table (Audit Log)

**Choice:** Create dedicated `import_errors` table for rejected imports

**Schema:**
```sql
id SERIAL PRIMARY KEY,
source TEXT NOT NULL,        -- 'strava', 'garmin', etc.
external_id TEXT,             -- source-specific activity ID
message TEXT,                 -- human-readable error
created_at TIMESTAMP WITH TIME ZONE
```

**Rationale:**
- Duplicate imports shouldn't crash the endpoint
- User needs visibility into why an import was rejected
- Enables future "retry failed imports" feature
- Separates concerns: `activities` = valid data, `import_errors` = rejected data

### 6. Sport Type Mapping Dictionary

**Choice:** Static dictionary with regex fallback

```csharp
["NordicSki"] → "Nordic Ski"
["Ride"] → "Cycling"
// ... etc
```

Fallback: `Regex.Replace(type, "([a-z])([A-Z])", "$1 $2")`

**Rationale:**
- Strava uses PascalCase with no spaces (`NordicSki`, `WeightTraining`)
- App UI expects readable labels (`Nordic Ski`, `Weight Training`)
- Fallback handles new/unknown sport types gracefully

## Open Questions

1. Should we store `strava_id` in activities table for future deduplication improvement?
2. Should we expose `GET /api/import-errors` endpoint for debugging?
3. Should we retry failed imports automatically (e.g., on polyline decode errors)?

## Testing

- Build succeeded with 0 warnings/errors
- Integration tests fail on infrastructure (Postgres auth) — unrelated to code changes
- Manual testing required: POST sample Strava JSON to endpoint

## Related Decisions

- Issue #11 (Smart Merge) — established 6-element track_data_json schema
- Issue #10 (Heat Map) — demonstrated jsonb column usage pattern
