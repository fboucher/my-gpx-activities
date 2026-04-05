# History — Alex

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

### Issue #10 — Heat Map page
- **Map library**: Leaflet.js was already included via CDN in `Components/App.razor`. Extended `wwwroot/map.js` with new global functions (`initializeHeatMap`, `drawHeatMapTraces`, `destroyHeatMap`) rather than creating a separate JS file to keep the single file pattern consistent.
- **Component pattern**: Used `IAsyncDisposable` on the Blazor page to call `destroyHeatMap` on disposal and avoid Leaflet map leaks. Map is initialized in `OnAfterRenderAsync(firstRender)` to ensure the DOM element exists.
- **API client pattern**: Created a dedicated `HeatMapApiClient` (primary-constructor style, injecting `IHttpClientFactory`) registered as transient. It builds query strings manually and deserializes with `GetFromJsonAsync`. Reuses the named `"ApiService"` HttpClient already configured in `Program.cs`.
- **MudBlazor checkbox in loops**: When using `MudCheckBox` inside `@foreach`, capture the loop variable with `var s = sport;` to avoid closure issues. Use `Value`/`ValueChanged` (not `Checked`/`CheckedChanged`) for MudBlazor v7+.
- **Sport colors**: Used a fixed palette array cycled by insertion order — simple and deterministic without requiring a database color column.

### Issue #11 — Smart Merge UI
- **File upload pattern**: Used two independent `MudFileUpload T="IBrowserFile"` components (single file, not list) with separate handlers (`HandleMergeGpxChanged`, `HandleMergeFitChanged`) to handle GPX and FIT file selection independently.
- **Visual separation**: Used `MudStack AlignItems="AlignItems.Center"` with centered text "— or —" instead of `MudDivider` with ChildContent (which triggers MudBlazor analyzer warning MUD0002).
- **Color scheme**: Used `Color.Secondary` for the merge card's button and chips to visually distinguish the Smart Merge workflow from the regular GPX batch import (which uses `Color.Primary` and `Color.Success`).
- **Multipart upload**: Followed existing pattern of reading both files into memory using `MemoryStream` and `ByteArrayContent` to avoid Blazor SignalR stream concurrency issues, then posting to `POST /api/activities/smart-merge/import` with field names "gpx" and "fit".
- **State management**: Added three fields (`mergeGpxFile`, `mergeFitFile`, `isMerging`) and cleared selected files after successful merge to reset the UI.

### Issue #19 — Activity Detail synchronized charts (Elevation, Pace, HR, Cadence)
- **Backward compatibility**: TrackData schema extended to 6 elements `[lat, lon, ele, hr, ts_ms, cadence]`. All JS checks use `p.length > 5 && p[5] != null` guard; older 5-element arrays work without changes.
- **Pace computation**: Haversine formula between consecutive lat/lon pairs + time delta gives m/s → converted to min/km. Outliers clamped at 30 min/km (stopped/GPS noise). 10-point rolling average (±5 window) applied before downsampling to smooth spiky data. Y-axis reversed so faster pace appears at top; ticks formatted as `M:SS`.
- **Crosshair sync across all 4 charts**: `makeHoverHandler` now receives `[elevationChart, paceChart, hrChart, cadenceChart]` — whichever are non-null. Each chart sets `_activeCrosshairX` on all siblings and calls `draw()` to repaint the custom `crosshairPlugin`.
- **Map sync marker color**: Changed from `#e74c3c` (red) to `#2563eb` (blue) per user's visual request.
- **Conditional Razor blocks**: Pace card shown when any point has `p.Length > 4 && p[4] != null` (timestamp required). Cadence card shown when `p.Length > 5 && p[5] != null`. Order on page: Elevation → Pace → Heart Rate → Cadence.
- **Chart colors**: Elevation = teal, Pace = amber `rgb(234,179,8)`, HR = red/pink, Cadence = purple `rgb(168,85,247)`.
- **`destroyActivityCharts`**: Tears down all four charts + removes the sync marker on disposal to prevent Leaflet/Chart.js memory leaks.


