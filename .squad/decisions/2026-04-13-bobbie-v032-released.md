# Decision: v0.3.2 Release Cut

**Date:** 2026-04-13  
**Owner:** Bobbie (DevOps)  
**Status:** Complete  

## Summary
Cut the v0.3.2 release from `dev` to `main` via PR #65, resolving merge conflicts and creating the GitHub release.

## What Changed
- **PR #65** merged to main with `--merge` flag (preserving history)
- **Tag:** v0.3.2 created on main
- **Release:** Published at https://github.com/fboucher/my-gpx-activities/releases/tag/v0.3.2

## Features in v0.3.2
1. Global Statistics page (`/statistics`) with weekly streaks, monthly heatmap, year recap, sport breakdown
2. Speed comparison on activity detail (avg/max vs global sport averages, color-coded)
3. Heart rate charts for no-GPS activities (yoga, indoor trainer, etc.)

## Bug Fixes in v0.3.2
1. Activities sort order fixed (most-recent-first, MudDataGrid override issue)
2. Activities list visibility fixed (deferred LINQ execution bug)
3. Exception logging added to all 13 API endpoints (server-side logging + safe client messages)

## Hotfixes in v0.3.2
1. Statistics page crash fixed (Dapper materialization/column aliasing)
2. Map/charts blank on first navigation fixed (Blazor StreamRendering + JSInterop timing)

## Merge Conflict Resolution
- Conflicts occurred in 5 files due to version bump and exception handling improvements on dev
- Resolved by:
  1. Fetching `origin/main` into dev branch
  2. Accepting dev's version for all conflicts (contains merged features)
  3. Pushing resolved dev
  4. Successfully merging PR #65 to main

## Version Bump
- `0.3.0` → `0.3.2` (already bumped in `Directory.Build.props` before release)

## Notes
- `dev` branch remains active and not deleted
- `sample-data/` files present on dev as expected (not on main due to `.gitignore`)
- Release marked as `--latest` on GitHub
