using System.Text.Json;
using my_gpx_activities.ApiService.Data;
using my_gpx_activities.ApiService.Models;
using my_gpx_activities.ApiService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("gpxactivities");

builder.Services.AddScoped<IDatabaseConnectionFactory, DatabaseConnectionFactory>();
builder.Services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
builder.Services.AddScoped<IActivityTypeRepository, ActivityTypeRepository>();
builder.Services.AddScoped<IGpxParserService, GpxParserService>();
builder.Services.AddScoped<IFitParserService, FitParserService>();
builder.Services.AddScoped<ISmartMergeService, SmartMergeService>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    await dbInitializer.InitializeAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "GPX Activities API is running. Use /api endpoints for activity management.");

app.MapGet("/api/activity-types", async (IActivityTypeRepository repository) =>
{
    var activityTypes = await repository.GetAllActivityTypesAsync();
    return Results.Ok(activityTypes);
})
.WithName("GetActivityTypes");

app.MapPost("/api/activities/import", async (HttpRequest request, IGpxParserService gpxParser, IActivityRepository activityRepository) =>
{
    try
    {
        var form = await request.ReadFormAsync();
        var file = form.Files.GetFile("gpx");

        if (file == null || file.Length == 0)
        {
            return Results.BadRequest("No GPX file provided");
        }

        if (!file.FileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest("File must be a GPX file");
        }

        await using var stream = file.OpenReadStream();
        var activityData = await gpxParser.ParseGpxAsync(stream);

        var trackCoordinates = activityData.TrackPoints
            .Select(tp => new[] { tp.Latitude, tp.Longitude })
            .ToList();

        var trackData = activityData.TrackPoints
            .Select(tp => new double?[]
            {
                tp.Latitude,
                tp.Longitude,
                tp.Elevation,
                tp.HeartRate,
                tp.Time.HasValue ? (double?)new DateTimeOffset(tp.Time.Value).ToUnixTimeMilliseconds() : null,
                tp.Cadence
            })
            .ToList();

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Title = activityData.Title,
            StartDateTime = activityData.StartDateTime,
            EndDateTime = activityData.EndDateTime,
            ActivityType = activityData.ActivityType,
            DistanceMeters = activityData.DistanceMeters,
            ElevationGainMeters = activityData.ElevationGainMeters,
            ElevationLossMeters = activityData.ElevationLossMeters,
            AverageSpeedMs = activityData.AverageSpeedMs,
            MaxSpeedMs = activityData.MaxSpeedMs,
            TrackPointCount = activityData.TrackPoints.Count,
            TrackCoordinatesJson = JsonSerializer.Serialize(trackCoordinates),
            TrackDataJson = JsonSerializer.Serialize(trackData),
            CreatedAt = DateTime.UtcNow
        };

        await activityRepository.CreateActivityAsync(activity);

        var response = new
        {
            activity.Id,
            activity.Title,
            activity.StartDateTime,
            activity.EndDateTime,
            activity.ActivityType,
            activity.DistanceMeters,
            activity.ElevationGainMeters,
            activity.ElevationLossMeters,
            activity.AverageSpeedMs,
            activity.MaxSpeedMs,
            TrackPoints = activity.TrackPointCount,
            activity.CreatedAt,
            TrackCoordinates = trackCoordinates
        };

        return Results.Created($"/api/activities/{activity.Id}", response);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error processing GPX file: {ex.Message}");
    }
})
.WithName("ImportGpxActivity");

