# my-gpx-activities

[![CI](https://github.com/fboucher/my-gpx-activities/actions/workflows/ci.yml/badge.svg)](https://github.com/fboucher/my-gpx-activities/actions/workflows/ci.yml)
[![Docker Publish](https://github.com/fboucher/my-gpx-activities/actions/workflows/docker-publish.yml/badge.svg)](https://github.com/fboucher/my-gpx-activities/actions/workflows/docker-publish.yml)
[![Docker Beta](https://github.com/fboucher/my-gpx-activities/actions/workflows/docker-beta.yml/badge.svg)](https://github.com/fboucher/my-gpx-activities/actions/workflows/docker-beta.yml)
[![Docker Hub](https://img.shields.io/docker/pulls/fboucher/my-gpx-activities-api?label=Docker%20Hub)](https://hub.docker.com/r/fboucher/my-gpx-activities-api)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/fboucher/my-gpx-activities)

A .NET 10 Aspire application for managing personal GPX files. Upload, view, and analyze GPS activity data with interactive maps.

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-enabled-2496ED?logo=docker&logoColor=white)
![MudBlazor](https://img.shields.io/badge/MudBlazor-UI-594AE2)

> **Note:** This project was built with heavy AI assistance. The author knows how to code but embraced AI-assisted development throughout. The code is production-ready and follows modern .NET best practices.

📸 **Screenshots welcome!** See [docs/screenshots/README.md](docs/screenshots/README.md) to contribute.

## Features

- Upload GPX files for GPS activities
- View activity details and analytics  
- Interactive maps showing routes
- Heart rate and cadence data support with FIT file merging
- Database persistence with PostgreSQL

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (required for the PostgreSQL database)
- Aspire workload:
  ```bash
  dotnet workload install aspire
  ```

## Getting Started

### Running Locally

Requires .NET 10 and PostgreSQL (or use Docker Compose).

```bash
# Clone and run
git clone https://github.com/fboucher/my-gpx-activities
cd my-gpx-activities

# Run the full application with Aspire (includes PostgreSQL)
dotnet run --project my-gpx-activities.AppHost
```

The application will be available at:
- **Web UI:** https://localhost:15888 (or similar)
- **API:** https://localhost:15889 (or similar)
- **pgAdmin:** https://localhost:15890 (or similar)

> 💡 **API documentation** (Swagger UI) is available at the API service URL + `/swagger` when running in development mode.

See [AGENTS.md](AGENTS.md) for detailed development setup and code style guidelines.

### Docker Compose

Pre-built images are published to Docker Hub on every release. Run the full stack without a local .NET install:

```bash
# Save this as docker-compose.yml
docker compose up
```

```yaml
services:
  db:
    image: postgres:16
    environment:
      POSTGRES_DB: gpxactivities
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    volumes:
      - gpxdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
      timeout: 5s
      retries: 10

  api:
    image: fboucher/my-gpx-activities-api:latest
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__gpxactivities: "Host=db;Database=gpxactivities;Username=postgres;Password=postgres"
    depends_on:
      db:
        condition: service_healthy

  webapp:
    image: fboucher/my-gpx-activities-webapp:latest
    ports:
      - "8081:8080"
    environment:
      ASPNETCORE_URLS: http://+:8080
      services__apiservice__http__0: http://api:8080
    depends_on:
      - api

volumes:
  gpxdata:
```

Web UI: **http://localhost:8081** | API: **http://localhost:8080**

> Tip: Replace `latest` with a specific version tag (e.g., `0.2.0`) for reproducible deployments.

## Development

This project uses:
- **.NET 10** with Aspire for orchestration
- **Blazor Server** + **MudBlazor** for the UI
- **PostgreSQL** for persistence
- **NUnit** for testing

See [AGENTS.md](AGENTS.md) for:
- Build, lint, and test commands
- Code style and naming conventions  
- Architecture patterns and best practices
- Team structure and responsibilities

## Roadmap

- [ ] Activity comparison (overlay multiple routes)
- [ ] FIT file support (Garmin device format)
- [ ] Export to CSV / GPX download
- [ ] Personal records and statistics dashboard
- [ ] Strava / Garmin Connect import integration

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for:
- How to fork and branch
- Pull request process
- Code review expectations
- Code style guidelines

Read [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for community guidelines.

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.