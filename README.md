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