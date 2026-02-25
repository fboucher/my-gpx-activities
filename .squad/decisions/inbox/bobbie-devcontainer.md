# Dev Container Setup for Zero-Install Development

**Author:** Bobbie  
**Date:** 2026-02-25  
**Issue:** #37  
**PR:** #38  
**Status:** Implemented

## Context
Contributors needed to install .NET 10, Docker, and various other tools locally before contributing. The goal was to enable zero-install development using GitHub Codespaces or VS Code Dev Containers.

## Decision: .NET 10 Base Image + Features

**Choice:** Use `mcr.microsoft.com/devcontainers/base:ubuntu` with the dotnet feature specifying `version: "10.0"`.

**Rationale:**
- Pre-built .NET images (like `mcr.microsoft.com/devcontainers/dotnet`) typically max out at .NET 9
- The feature-based approach allows specifying .NET 10 explicitly
- Ubuntu base is lightweight and flexible for adding additional tools

## Decision: Docker-in-Docker for Aspire

**Choice:** Include the `docker-in-docker` feature.

**Rationale:**
- Aspire applications orchestrate their own services (PostgreSQL, pgAdmin, etc.) in Docker containers
- Without Docker-in-Docker, developers cannot run the full Aspire application in the dev container
- This feature is essential for local development parity

## Decision: Port Forwarding Strategy

**Ports Forwarded:**
- Aspire Dashboard: 15888
- API Service: 15889
- pgAdmin: 15890
- API (direct): 8080
- Webapp (direct): 8081
- Additional Aspire ports: 15000, 15001, 15002

**Rationale:**
- Aspire dashboard ports (155xx range) are used when running the AppHost
- Direct service ports (8080, 8081) allow testing individual services or Docker Compose deployments
- Extra ports ensure flexibility for different deployment scenarios

## Decision: VS Code Extensions

**Extensions Included:**
- `ms-dotnettools.csdevkit` — C# Dev Kit for advanced IDE features
- `ms-dotnettools.csharp` — OmniSharp for C# language support
- `ms-azuretools.vscode-docker` — Docker integration for container management
- `GitHub.copilot` — GitHub Copilot AI assistant
- `GitHub.copilot-chat` — GitHub Copilot Chat for conversational AI

**Rationale:**
- C# Dev Kit provides modern .NET development experience
- Docker extension eases container debugging and exploration
- Copilot extensions enhance productivity and learning
- These are commonly installed in the team's local environments

## Decision: Aspire Workload Installation

**Choice:** Install via `postCreateCommand: "dotnet workload install aspire"`.

**Rationale:**
- Aspire SDK workload is required to run the AppHost and its orchestration features
- Post-create hook ensures it's installed before the developer opens the workspace
- Takes ~2-3 minutes on first startup (acceptable trade-off for zero-install experience)

## Decision: Codespaces Badge in README

**Choice:** Add badge linking to `https://codespaces.new/fboucher/my-gpx-activities`.

**Rationale:**
- Makes Codespaces entry point discoverable from the README
- Single click from browser to running dev environment in the cloud
- Follows GitHub's standard Codespaces badge pattern

## Trade-offs

### Pro
- Zero local installation required
- Works on any machine with a browser (including Chromebooks, tablets)
- Reproducible environment for all contributors
- Aspire full-stack testing available immediately

### Con
- First startup takes ~5-10 minutes (workload installation + NuGet restore)
- Codespaces has compute/storage limits on free tier
- Not suitable for long-term development on free tier (pay model applies)
- Bandwidth/latency depends on internet connection

## Files Created
- `.devcontainer/devcontainer.json` — Main configuration
- `.devcontainer/README.md` — Usage instructions
- Updated `README.md` with Codespaces badge
