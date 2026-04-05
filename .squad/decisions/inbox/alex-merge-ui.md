# Decision: Merge UI — page not added to NavMenu

**By:** Alex | **Issue:** #41

The `/merge` page is a transient workflow page (only reachable via the activity list after selecting 2 items). It was intentionally **not** added to the `NavMenu` — consistent with how other utility pages (e.g., `/import`, `/activities/{id}`) are navigated to from within the app rather than from the sidebar.

If a future issue requires surfacing merge as a standalone nav entry, it should be reconsidered then.

---

# Decision: MergePreviewDto and MergeRequest as inner records on ActivityApiClient

**By:** Alex | **Issue:** #41

New API DTOs (`MergePreviewDto`, `MergeRequest`, `MergeResponse`) were added as inner `record` types on `ActivityApiClient`, following the existing pattern of `ActivityTypeDto` being declared there. This keeps all API shapes co-located with the HTTP client and avoids separate DTO files for lightweight contract types.
