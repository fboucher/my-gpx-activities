# my-gpx-activities

A .NET 10 Aspire application for managing personal GPX files. Upload, view, and analyze GPS activity data with interactive maps.

## Running Locally

```bash
# Clone the repository
git clone <repository-url>
cd my-gpx-activities

# Run the application (includes PostgreSQL database)
dotnet run --project my-gpx-activities.AppHost
```

The application will be available at:
- Web UI: https://localhost:15888 (or similar)
- API: https://localhost:15889 (or similar)
- pgAdmin: https://localhost:15890 (or similar)

## Features

- Upload GPX files for GPS activities
- View activity details and analytics
- Interactive maps showing routes
- Database persistence with PostgreSQL

## Docker Compose

Pre-built images are published to Docker Hub on every release. Use the example below to run the full stack without a local .NET install.

```yaml
# docker-compose.yml
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

Then run:

```bash
docker compose up
```

The web UI will be available at **http://localhost:8081** and the API at **http://localhost:8080**.

Replace `latest` with a specific version tag (e.g., `0.2.0`) for reproducible deployments.