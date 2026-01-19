using Dapper;
using my_gpx_activities.ApiService.Models;

namespace my_gpx_activities.ApiService.Data;

public class ActivityTypeRepository : IActivityTypeRepository
{
    private readonly IDatabaseConnectionFactory _connectionFactory;

    public ActivityTypeRepository(IDatabaseConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<ActivityType>> GetAllActivityTypesAsync()
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();

        var activityTypes = await connection.QueryAsync<ActivityTypeDto>("""
            SELECT 
                id,
                name,
                icon,
                color,
                is_default
            FROM activity_types
            ORDER BY is_default DESC, name ASC
            """);

        return activityTypes.Select(MapToActivityType);
    }

    public async Task<ActivityType?> GetActivityTypeByIdAsync(Guid id)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();

        var activityType = await connection.QuerySingleOrDefaultAsync<ActivityTypeDto>("""
            SELECT 
                id,
                name,
                icon,
                color,
                is_default
            FROM activity_types
            WHERE id = @Id
            """, new { Id = id });

        return activityType != null ? MapToActivityType(activityType) : null;
    }

    public async Task<ActivityType?> GetActivityTypeByNameAsync(string name)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();

        var activityType = await connection.QuerySingleOrDefaultAsync<ActivityTypeDto>("""
            SELECT 
                id,
                name,
                icon,
                color,
                is_default
            FROM activity_types
            WHERE LOWER(name) = LOWER(@Name)
            """, new { Name = name });

        return activityType != null ? MapToActivityType(activityType) : null;
    }

    public async Task<Guid> CreateActivityTypeAsync(ActivityType activityType)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();

        var id = await connection.ExecuteScalarAsync<Guid>("""
            INSERT INTO activity_types (id, name, icon, color, is_default)
            VALUES (@Id, @Name, @Icon, @Color, @IsDefault)
            RETURNING id
            """, new
            {
                activityType.Id,
                activityType.Name,
                activityType.Icon,
                activityType.Color,
                activityType.IsDefault
            });

        return id;
    }

    public async Task<bool> DeleteActivityTypeAsync(Guid id)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync();

        var rowsAffected = await connection.ExecuteAsync(
            "DELETE FROM activity_types WHERE id = @Id AND is_default = FALSE",
            new { Id = id });

        return rowsAffected > 0;
    }

    private static ActivityType MapToActivityType(ActivityTypeDto dto)
    {
        return new ActivityType
        {
            Id = dto.Id,
            Name = dto.Name,
            Icon = dto.Icon,
            Color = dto.Color,
            IsDefault = dto.Is_Default
        };
    }

    // DTO class to handle PostgreSQL snake_case column names
    private class ActivityTypeDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public bool Is_Default { get; set; }
    }
}
