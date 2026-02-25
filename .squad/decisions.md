# Team Decisions

---

### 2026-02-25: Dev container setup for zero-install development

**By:** Bobbie | **Issue:** #37 | **PR:** #38

- Use `mcr.microsoft.com/devcontainers/base:ubuntu` + dotnet feature `version: "10.0"` (pre-built images max out at .NET 9)
- Include `docker-in-docker` — Aspire needs it to spin up PostgreSQL, pgAdmin, etc.
- Forward ports: 15888 (Aspire dashboard), 15889 (API), 15890 (pgAdmin), 8080, 8081
- Install Aspire workload via `postCreateCommand` (adds ~5 min to first startup — acceptable)
- Add Codespaces badge to README for one-click cloud dev

---

### 2026-02-25: README improvements — screenshots, prerequisites, tech stack, API docs, roadmap

**By:** Holden | **Issue:** #36 | **PR:** #35

- `docs/screenshots/` placeholder created; contributions welcome but not required
- Prerequisites section placed before Getting Started (explicit dependency listing reduces friction)
- Tech stack badges (.NET 10, Blazor Server, PostgreSQL 16, Docker, MudBlazor) placed after description
- Swagger tip added after port listing in Running Locally
- Roadmap section placed before Contributing; no dates/owners to manage expectations

---

### 2026-02-25: Squad state (.squad/) committed to dev only — never to feature branches

**By:** fboucher (via Coordinator)

Squad state files (history.md, decisions inbox, orchestration logs, session logs) must only be committed on the `dev` branch. Committing them on feature branches pollutes PR diffs with unrelated file changes.

**Rules for all agents:**
- On a feature branch: write `.squad/` changes to disk only — do NOT `git add .squad/` or commit them
- Scribe: always commit `.squad/` state on `dev`. If currently on a feature branch, switch to dev first:
  ```bash
  git stash           # if any uncommitted work on feature branch
  git checkout dev
  git add .squad/
  git commit -m "chore: squad state update [skip ci]"
  git checkout -      # return to feature branch
  git stash pop       # if stashed
  ```
- Feature branch commits must contain only the actual work files

**Why:** Every PR was showing 30+ changed files due to squad state. PRs should only show the files that matter for review.
