# Decision: Footer implemented with MudPaper (fixed position) instead of MudAppBar Bottom

**Author:** Alex  
**Issue:** #39  
**Date:** 2025

## Context
The spec called for `MudAppBar` with `Bottom="true"` and `Fixed="true"`. In the MudBlazor version used by this project, `MudAppBar` does not expose a `Bottom` property.

## Decision
Use `MudPaper` with inline style `position:fixed;bottom:0;left:0;right:0;z-index:1300` to achieve a fixed bottom bar. This is visually equivalent and compiles cleanly.

## Consequences
- Any future change to the footer should continue using `MudPaper` with fixed positioning (not `MudAppBar`).
- If MudBlazor is upgraded and `MudAppBar Bottom="true"` becomes available, consider migrating for consistency.
