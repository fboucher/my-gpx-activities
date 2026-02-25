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

### 2026-02-25: Docker Hub publish + beta tagging (Issue #32)

Added two GitHub Actions workflows and two Dockerfiles to publish application images to Docker Hub.

Key learnings:
- This project has two publishable services: `my-gpx-activities.ApiService` (API) and `webapp` (Blazor Server). There is no single "app image" — both need separate Dockerfiles.
- Dockerfile build context must be set at the solution root (`./my-gpx-activities`) so `COPY` can reach the shared `ServiceDefaults` project referenced by both services.
- Aspire service discovery uses the env var `services__apiservice__http__0` — this is the correct key to set in Docker Compose so the webapp can find the API without code changes.
- Beta versioning: fetch all tags with `fetch-depth: 0`, use `git describe --tags --abbrev=0` for latest tag, strip `v`, increment patch, append `-beta`. No `latest` tag is written for beta builds.
- Release workflow uses `docker/build-push-action@v6` and `docker/setup-buildx-action@v3` — these are the current stable major versions.
- Images are named `fboucher/my-gpx-activities-api` and `fboucher/my-gpx-activities-webapp` (two separate repos on Docker Hub).
- Decision documented in `.squad/decisions/inbox/bobbie-container-setup.md`.
