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

app.MapDefaultEndpoints();

await app.RunAsync();