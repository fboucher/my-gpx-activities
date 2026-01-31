using Dapper;
using my_gpx_activities.ApiService.Models;

namespace my_gpx_activities.ApiService.Data;

public class ActivityRepository : IActivityRepository
{
    private readonly IDatabaseConnectionFactory _connectionFactory;

    public ActivityRepository(IDatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<Activity>> GetAllActivitiesAsync()
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();

        var activities = await connection.QueryAsync<ActivityDto>("""
            SELECT 
                id,
                title,
                start_date_time,
                end_date_time,
                activity_type,
                distance_meters,
                elevation_gain_meters,
                elevation_loss_meters,
                average_speed_ms,
                max_speed_ms,
                track_point_count,
                track_coordinates_json,
                created_at
            FROM activities
            ORDER BY start_date_time DESC
            """);

        return activities.Select(MapToActivity);
    }

    public async Task<Activity?> GetActivityByIdAsync(Guid id)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();

        var activity = await connection.QuerySingleOrDefaultAsync<ActivityDto>("""
            SELECT 
                id,
                title,
                start_date_time,
                end_date_time,
                activity_type,
                distance_meters,
                elevation_gain_meters,
                elevation_loss_meters,
                average_speed_ms,
                max_speed_ms,
                track_point_count,
                track_coordinates_json,
                created_at
            FROM activities
            WHERE id = @Id
            """, new { Id = id });

        return activity != null ? MapToActivity(activity) : null;
    }

    public async Task<Guid> CreateActivityAsync(Activity activity)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();

        var id = await connection.ExecuteScalarAsync<Guid>("""
            INSERT INTO activities (
                id,
                title,
                start_date_time,
                end_date_time,
                activity_type,
                distance_meters,
                elevation_gain_meters,
                elevation_loss_meters,
                average_speed_ms,
                max_speed_ms,
                track_point_count,
                track_coordinates_json,
                created_at
            ) VALUES (
                @Id,
                @Title,
                @StartDateTime,
                @EndDateTime,
                @ActivityType,
                @DistanceMeters,
                @ElevationGainMeters,
                @ElevationLossMeters,
                @AverageSpeedMs,
                @MaxSpeedMs,
                @TrackPointCount,
                @TrackCoordinatesJson::jsonb,
                @CreatedAt
            )
            RETURNING id
            """, new
            {
                activity.Id,
                activity.Title,
                StartDateTime = activity.StartDateTime,
                EndDateTime = activity.EndDateTime,
                activity.ActivityType,
                activity.DistanceMeters,
                activity.ElevationGainMeters,
                activity.ElevationLossMeters,
                activity.AverageSpeedMs,
                activity.MaxSpeedMs,
                activity.TrackPointCount,
                activity.TrackCoordinatesJson,
                activity.CreatedAt
            });

        return id;
    }

    public async Task<bool> UpdateActivityAsync(Activity activity)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();

        var rowsAffected = await connection.ExecuteAsync("""
            UPDATE activities SET
                title = @Title,
                start_date_time = @StartDateTime,
                end_date_time = @EndDateTime,
                activity_type = @ActivityType,
                distance_meters = @DistanceMeters,
                elevation_gain_meters = @ElevationGainMeters,
                elevation_loss_meters = @ElevationLossMeters,
                average_speed_ms = @AverageSpeedMs,
                max_speed_ms = @MaxSpeedMs,
                track_point_count = @TrackPointCount,
                track_coordinates_json = @TrackCoordinatesJson::jsonb
            WHERE id = @Id
            """, new
            {
                activity.Id,
                activity.Title,
                StartDateTime = activity.StartDateTime,
                EndDateTime = activity.EndDateTime,
                activity.ActivityType,
                activity.DistanceMeters,
                activity.ElevationGainMeters,
                activity.ElevationLossMeters,
                activity.AverageSpeedMs,
                activity.MaxSpeedMs,
                activity.TrackPointCount,
                activity.TrackCoordinatesJson
            });

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteActivityAsync(Guid id)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();

        var rowsAffected = await connection.ExecuteAsync(
            "DELETE FROM activities WHERE id = @Id",
            new { Id = id });

        return rowsAffected > 0;
    }

    public async Task<IEnumerable<SportStatistics>> GetStatisticsBySportAsync()
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();

        var stats = await connection.QueryAsync<SportStatisticsDto>("""
            SELECT 
                a.activity_type as sport_name,
                at.icon,
                at.color,
                COUNT(*) as total_activities,
                SUM(a.distance_meters) as total_distance_meters,
                SUM(EXTRACT(EPOCH FROM (a.end_date_time - a.start_date_time))) as total_duration_seconds,
                AVG(a.average_speed_ms) as average_speed_ms,
                MAX(a.max_speed_ms) as max_speed_ms,
                MAX(EXTRACT(EPOCH FROM (a.end_date_time - a.start_date_time))) as max_duration_seconds,
                SUM(a.elevation_gain_meters) as total_elevation_gain_meters
            FROM activities a
            LEFT JOIN activity_types at ON a.activity_type = at.name
            GROUP BY a.activity_type, at.icon, at.color
            ORDER BY total_activities DESC
            """);

        return stats.Select(dto => new SportStatistics(
            dto.Sport_Name ?? "Unknown",
            dto.Icon,
            dto.Color,
            dto.Total_Activities,
            dto.Total_Distance_Meters,
            dto.Total_Duration_Seconds,
            dto.Average_Speed_Ms,
            dto.Max_Speed_Ms,
            dto.Max_Duration_Seconds,
            dto.Total_Elevation_Gain_Meters
        ));
    }

    private static Activity MapToActivity(ActivityDto dto)
    {
        return new Activity
        {
            Id = dto.Id,
            Title = dto.Title,
            StartDateTime = dto.Start_Date_Time,
            EndDateTime = dto.End_Date_Time,
            ActivityType = dto.Activity_Type,
            DistanceMeters = dto.Distance_Meters,
            ElevationGainMeters = dto.Elevation_Gain_Meters,
            ElevationLossMeters = dto.Elevation_Loss_Meters,
            AverageSpeedMs = dto.Average_Speed_Ms,
            MaxSpeedMs = dto.Max_Speed_Ms,
            TrackPointCount = dto.Track_Point_Count,
            TrackCoordinatesJson = dto.Track_Coordinates_Json,
            CreatedAt = dto.Created_At
        };
    }

    // DTO class to handle PostgreSQL snake_case column names
    private class ActivityDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime Start_Date_Time { get; set; }
        public DateTime End_Date_Time { get; set; }
        public string Activity_Type { get; set; } = string.Empty;
        public double Distance_Meters { get; set; }
        public double Elevation_Gain_Meters { get; set; }
        public double Elevation_Loss_Meters { get; set; }
        public double Average_Speed_Ms { get; set; }
        public double Max_Speed_Ms { get; set; }
        public int Track_Point_Count { get; set; }
        public string? Track_Coordinates_Json { get; set; }
        public DateTime Created_At { get; set; }
    }

    private class SportStatisticsDto
    {
        public string? Sport_Name { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public int Total_Activities { get; set; }
        public double Total_Distance_Meters { get; set; }
        public double Total_Duration_Seconds { get; set; }
        public double Average_Speed_Ms { get; set; }
        public double Max_Speed_Ms { get; set; }
        public double Max_Duration_Seconds { get; set; }
        public double Total_Elevation_Gain_Meters { get; set; }
    }
}
