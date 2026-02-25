# History — Bobbie

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

### 2026-02-25: Copilot config files blocked from `main`

Extended `.github/workflows/squad-main-guard.yml` (which already blocked `.squad/`, `.ai-team/`, etc.) to also block `.copilot/` and `.github/copilot-instructions.md`. Creating a new `check-squad-files.yml` was rejected because it would duplicate logic — extending the existing guard was the cleaner solution.

Key learnings:
- `.gitignore` on `main` is a bad approach because it's branch-specific and conflicts on every `dev` → `main` merge.
- The `squad-main-guard.yml` workflow uses `f.status !== 'removed'` to allow deletion PRs — so you can clean up forbidden files from `main` via PR without the guard blocking you.
- The guard fires on both `pull_request` and `push` events to protected branches, including handling force pushes via tree enumeration.
- Decision documented in `.squad/decisions/inbox/bobbie-main-branch-protection.md`.
