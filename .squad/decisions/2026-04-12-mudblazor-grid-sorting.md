# Decision: MudBlazor Grid Sorting Strategy

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
