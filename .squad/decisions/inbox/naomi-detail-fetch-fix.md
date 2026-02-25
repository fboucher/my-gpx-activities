# ActivityDetail Page: Fetch Full Data When Store Has Summary Only

**Author:** Naomi  
**Date:** 2026-02-25  
**Status:** Implemented

## Context
`ActivityDetail.razor` showed empty charts and map when users navigated from the Activities list page to a detail page. The bug occurred because:

1. `/api/activities` (list endpoint) returns summary data without `TrackData` or `TrackCoordinates`
2. Activities list page populates `ActivityStore` with these summary-only objects
3. `ActivityDetail.razor` checks store first via `ActivityStore.GetById(Id)`
4. When found in store, the page used the data directly — but `TrackData = null` meant no charts/map rendered

## Decision
Modified `ActivityDetail.razor` to **always fetch from API when TrackData is missing**, even if the activity exists in the store. This ensures full data (with TrackData and TrackCoordinates) is loaded for the detail page.

### Changes

#### ActivityDetail.razor (line 296)
**Before:**
```csharp
if (storedActivity == null)
```

**After:**
```csharp
if (storedActivity == null || storedActivity.TrackData == null)
```

This condition now fetches from API in two scenarios:
- Activity not in store at all (original behavior)
- Activity in store but only has summary data (new fix)

#### ActivityStore.cs (new method)
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

## Behavior After Fix
1. **First visit to list page:** Store populated with summary data (no TrackData)
2. **Navigate to detail page:** Condition detects `TrackData == null` → fetches full data from `/api/activities/{id}`
3. **Store updated:** `AddOrUpdate()` replaces summary entry with full data
4. **Subsequent detail visits:** Store has full data → no API call needed (fast path works correctly)

## Rationale
- **Minimal change:** Only modified the condition and added one method
- **Performance:** Store caching still works when full data is present
- **No API changes:** Backend endpoints remain unchanged
- **No duplicates:** AddOrUpdate ensures clean store state
- **Backward compatible:** Existing `Add()` method unchanged; edit dialog still works

## Alternative Considered
Could have changed list endpoint to always return full data — rejected because:
- List page doesn't need TrackData (only displays summary cards)
- Would increase payload size and DB load unnecessarily
- Current approach is more efficient (lazy loading pattern)
