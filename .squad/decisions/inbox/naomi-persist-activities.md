# Decision: Activity Persistence Strategy

**Author:** Naomi (Backend Dev)  
**Date:** 2026-02-25  
**Issue:** #25  
**PR:** #29

## Context
Activities were being saved to PostgreSQL on import, but the webapp's Activities page only displayed activities from the in-memory `ActivityStore` singleton. After an app restart, `ActivityStore` was empty, so activities disappeared from the list (but were still visible in the heat map, which reads directly from the database).

## Decision
Implement a hybrid approach where the API (PostgreSQL) is the source of truth, but the in-memory `ActivityStore` is used as a session cache:

1. **Created `ActivityApiClient` service** with methods to fetch all activities and individual activities from the API
2. **Activities page loads from API first** on `OnInitializedAsync()`, then populates `ActivityStore` as a cache
3. **ActivityDetail page checks store first** (fast path), then fetches from API if not found
4. **Import workflows continue to add to store** after successful API calls for immediate display
5. **Graceful fallback**: If API call fails, pages fall back to reading from the in-memory store

## Alternatives Considered
1. **Remove ActivityStore entirely** — Would require API call on every page navigation, poor UX
2. **Persist ActivityStore to browser storage** — Adds complexity, data could become stale
3. **Background sync job** — Over-engineered for this use case

## Rationale
- **API as source of truth** ensures data persists across restarts
- **In-memory cache** provides fast navigation without repeated API calls
- **Backward compatible** with existing Import page logic
- **Simple and pragmatic** for a single-user app

## Trade-offs
- **Cold start latency:** First page load after restart requires API call (acceptable for this app)
- **Cache invalidation:** Store isn't automatically updated if activities are deleted/modified elsewhere (not a concern for single-user app with no external modifications)

## Implementation Files
- `webapp/Services/ActivityApiClient.cs` (new)
- `webapp/Components/Pages/Activities.razor` (modified)
- `webapp/Components/Pages/ActivityDetail.razor` (modified)
- `webapp/Program.cs` (registered ActivityApiClient)

## Verification
Build succeeded. Activities now persist across app restarts and are immediately visible after import.
