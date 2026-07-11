# Context: my-gpx-activities

## Domain Terms

| Term | Definition |
|---|---|
| **Activity-Level Record** | The maximum value of a metric (Max Speed, Distance, Duration, Elevation Gain) among all activities of the same sport type. Records exist at two scopes: **all-time** (across all years) and **yearly** (within a calendar year). |
| **Best Segment** | The fastest contiguous sub-segment of an activity at a standard distance (1 km, 5 km, 10 km). Computed by scanning adjacent trackpoints at import time. |
| **Record Indicator** | A visual badge shown on the Activity Detail page when an activity sets a new record. Each sport-type/year combination that the activity breaks is shown. |
| **Records Table** | A dedicated `activity_records` database table that materializes the current record holder for each (sport_type, year) and (sport_type, all_time) combination. Updated during import and deletion. |
| **Best Segments Table** | A dedicated `activity_best_segments` database table storing the fastest 1 km, 5 km, and 10 km segments computed for each activity at import time. |
| **Weather Condition** | The set of atmospheric measurements fetched for an activity: temperature (°C), condition text/icon, wind speed, wind direction, humidity, visibility. |
| **Weather Fetch Strategy** | Weather is fetched at import time using the activity's **starting trackpoint** (first lat/lon + timestamp). If weather data is missing when viewing an activity (e.g., activities imported before this feature), it is fetched and saved on-the-fly. |
| **Weather Data Storage** | Stored as a single `weather_data` JSONB column on the `activities` table. No separate table or normalized columns. |
| **Weather API** | Open-Meteo Historical Weather API — free, no API key required. |
| **Lazy Backfill** | The fallback fetch-then-cache behavior when viewing an activity that lacks weather data. |
