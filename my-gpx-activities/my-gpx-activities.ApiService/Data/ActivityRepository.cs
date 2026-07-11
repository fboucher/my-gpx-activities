using Dapper;
using my_gpx_activities.ApiService.Models;

namespace my_gpx_activities.ApiService.Data;

public class ActivityRepository : IActivityRepository
{
    private readonly IDatabaseConnectionFactory _connectionFactory;
    private readonly IActivityTypeRepository _activityTypeRepository;

    public ActivityRepository(IDatabaseConnectionFactory connectionFactory, IActivityTypeRepository activityTypeRepository)
    {
        _connectionFactory = connectionFactory;
        _activityTypeRepository = activityTypeRepository;
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
                track_data_json,
                weather_data,
                created_at
            FROM activities
            ORDER BY start_date_time DESC
            """);

        return activities.Select(MapToActivity).ToList();
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
                track_data_json,
                weather_data,
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
                track_data_json,
                weather_data,
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
                @TrackDataJson::jsonb,
                @WeatherDataJson::jsonb,
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
                activity.TrackDataJson,
                activity.WeatherDataJson,
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
                track_coordinates_json = @TrackCoordinatesJson::jsonb,
                track_data_json = @TrackDataJson::jsonb,
                weather_data = @WeatherDataJson::jsonb
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
                activity.TrackCoordinatesJson,
                activity.TrackDataJson,
                activity.WeatherDataJson
            });

        return rowsAffected > 0;
    }

    public async Task<Activity?> UpdateActivityPartialAsync(Guid id, string? title, string? activityType)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();

        if (activityType != null)
        {
            var existingType = await _activityTypeRepository.GetActivityTypeByNameAsync(activityType);
            if (existingType == null)
            {
                await _activityTypeRepository.CreateActivityTypeAsync(new ApiService.Models.ActivityType
                {
                    Name = activityType,
                    Icon = "directions_run",
                    Color = "#888888",
                    IsDefault = false
                });
            }
        }

        if (title == null && activityType == null) return null;

        var titleValue = title;
        var activityTypeValue = activityType;

        var updated = await connection.QuerySingleOrDefaultAsync<ActivityDto>("""
            UPDATE activities SET
                title = COALESCE(NULLIF(@Title, ''), title),
                activity_type = COALESCE(NULLIF(@ActivityType, ''), activity_type)
            WHERE id = @Id
            RETURNING 
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
                track_data_json,
                weather_data,
                created_at
            """, new { Id = id, Title = titleValue, ActivityType = activityTypeValue });

        return updated != null ? MapToActivity(updated) : null;
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

    public async Task<IEnumerable<HeatMapActivity>> GetActivitiesForHeatMapAsync(DateOnly? from, DateOnly? to, string[]? sportTypes)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();

        var conditions = new List<string>();
        if (from.HasValue)
            conditions.Add("start_date_time >= @From");
        if (to.HasValue)
            conditions.Add("start_date_time < @To");
        if (sportTypes is { Length: > 0 })
            conditions.Add("activity_type = ANY(@SportTypes)");

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;

        var sql = $"""
            SELECT
                id,
                title,
                activity_type,
                track_coordinates_json
            FROM activities
            {where}
            ORDER BY start_date_time DESC
            """;

        var rows = await connection.QueryAsync<HeatMapDto>(sql, new
        {
            From = from.HasValue ? from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : (DateTime?)null,
            To = to.HasValue ? to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : (DateTime?)null,
            SportTypes = sportTypes
        });

        return rows.Select(dto => new HeatMapActivity
        {
            ActivityId = dto.Id,
            ActivityName = dto.Title,
            SportType = dto.Activity_Type,
            TrackPoints = string.IsNullOrEmpty(dto.Track_Coordinates_Json)
                ? []
                : System.Text.Json.JsonSerializer.Deserialize<double[][]>(dto.Track_Coordinates_Json) ?? []
        });
    }

    
    public async Task<GlobalStatistics> GetGlobalStatisticsAsync()
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();
        var streakData = await connection.QueryAsync<StreakWeek>("""
            WITH weekly_activity AS (SELECT DISTINCT EXTRACT(YEAR FROM start_date_time)::int as year, EXTRACT(WEEK FROM start_date_time)::int as week FROM activities ORDER BY year, week),
            week_with_row AS (SELECT year, week, year * 100 + week as year_week, ROW_NUMBER() OVER (ORDER BY year, week) as rn FROM weekly_activity),
            streak_groups AS (SELECT year, week, year_week, year_week - rn as streak_group FROM week_with_row),
            streaks AS (SELECT MIN(year_week) as first_week, MAX(year_week) as last_week, COUNT(*) as streak_length FROM streak_groups GROUP BY streak_group)
            SELECT first_week, last_week, streak_length FROM streaks ORDER BY streak_length DESC, last_week DESC
            """);
        var streaks = streakData.ToList();
        var longestStreak = streaks.FirstOrDefault()?.Streak_Length ?? 0;
        var now = DateTime.UtcNow;
        var currentYearWeek = (now.Year * 100) + (int)System.Globalization.ISOWeek.GetWeekOfYear(now);
        var currentStreak = streaks.FirstOrDefault(s => s.Last_Week >= currentYearWeek - 1);
        var currentWeekStreak = currentStreak?.Streak_Length ?? 0;
        var activityDaysByWeekDtos = await connection.QueryAsync<DayActivityCountDto>("""
            SELECT EXTRACT(WEEK FROM start_date_time)::int as week_number, EXTRACT(YEAR FROM start_date_time)::int as year, COUNT(DISTINCT DATE(start_date_time)) as days_with_activities
            FROM activities WHERE start_date_time >= NOW() - INTERVAL '12 weeks' GROUP BY year, week_number ORDER BY year DESC, week_number DESC
            """);
        var activityDaysByMonthDtos = await connection.QueryAsync<MonthActivityCountDto>("""
            SELECT EXTRACT(MONTH FROM start_date_time)::int as month, EXTRACT(YEAR FROM start_date_time)::int as year, COUNT(DISTINCT DATE(start_date_time)) as days_with_activities
            FROM activities WHERE start_date_time >= NOW() - INTERVAL '12 months' GROUP BY year, month ORDER BY year DESC, month DESC
            """);
        var yearRecapDtos = await connection.QueryAsync<YearSummaryDto>("""
            SELECT EXTRACT(YEAR FROM start_date_time)::int as year, COUNT(*)::int as total_activities, (SUM(distance_meters) / 1000.0) as total_distance_km,
            (SUM(EXTRACT(EPOCH FROM (end_date_time - start_date_time))) / 60.0) as total_duration_minutes FROM activities GROUP BY year ORDER BY year DESC
            """);
        var sportCountDtos = await connection.QueryAsync<SportCountDto>("""
            SELECT activity_type as sport_type, COUNT(*)::int as count FROM activities GROUP BY activity_type ORDER BY count DESC
            """);
        var activityDaysByWeek = activityDaysByWeekDtos.Select(d => new DayActivityCount(d.Week_Number, d.Year, d.Days_With_Activities));
        var activityDaysByMonth = activityDaysByMonthDtos.Select(d => new MonthActivityCount(d.Month, d.Year, d.Days_With_Activities));
        var yearRecap = yearRecapDtos.Select(d => new YearSummary(d.Year, d.Total_Activities, d.Total_Distance_Km, d.Total_Duration_Minutes));
        var sportCounts = sportCountDtos.Select(d => new SportCount(d.Sport_Type, d.Count));
        return new GlobalStatistics(currentWeekStreak, longestStreak, activityDaysByWeek, activityDaysByMonth, yearRecap, sportCounts);
    }

    public async Task UpdateWeatherDataAsync(Guid activityId, string? weatherDataJson)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync("""
            UPDATE activities SET weather_data = @WeatherDataJson::jsonb WHERE id = @Id
            """, new { Id = activityId, WeatherDataJson = weatherDataJson });
    }

    public async Task SaveBestSegmentsAsync(IEnumerable<BestSegment> segments)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();
        foreach (var segment in segments)
        {
            await connection.ExecuteAsync("""
                INSERT INTO activity_best_segments (id, activity_id, distance_meters, speed_ms, total_time_seconds, start_track_point_index, end_track_point_index, created_at)
                VALUES (@Id, @ActivityId, @DistanceMeters, @SpeedMs, @TotalTimeSeconds, @StartTrackPointIndex, @EndTrackPointIndex, @CreatedAt)
                ON CONFLICT DO NOTHING
                """, segment);
        }
    }

    public async Task<IEnumerable<BestSegment>> GetBestSegmentsByActivityIdAsync(Guid activityId)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();
        return await connection.QueryAsync<BestSegment>("""
            SELECT id, activity_id AS ActivityId, distance_meters AS DistanceMeters, speed_ms AS SpeedMs,
                   total_time_seconds AS TotalTimeSeconds, start_track_point_index AS StartTrackPointIndex,
                   end_track_point_index AS EndTrackPointIndex, created_at AS CreatedAt
            FROM activity_best_segments
            WHERE activity_id = @ActivityId
            ORDER BY distance_meters
            """, new { ActivityId = activityId });
    }

    public async Task<IEnumerable<ActivityRecord>> GetAllRecordsAsync(string? activityType = null)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();
        var sql = """
            SELECT id, activity_id AS ActivityId, activity_type AS ActivityType, metric, value, year,
                   achieved_at AS AchievedAt, created_at AS CreatedAt
            FROM activity_records
            """;
        if (!string.IsNullOrEmpty(activityType))
            sql += " WHERE activity_type = @ActivityType";
        sql += " ORDER BY activity_type, metric, year NULLS LAST";

        return await connection.QueryAsync<ActivityRecord>(sql, new { ActivityType = activityType });
    }

    public async Task UpsertRecordAsync(ActivityRecord record)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync("""
            INSERT INTO activity_records (id, activity_id, activity_type, metric, value, year, achieved_at, created_at)
            VALUES (@Id, @ActivityId, @ActivityType, @Metric, @Value, @Year, @AchievedAt, @CreatedAt)
            ON CONFLICT (activity_type, metric, year)
            DO UPDATE SET activity_id = @ActivityId, value = @Value, achieved_at = @AchievedAt, created_at = @CreatedAt
            """, record);
    }

    public async Task DeleteRecordsForActivityAsync(Guid activityId)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync("DELETE FROM activity_records WHERE activity_id = @ActivityId",
            new { ActivityId = activityId });
    }

    public async Task RecalculateRecordsAsync(string? activityType = null)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();
        var metrics = new[] { "max_speed_ms", "distance_meters", "elevation_gain_meters" };
        var durationExpr = "EXTRACT(EPOCH FROM (end_date_time - start_date_time))";

        foreach (var metric in metrics)
        {
            var valueCol = metric;
            var metricName = metric switch
            {
                "max_speed_ms" => "MaxSpeed",
                "distance_meters" => "Distance",
                "elevation_gain_meters" => "ElevationGain",
                _ => metric
            };

            var typeFilter = !string.IsNullOrEmpty(activityType) ? " AND activity_type = @ActivityType" : "";

            // All-time records
            await connection.ExecuteAsync($"""
                INSERT INTO activity_records (id, activity_id, activity_type, metric, value, year, achieved_at, created_at)
                SELECT gen_random_uuid(), id, activity_type, @MetricName, {valueCol}, NULL, start_date_time, NOW()
                FROM activities
                WHERE {valueCol} = (
                    SELECT MAX({valueCol}) FROM activities WHERE {valueCol} > 0{typeFilter}
                ) AND {valueCol} > 0{typeFilter}
                LIMIT 1
                ON CONFLICT (activity_type, metric, year)
                DO UPDATE SET activity_id = EXCLUDED.activity_id, value = EXCLUDED.value, achieved_at = EXCLUDED.achieved_at
                """, new { MetricName = metricName, ActivityType = activityType });

            // Yearly records
            await connection.ExecuteAsync($"""
                INSERT INTO activity_records (id, activity_id, activity_type, metric, value, year, achieved_at, created_at)
                SELECT DISTINCT ON (activity_type, EXTRACT(YEAR FROM start_date_time))
                    gen_random_uuid(), id, activity_type, @MetricName, {valueCol},
                    EXTRACT(YEAR FROM start_date_time)::int, start_date_time, NOW()
                FROM activities
                WHERE {valueCol} > 0{typeFilter}
                ORDER BY activity_type, EXTRACT(YEAR FROM start_date_time), {valueCol} DESC
                ON CONFLICT (activity_type, metric, year)
                DO UPDATE SET activity_id = EXCLUDED.activity_id, value = EXCLUDED.value, achieved_at = EXCLUDED.achieved_at
                """, new { MetricName = metricName, ActivityType = activityType });
        }

        // Duration records (special handling)
        var durationMetric = "Duration";
        var durationTypeFilter = !string.IsNullOrEmpty(activityType) ? " AND activity_type = @ActivityType2" : "";

        await connection.ExecuteAsync($"""
            INSERT INTO activity_records (id, activity_id, activity_type, metric, value, year, achieved_at, created_at)
            SELECT gen_random_uuid(), id, activity_type, @MetricName, {durationExpr}, NULL, start_date_time, NOW()
            FROM activities
            WHERE {durationExpr} = (
                SELECT MAX({durationExpr}) FROM activities WHERE {durationExpr} > 0{durationTypeFilter}
            ) AND {durationExpr} > 0{durationTypeFilter}
            LIMIT 1
            ON CONFLICT (activity_type, metric, year)
            DO UPDATE SET activity_id = EXCLUDED.activity_id, value = EXCLUDED.value, achieved_at = EXCLUDED.achieved_at
            """, new { MetricName = durationMetric, ActivityType2 = activityType });

        await connection.ExecuteAsync($"""
            INSERT INTO activity_records (id, activity_id, activity_type, metric, value, year, achieved_at, created_at)
            SELECT DISTINCT ON (activity_type, EXTRACT(YEAR FROM start_date_time))
                gen_random_uuid(), id, activity_type, @MetricName, {durationExpr},
                EXTRACT(YEAR FROM start_date_time)::int, start_date_time, NOW()
            FROM activities
            WHERE {durationExpr} > 0{durationTypeFilter}
            ORDER BY activity_type, EXTRACT(YEAR FROM start_date_time), {durationExpr} DESC
            ON CONFLICT (activity_type, metric, year)
            DO UPDATE SET activity_id = EXCLUDED.activity_id, value = EXCLUDED.value, achieved_at = EXCLUDED.achieved_at
            """, new { MetricName = durationMetric, ActivityType2 = activityType });
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
            TrackDataJson = dto.Track_Data_Json,
            WeatherDataJson = dto.Weather_Data,
            CreatedAt = dto.Created_At
        };
    }

    private class HeatMapDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Activity_Type { get; set; } = string.Empty;
        public string? Track_Coordinates_Json { get; set; }
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
        public string? Track_Data_Json { get; set; }
        public string? Weather_Data { get; set; }
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

    private class StreakWeek
    {
        public int First_Week { get; set; }
        public int Last_Week { get; set; }
        public int Streak_Length { get; set; }
    }

    private class DayActivityCountDto
    {
        public int Week_Number { get; set; }
        public int Year { get; set; }
        public int Days_With_Activities { get; set; }
    }

    private class MonthActivityCountDto
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public int Days_With_Activities { get; set; }
    }

    private class YearSummaryDto
    {
        public int Year { get; set; }
        public int Total_Activities { get; set; }
        public double Total_Distance_Km { get; set; }
        public double Total_Duration_Minutes { get; set; }
    }

    private class SportCountDto
    {
        public string Sport_Type { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
