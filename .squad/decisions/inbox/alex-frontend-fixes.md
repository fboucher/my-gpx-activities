# Frontend Fixes: Map Sync and Activities UX

**Author:** Alex  
**Date:** 2026-02-25  
**Issues:** #23, #5

## Issue #23: Chart Hover → Map Sync Bug

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

---

## Testing Approach
Both fixes are visual/interactive — no unit tests added.

**Manual testing:**
- #23: Load activity with 30+ min duration, hover through chart timeline, verify map marker moves along entire route at correct position
- #5: Add multiple activity types, test text search (partial match), test type dropdown, test clearing filters, verify row hover styling, check button position on narrow viewport

**Regression risk:** Low. Changes are isolated to two pages (`ActivityDetail.razor` via `chartSync.js` and `Activities.razor`). No shared components or backend APIs modified.
