# History — Holden

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

### OSS Project Setup (PR #35)

**Date:** 2025-02-26  
**Task:** README overhaul + open-source infrastructure per fboucher request

#### What was built
- Restructured README with badges (CI, Docker workflows, Docker Hub, License), vibe-coding note, clear sections
- MIT License (fboucher, 2025)
- CONTRIBUTING.md with branch naming rules (squad/*, feature/*, fix/*) and PR guidelines
- CODE_OF_CONDUCT.md using Contributor Covenant 2.1
- GitHub issue templates for bugs and features (.github/ISSUE_TEMPLATE/)

#### Design decisions
1. **Vibe-coding note tone:** "This project was built with heavy AI assistance. The author knows how to code but embraced AI-assisted development throughout. The code is production-ready and follows modern .NET best practices."
   - Honest and confident (not self-deprecating)
   - Placed just below title/badges for visibility
   - Emphasizes production quality

2. **Badge order:** CI → Docker Publish → Docker Beta → Docker Hub → License
   - Functional health (CI first), then deployment health, then accessibility, then legal

3. **README sections:** Badges → Description → Vibe note → Features → Getting Started (local + Docker Compose) → Development → Contributing → License
   - Docker Compose in "Getting Started" (not separate) for tight narrative flow
   - Development section links to AGENTS.md for team structure and code style

4. **Contributing guidelines:** Reference AGENTS.md for code style rather than duplicate
   - Branch naming clear (squad/*, feature/*, fix/*)
   - No friction—fork, branch, test locally, open PR to dev

#### Patterns for future PRs
- Badge markdown is reusable; keep in reference docs for consistency
- Contributor Covenant 2.1 is standard; future updates use same template
- Issue templates should mirror the project's concerns (GPX parsing, analytics, UI/maps)

#### No blockers
All files created cleanly on squad/readme-oss branch, PR #35 created against dev, awaiting fboucher review.
# History — Holden

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

### README Improvements (Issue #36, PR #35)

**Date:** 2025-02-26  
**Task:** Add 5 improvements to README on squad/readme-oss branch

#### Improvements implemented
1. **Screenshots placeholder:** Created `docs/screenshots/README.md` with contributor instructions; added emoji callout in README pointing to it
2. **Prerequisites section:** New section before "Getting Started" covering .NET 10 SDK, Docker Desktop, Aspire workload installation
3. **Tech stack badges:** Row of 5 badges (.NET 10, Blazor Server, PostgreSQL 16, Docker, MudBlazor) placed after description and before vibe-coding note
4. **API docs note:** Added 💡 tip about Swagger UI availability after port listing
5. **Roadmap section:** New section (before Contributing) listing 5 planned features as checkboxes

#### Decisions
- **Tech stack badge placement:** Immediately after title/existing badges for high visibility to new contributors and users
- **Prerequisites as dedicated section:** Clearer than burying in Getting Started; makes install requirements explicit upfront
- **Roadmap as aspirational:** Listed without owner/due date to manage expectations; allows flexible prioritization
- **Screenshot contributor pathway:** Low-friction docs/ folder with guidance; not mandatory, encourages community participation

#### No blockers
All improvements committed to squad/readme-oss and pushed. PR #35 body updated to reference and close issue #36.
