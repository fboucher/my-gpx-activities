# Decision: Copilot config files blocked from `main` via existing guard workflow

**Date:** 2026-02-25
**Author:** Bobbie (DevOps)
**Status:** Implemented

## Context

The coordinator directive (`coordinator-main-branch-clean.md`) required that `.squad/`, `.copilot/`, and `.github/copilot-instructions.md` be blocked from the `main` branch in an enforceable way that doesn't break the `dev` workflow.

## Decision

Rather than creating a new `check-squad-files.yml` workflow (which would duplicate logic), we extended the **existing** `squad-main-guard.yml` workflow to also block:
- `.copilot/` — Copilot/MCP configuration (e.g., `mcp-config.json`)
- `.github/copilot-instructions.md` — the Copilot prompt/instructions file

The existing workflow already covered `.squad/`, `.ai-team/`, `.ai-team-templates/`, `team-docs/`, and `docs/proposals/`. Adding the two Copilot-specific paths keeps enforcement in a single workflow file.

## Why not `.gitignore` on `main`?

Adding `.squad/` to `.gitignore` on `main` would conflict on every merge from `dev` because `.gitignore` is branch-specific. This approach was explicitly rejected in favor of the CI guard.

## Enforcement

- Triggers on `pull_request` targeting `main`, `preview`, and `insider`
- Also triggers on direct `push` to those branches (guards against force pushes)
- Fails with a clear, actionable error message listing forbidden files and `git rm --cached` fix instructions
- Removals (deleting forbidden files) are explicitly allowed — so you can clean up via PR if needed

## Dev workflow impact

None. `.squad/` and `.copilot/` remain fully tracked and committable on `dev` and feature branches. The guard only fires when targeting protected branches.
