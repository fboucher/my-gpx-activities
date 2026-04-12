# Decision: Reuse Existing /api/statistics/by-sport for Activity Detail Comparisons

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