### Issue #23 — Map dot position sync with chart hover
- **Root cause**: Charts were downsampled to ~500 points for performance, but the hover handler used the downsampled index directly on the original `trackData` array. At 29/30 minutes (97% through the chart), the code accessed `trackData[497]` when it should have accessed `trackData[~2900]` (the original index of that downsampled point).
- **Fix**: Store the downsampling index maps (`dsIndexMaps.elevation`, `.pace`, `.hr`, `.cadence`) — each is an array mapping chart index → original trackData index. When hovering, `makeHoverHandler` now receives the index map and uses `indexMap[chartIdx]` to find the correct lat/lon in the original data.
- **Result**: Map blue dot now tracks the exact GPS position corresponding to the hovered chart point, moving smoothly along the entire route as you scrub through the timeline.

### Issue #5 — Activities page UX improvements
- **Filters added**: Two-part filter system — text search (by activity title or type) + dropdown filter by activity type. `FilteredActivities` computed property applies both filters in sequence. Activity type dropdown is dynamically populated from distinct types in the current activity list.
- **Button repositioning**: Moved "View" action buttons from right-aligned last column to left-most column (first column). Keeps buttons visible and accessible without horizontal scrolling on narrow screens.
- **Row hover highlight**: Added `Hover="true"` to `MudDataGrid` — MudBlazor's built-in hover styling provides visual feedback when mousing over rows.
- **Filter UI design**: Wrapped filters in `MudPaper` with elevation for visual separation. Used `MudTextField` with search icon and debounce (300ms) to avoid excessive re-filtering on every keystroke.
- **Counter update**: Footer now shows `X of Y activities` when filters are active, indicating how many match out of total.

### Issue #39 — Add a footer
- **Footer component**: Created `AppFooter.razor` using `MudPaper` with `position:fixed;bottom:0` inline style rather than `MudAppBar Bottom="true"` — `MudAppBar` does not support a `Bottom` prop in this MudBlazor version; `MudPaper` with fixed positioning achieves the same visual result.
- **Service injection**: Used `@inject IAppVersionService VersionService` — the service was already registered in `Program.cs` by Naomi as a singleton.
- **Layout update**: Added `pb-16` to `MudMainContent` to prevent page content from being hidden behind the fixed footer; placed `<AppFooter />` just before the closing `</MudLayout>` tag.
- **Typography**: Used `Typo.caption` + `Color.Secondary` for all three sections to keep the footer visually subtle and consistent with the app's dark palette.

### Issue #41 — Merge selected activities
- **Checkbox selection**: Added a narrow `TemplateColumn` with `MudCheckBox` as the first column in Activities.razor. Used a `HashSet<Guid> selectedIds` and a `ToggleSelection` helper. Disabling checkboxes when `selectedIds.Count >= 2` (and the item isn't already checked) enforces the "exactly 2" constraint without extra validation logic.
- **Merge banner**: Used `MudAlert Severity="Severity.Info"` with an inline `MudStack Row` containing the "Merge Selected" and "Clear" buttons — this floats above the grid only when exactly 2 items are checked, keeping the UI uncluttered otherwise.
- **Navigation**: `NavigationManager.NavigateTo($"/merge?a={ids[0]}&b={ids[1]}")` — order matches the order items were added to the HashSet (insertion order is preserved in .NET `HashSet<Guid>` via `LinkedHashSet`-like behavior in recent runtimes; this is fine for preview).
- **Merge.razor query params**: Used `[SupplyParameterFromQuery(Name = "a")]` / `[SupplyParameterFromQuery(Name = "b")]` on `public Guid` properties — the standard Blazor way for typed query params (no `NavigationManager.GetUriWithQueryParameter` needed).
- **MergePreviewDto**: Declared as a record inside `ActivityApiClient` alongside `MergeRequest` and `MergeResponse` to keep all API DTOs co-located with the client. Follows existing pattern of `ActivityTypeDto` as an inner record.
- **Sport type dropdown**: When both activities have the same sport type, only one `MudSelectItem` is shown. Guard with `if (preview.ActivityBSportType != preview.ActivityASportType)` in the Razor template.
- **MUD0002 warning**: `Title` is an illegal attribute on `MudIconButton` — use `aria-label` instead for accessibility tooltips on icon buttons.
- **Merge page not in NavMenu**: The `/merge` route is a transient utility page, not a top-level destination. No nav entry added — consistent with how modal/workflow pages are handled in this app.
