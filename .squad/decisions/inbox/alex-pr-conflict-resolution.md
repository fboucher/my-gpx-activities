# Decision: Concurrent agent branches cause divergence when later PRs land

**By:** Alex | **Date:** 2026-04-13 | **Context:** v0.3.2 sprint PR conflict resolution (PRs #61, #62)

## Pattern observed

During v0.3.2, seven agents ran concurrently. All branched from the same state of `dev`. Five PRs merged cleanly. Two (`squad/55-final` / PR #62 and `squad/56-fix` / PR #61) were blocked because two subsequent PRs (#63 global statistics, #64 sport comparison) landed on `dev` before those branches were merged.

The divergence affected two files:
- `ActivityRepository.cs` — both branches had a manually applied brace fix that dev also had, plus dev gained new GlobalStatistics methods. Rebase auto-merged but produced a duplicate `StreakWeek` inner class — a **silent merge artifact** that only failed at build time.
- `ActivityDetail.razor` — one branch had HR chart features; dev gained sport comparison features. These were independent, non-overlapping additions that had to coexist in the merged result.

## Resolution applied

**PR #62 (`squad/55-final`):** Rebased onto `origin/dev`. Git auto-merged `ActivityDetail.razor` correctly (both feature sets present). The duplicate `StreakWeek` in `ActivityRepository.cs` was fixed by checking out `origin/dev`'s version and amending the top commit. Build verified before push.

**PR #61 (`squad/56-fix`):** The branch's only meaningful change was `SortMode="None"` in `Activities.razor`, sitting on top of many diverged commits. Used the clean branch approach: created `squad/56-fix-clean` from `origin/dev`, applied only the `Activities.razor` change, committed, built, then force-pushed to `squad/56-fix`. Clean 1-commit diff.

## Recommendation

When concurrent agents are expected, consider staggering branch creation so each agent starts from the most recent `dev`. Alternatively, establish a post-sprint rebase step as part of the PR checklist. Always run `dotnet build` after rebase even when git reports no conflicts — silent merge artifacts (duplicate class definitions) are not caught by git.
