# README Improvements — Issue #36

**Author:** Holden (AI Lead)  
**Date:** 2025-02-26  
**Issue:** #36  
**PR:** #35  
**Branch:** `squad/readme-oss`  
**Status:** Committed and pushed; awaiting fboucher review

## Context
Requested 5 improvements to enhance the README's usefulness for new contributors and users.

## Decision: Implementation Details

### 1. Screenshots Placeholder
- **What:** New `docs/screenshots/README.md` with contributor guidelines
- **Why:** Lower friction for community contributions; centralizes visual documentation strategy
- **Placement:** Referenced with 📸 emoji callout after vibe-coding note
- **Not mandatory:** Keeps onboarding lightweight; optional enhancement

### 2. Prerequisites Section
- **What:** Dedicated section before "Getting Started" listing required tools
- **Content:**
  - .NET 10 SDK with download link
  - Docker Desktop requirement (for PostgreSQL)
  - Aspire workload installation command
- **Why:** Makes dependencies explicit upfront; reduces install friction
- **Alternative rejected:** Burying in Getting Started text makes requirements unclear

### 3. Tech Stack Badges
- **What:** Five shields (.NET 10, Blazor Server, PostgreSQL 16, Docker, MudBlazor)
- **Placement:** Immediately after description, before vibe-coding note
- **Why:** High visibility for decision-makers; signals technology maturity to potential users/contributors
- **Choice of badges:** Match core project identity (not transient deps like NUnit)

### 4. API Documentation Note
- **What:** 💡 tip about Swagger UI at `/swagger` endpoint
- **Placement:** After localhost port listing in "Running Locally"
- **Why:** Developers need API contract visibility; in-app discovery better than separate docs
- **Format:** Callout style consistent with existing Tip about Docker Compose version tags

### 5. Roadmap Section
- **What:** Five checkbox items for planned features
- **Placement:** New section before Contributing (narrative flow: Setup → Develop → Contribute → Roadmap → License)
- **Content:** Activity comparison, FIT support, CSV/GPX export, personal records, Strava integration
- **Why:** Sets expectations; invites community input on priorities; not owner-assigned (flexible prioritization)
- **No dates/owners:** Manages expectations; prevents commitment to features beyond project scope

## Rationale

### Section Ordering
1. **Prerequisites** before Getting Started → install clarity first
2. **Roadmap** before Contributing → shows vision before asking for help
3. Both additions improve narrative flow without disrupting existing content

### Badge Placement
Placed after description to catch readers early while still highlighting the current stable status badges. Avoids banner bloat by grouping with CI/publish/Docker badges.

### Optional Contributions
Screenshots and roadmap feedback are framed as "contributions welcome" rather than requirements. Reduces friction for new contributors while inviting engagement.

## Implementation Notes
- All edits applied to `squad/readme-oss` (existing branch for PR #35)
- New directory `docs/screenshots/` created with guidance README
- PR #35 body updated to include "Closes #36"
- Commit message: "docs(#36): README improvements..."
- Single commit for clean history

## No blockers
Ready for fboucher review and merge to dev.