app.MapPost("/api/activities/import/batch", async (HttpRequest request, IGpxParserService gpxParser, IActivityRepository activityRepository) =>
{
    try
    {
        var form = await request.ReadFormAsync();
        var files = form.Files.GetFiles("gpx");

        if (files == null || files.Count == 0)
        {
            return Results.BadRequest("No GPX files provided");
        }

        var results = new List<object>();
        var tasks = files.Select(async file =>
        {
            var result = new
            {
                FileName = file.FileName,
                Success = false,
                ErrorMessage = (string?)null,
                Activity = (object?)null
            };

            try
            {
                if (!file.FileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
                {
                    return result with { ErrorMessage = "File must be a GPX file" };
                }

                await using var stream = file.OpenReadStream();
                var activityData = await gpxParser.ParseGpxAsync(stream);

                var trackCoordinates = activityData.TrackPoints
                    .Select(tp => new[] { tp.Latitude, tp.Longitude })
                    .ToList();

                var trackData = activityData.TrackPoints
                    .Select(tp => new double?[]
                    {
                        tp.Latitude,
                        tp.Longitude,
                        tp.Elevation,
                        tp.HeartRate,
                        tp.Time.HasValue ? (double?)new DateTimeOffset(tp.Time.Value).ToUnixTimeMilliseconds() : null
                    })
                    .ToList();

                var activity = new Activity
                {
                    Id = Guid.NewGuid(),
                    Title = activityData.Title,
                    StartDateTime = activityData.StartDateTime,
                    EndDateTime = activityData.EndDateTime,
                    ActivityType = activityData.ActivityType,
                    DistanceMeters = activityData.DistanceMeters,
                    ElevationGainMeters = activityData.ElevationGainMeters,
                    ElevationLossMeters = activityData.ElevationLossMeters,
                    AverageSpeedMs = activityData.AverageSpeedMs,
                    MaxSpeedMs = activityData.MaxSpeedMs,
                    TrackPointCount = activityData.TrackPoints.Count,
                    TrackCoordinatesJson = JsonSerializer.Serialize(trackCoordinates),
                    TrackDataJson = JsonSerializer.Serialize(trackData),
                    CreatedAt = DateTime.UtcNow
                };

                await activityRepository.CreateActivityAsync(activity);

                var activityResponse = new
                {
                    activity.Id,
                    activity.Title,
                    activity.StartDateTime,
                    activity.EndDateTime,
                    activity.ActivityType,
                    activity.DistanceMeters,
                    activity.ElevationGainMeters,
                    activity.ElevationLossMeters,
                    activity.AverageSpeedMs,
                    activity.MaxSpeedMs,
                    TrackPoints = activity.TrackPointCount,
                    activity.CreatedAt,
                    TrackCoordinates = trackCoordinates
                };

                return result with { Success = true, Activity = activityResponse };
            }
            catch (Exception ex)
            {
                return result with { ErrorMessage = ex.Message };
            }
        });

        var processedResults = await Task.WhenAll(tasks);
        var successCount = processedResults.Count(r => r.Success);
        var totalCount = processedResults.Length;

        var batchResponse = new
        {
            TotalFiles = totalCount,
            SuccessCount = successCount,
            FailureCount = totalCount - successCount,
            Results = processedResults
        };

        return Results.Ok(batchResponse);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error processing batch import: {ex.Message}");
    }
})
.WithName("BatchImportGpxActivities")
.WithDescription("Import multiple GPX files in a single request");

app.MapPost("/api/activities/smart-merge", async (HttpRequest request, ISmartMergeService smartMerge) =>
{
    try
    {
        var form = await request.ReadFormAsync();
        var gpxFile = form.Files.GetFile("gpx");
        var fitFile = form.Files.GetFile("fit");

        if (gpxFile == null || gpxFile.Length == 0)
            return Results.BadRequest("No GPX file provided (field: 'gpx').");

        if (fitFile == null || fitFile.Length == 0)
            return Results.BadRequest("No FIT file provided (field: 'fit').");

        if (!gpxFile.FileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest("The 'gpx' file must have a .gpx extension.");

        if (!fitFile.FileName.EndsWith(".fit", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest("The 'fit' file must have a .fit extension.");

        await using var gpxStream = gpxFile.OpenReadStream();
        await using var fitStream = fitFile.OpenReadStream();

        var mergedGpx = await smartMerge.MergeAsync(gpxStream, fitStream);

        return Results.Content(mergedGpx, "application/gpx+xml", System.Text.Encoding.UTF8);
    }
    catch (InvalidDataException ex)
    {
        return Results.BadRequest($"Invalid file format: {ex.Message}");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error merging files: {ex.Message}");
    }
})
.WithName("SmartMergeGpxFit")
.WithDescription("Merge a GPX file with a FIT file, enriching trackpoints with heart-rate data matched by timestamp.");

app.MapPost("/api/activities/smart-merge/import", async (HttpRequest request, ISmartMergeService smartMerge, IGpxParserService gpxParser, IActivityRepository activityRepository) =>
{
    try
    {
        var form = await request.ReadFormAsync();
        var gpxFile = form.Files.GetFile("gpx");
        var fitFile = form.Files.GetFile("fit");

        if (gpxFile == null || gpxFile.Length == 0)
            return Results.BadRequest("No GPX file provided (field: 'gpx').");

        if (fitFile == null || fitFile.Length == 0)
            return Results.BadRequest("No FIT file provided (field: 'fit').");

        if (!gpxFile.FileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest("The 'gpx' file must have a .gpx extension.");

        if (!fitFile.FileName.EndsWith(".fit", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest("The 'fit' file must have a .fit extension.");

        await using var gpxStream = gpxFile.OpenReadStream();
        await using var fitStream = fitFile.OpenReadStream();

        var mergedGpx = await smartMerge.MergeAsync(gpxStream, fitStream);

        await using var mergedStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(mergedGpx));
        var activityData = await gpxParser.ParseGpxAsync(mergedStream);

        var trackCoordinates = activityData.TrackPoints
            .Select(tp => new[] { tp.Latitude, tp.Longitude })
            .ToList();

        var trackData = activityData.TrackPoints
            .Select(tp => new double?[]
            {
                tp.Latitude,
                tp.Longitude,
                tp.Elevation,
                tp.HeartRate,
                tp.Time.HasValue ? (double?)new DateTimeOffset(tp.Time.Value).ToUnixTimeMilliseconds() : null,
                tp.Cadence
            })
            .ToList();

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Title = activityData.Title,
            StartDateTime = activityData.StartDateTime,
            EndDateTime = activityData.EndDateTime,
            ActivityType = activityData.ActivityType,
            DistanceMeters = activityData.DistanceMeters,
            ElevationGainMeters = activityData.ElevationGainMeters,
            ElevationLossMeters = activityData.ElevationLossMeters,
            AverageSpeedMs = activityData.AverageSpeedMs,
            MaxSpeedMs = activityData.MaxSpeedMs,
            TrackPointCount = activityData.TrackPoints.Count,
            TrackCoordinatesJson = JsonSerializer.Serialize(trackCoordinates),
            TrackDataJson = JsonSerializer.Serialize(trackData),
            CreatedAt = DateTime.UtcNow
        };

        await activityRepository.CreateActivityAsync(activity);

        var response = new
        {
            activity.Id,
            activity.Title,
            activity.StartDateTime,
            activity.EndDateTime,
            activity.ActivityType,
            activity.DistanceMeters,
            activity.ElevationGainMeters,
            activity.ElevationLossMeters,
            activity.AverageSpeedMs,
            activity.MaxSpeedMs,
            TrackPoints = activity.TrackPointCount,
            activity.CreatedAt,
            TrackCoordinates = trackCoordinates
        };

        return Results.Created($"/api/activities/{activity.Id}", response);
    }
    catch (InvalidDataException ex)
    {
        return Results.BadRequest($"Invalid file format: {ex.Message}");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error processing files: {ex.Message}");
    }
})
.WithName("SmartMergeAndImportActivity")
.WithDescription("Merge a GPX file with a FIT file to enrich heart-rate data, then save as a new activity");

app.MapGet("/api/activities", async (IActivityRepository repository) =>
{
    var activities = await repository.GetAllActivitiesAsync();

    var response = activities.Select(a => new
    {
        a.Id,
        a.Title,
        a.StartDateTime,
        a.EndDateTime,
        a.ActivityType,
        a.DistanceMeters,
        a.ElevationGainMeters,
        a.ElevationLossMeters,
        a.AverageSpeedMs,
        a.MaxSpeedMs,
        TrackPoints = a.TrackPointCount,
        a.CreatedAt
    });

    return Results.Ok(response);
})
.WithName("GetActivities");

app.MapGet("/api/activities/{id}", async (Guid id, IActivityRepository repository) =>
{
    var activity = await repository.GetActivityByIdAsync(id);

    if (activity == null)
    {
        return Results.NotFound();
    }

    List<double[]>? trackCoordinates = null;
    if (!string.IsNullOrEmpty(activity.TrackCoordinatesJson))
    {
        trackCoordinates = JsonSerializer.Deserialize<List<double[]>>(activity.TrackCoordinatesJson);
    }

    List<double?[]>? trackData = null;
    if (!string.IsNullOrEmpty(activity.TrackDataJson))
    {
        trackData = JsonSerializer.Deserialize<List<double?[]>>(activity.TrackDataJson);
    }

    var response = new
    {
        activity.Id,
        activity.Title,
        activity.StartDateTime,
        activity.EndDateTime,
        activity.ActivityType,
        activity.DistanceMeters,
        activity.ElevationGainMeters,
        activity.ElevationLossMeters,
        activity.AverageSpeedMs,
        activity.MaxSpeedMs,
        TrackPoints = activity.TrackPointCount,
        activity.CreatedAt,
        TrackCoordinates = trackCoordinates,
        TrackData = trackData
    };

    return Results.Ok(response);
})
.WithName("GetActivity");

app.MapDelete("/api/activities/{id}", async (Guid id, IActivityRepository repository) =>
{
    var deleted = await repository.DeleteActivityAsync(id);
    return deleted ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteActivity");

app.MapGet("/api/statistics/by-sport", async (IActivityRepository repository) =>
{
    var statistics = await repository.GetStatisticsBySportAsync();
    return Results.Ok(statistics);
})
.WithName("GetStatisticsBySport")
.WithDescription("Get aggregated statistics grouped by sport type");

app.MapGet("/api/activities/heatmap", async (
    IActivityRepository repository,
    DateOnly? dateFrom,
    DateOnly? dateTo,
    string? sportTypes) =>
{
    string[]? sportTypesArray = string.IsNullOrWhiteSpace(sportTypes)
        ? null
        : sportTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var activities = await repository.GetActivitiesForHeatMapAsync(dateFrom, dateTo, sportTypesArray);
    return Results.Ok(activities);
})
.WithName("GetHeatMapActivities")
.WithDescription("Get GPS track points for all activities, optionally filtered by date range and sport types, for heat map rendering");

app.MapDefaultEndpoints();

await app.RunAsync();