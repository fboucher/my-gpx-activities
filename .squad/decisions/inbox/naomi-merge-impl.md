# Decision: Activity Merge — ID type and merge alignment strategy

**Author:** Naomi  
**Issue:** #41  
**Date:** 2025-02-26

## ID Type

The spec called for `int ActivityAId / ActivityBId` in `MergeRequest`, but the `activities` table and all existing models use `Guid` as the primary key. **Decision: use `Guid`** in `MergeRequest` to stay consistent with the rest of the codebase. Alex/UI layer should pass GUIDs.

## Merge Mode — Point Alignment

For "merge" mode (overlapping time ranges), the spec says each channel is taken from the activity with more data points for that channel. The implementation uses **array-index alignment**: point `i` in the merged output corresponds to point `i` in the GPS-spine activity. This works correctly when both activities have the same number of trackpoints (typical for overlapping activities recorded with the same device). If lengths differ, the merged output is trimmed to the GPS-spine length (shorter wins).

This is a pragmatic simplification. A timestamp-based alignment (matching points by unix_ms) would be more accurate but significantly more complex — deferred unless fboucher requests it.

## Stats for Merged Activity

Stats (distance, elevation, speed) are **inherited** from the source activities rather than recomputed from raw GPS points. This avoids a full haversine pass over merged trackpoints at insert time. Recomputation could be added later if accuracy becomes a concern.
