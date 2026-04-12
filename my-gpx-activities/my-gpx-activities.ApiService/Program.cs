using System.Text.Json;
using my_gpx_activities.ApiService.Data;
using my_gpx_activities.ApiService.Models;
using my_gpx_activities.ApiService.Models.Merge;
using my_gpx_activities.ApiService.Models.Strava;
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
builder.Services.AddScoped<IStravaImportService, StravaImportService>();
builder.Services.AddScoped<IActivityMergeService, ActivityMergeService>();
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
            TrackCoordinates = trackCoordinates,
            TrackData = trackData
        };

        return Results.Created($"/api/activities/{activity.Id}", response);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error processing GPX file during import");
        return Results.Problem("An error occurred while processing the GPX file. Please try again.");
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
                    TrackCoordinates = trackCoordinates,
                    TrackData = trackData
                };

                return result with { Success = true, Activity = activityResponse };
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Error processing file {FileName} in batch import", file.FileName);
                return result with { ErrorMessage = "An error occurred while processing this file." };
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
        app.Logger.LogError(ex, "Error processing batch import request");
        return Results.Problem("An error occurred while processing the batch import. Please try again.");
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
        app.Logger.LogWarning(ex, "Invalid file format");
        return Results.BadRequest("Invalid file format. Please ensure both files are valid GPX and FIT files.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error merging GPX and FIT files");
        return Results.Problem("An error occurred while merging the files. Please try again.");
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
            TrackCoordinates = trackCoordinates,
            TrackData = trackData
        };

        return Results.Created($"/api/activities/{activity.Id}", response);
    }
    catch (InvalidDataException ex)
    {
        app.Logger.LogWarning(ex, "Invalid file format");
        return Results.BadRequest("Invalid file format. Please ensure both files are valid GPX and FIT files.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error processing smart merge import");
        return Results.Problem("An error occurred while processing the files. Please try again.");
    }
})
.WithName("SmartMergeAndImportActivity")
.WithDescription("Merge a GPX file with a FIT file to enrich heart-rate data, then save as a new activity");

app.MapPost("/api/activities/import/strava", async (
    StravaImportRequest request,
    IStravaImportService stravaImportService,
    Npgsql.NpgsqlDataSource dataSource) =>
{
    try
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        var result = await stravaImportService.ImportAsync(request.Activity, request.Streams, conn);

        if (result.IsSuccess)
        {
            return Results.Ok(new { id = result.ActivityId });
        }
        else
        {
            return Results.Ok(new { duplicate = true, message = result.Message });
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error importing Strava activity");
        return Results.Problem("An error occurred while importing the Strava activity. Please try again.");
    }
})
.WithName("ImportStravaActivity")
.WithDescription("Import a Strava activity from JSON envelope containing activity and optional streams data");

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

app.MapPatch("/api/activities/{id}", async (Guid id, UpdateActivityRequest request, IActivityRepository repository) =>
{
    var updated = await repository.UpdateActivityPartialAsync(id, request.Title, request.ActivityType);
    if (updated == null) return Results.NotFound();

    List<double[]>? trackCoordinates = null;
    if (!string.IsNullOrEmpty(updated.TrackCoordinatesJson))
        trackCoordinates = JsonSerializer.Deserialize<List<double[]>>(updated.TrackCoordinatesJson);

    List<double?[]>? trackData = null;
    if (!string.IsNullOrEmpty(updated.TrackDataJson))
        trackData = JsonSerializer.Deserialize<List<double?[]>>(updated.TrackDataJson);

    var response = new
    {
        updated.Id,
        updated.Title,
        updated.StartDateTime,
        updated.EndDateTime,
        updated.ActivityType,
        updated.DistanceMeters,
        updated.ElevationGainMeters,
        updated.ElevationLossMeters,
        updated.AverageSpeedMs,
        updated.MaxSpeedMs,
        TrackPoints = updated.TrackPointCount,
        updated.CreatedAt,
        TrackCoordinates = trackCoordinates,
        TrackData = trackData
    };

    return Results.Ok(response);
})
.WithName("PatchActivity")
.WithDescription("Partially update an activity's title and/or sport type");

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

app.MapGet("/api/activities/merge/preview", async (
    Guid activityAId,
    Guid activityBId,
    IActivityMergeService mergeService) =>
{
    try
    {
        var preview = await mergeService.GetMergePreviewAsync(activityAId, activityBId);
        return Results.Ok(preview);
    }
    catch (KeyNotFoundException ex)
    {
        app.Logger.LogWarning(ex, "Activity not found");
        return Results.NotFound("One or both activities not found.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error generating merge preview");
        return Results.Problem("An error occurred while generating the merge preview. Please try again.");
    }
})
.WithName("GetMergePreview")
.WithDescription("Returns a merge preview including suggested mode and detected channels for two activities");

app.MapPost("/api/activities/merge", async (MergeRequest request, IActivityMergeService mergeService) =>
{
    try
    {
        var newId = await mergeService.MergeActivitiesAsync(request);
        return Results.Created($"/api/activities/{newId}", new { id = newId });
    }
    catch (KeyNotFoundException ex)
    {
        app.Logger.LogWarning(ex, "Activity not found");
        return Results.NotFound("One or both activities not found.");
    }
    catch (ArgumentException ex)
    {
        app.Logger.LogWarning(ex, "Invalid merge request");
        return Results.BadRequest("Invalid merge request. Please check your input and try again.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error merging activities");
        return Results.Problem("An error occurred while merging the activities. Please try again.");
    }
})
.WithName("MergeActivities")
.WithDescription("Merge two activities into a new standalone activity using append (concatenate) or merge (overlapping) mode");

app.MapDefaultEndpoints();

await app.RunAsync();

public record UpdateActivityRequest(string? Title, string? ActivityType);