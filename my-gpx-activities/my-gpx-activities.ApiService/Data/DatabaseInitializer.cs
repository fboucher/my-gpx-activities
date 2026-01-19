using Dapper;

namespace my_gpx_activities.ApiService.Data;

public interface IDatabaseInitializer
{
    Task InitializeAsync();
}

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IDatabaseConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseInitializer> _logger;
    private const int MaxRetries = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public DatabaseInitializer(IDatabaseConnectionFactory connectionFactory, ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Attempting to connect to database (attempt {Attempt}/{MaxRetries})...", attempt, MaxRetries);
                
                await using var connection = await _connectionFactory.CreateConnectionAsync();

                _logger.LogInformation("Connected to database. Initializing schema...");

                // Create activity_types table
                await connection.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS activity_types (
                        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                        name VARCHAR(50) NOT NULL UNIQUE,
                        icon VARCHAR(50),
                        color VARCHAR(7),
                        is_default BOOLEAN NOT NULL DEFAULT FALSE,
                        created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                    """);

                // Create activities table
                await connection.ExecuteAsync("""
                    CREATE TABLE IF NOT EXISTS activities (
                        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                        title VARCHAR(200) NOT NULL,
                        start_date_time TIMESTAMP WITH TIME ZONE NOT NULL,
                        end_date_time TIMESTAMP WITH TIME ZONE NOT NULL,
                        activity_type VARCHAR(50) NOT NULL,
                        distance_meters DOUBLE PRECISION NOT NULL DEFAULT 0,
                        elevation_gain_meters DOUBLE PRECISION NOT NULL DEFAULT 0,
                        elevation_loss_meters DOUBLE PRECISION NOT NULL DEFAULT 0,
                        average_speed_ms DOUBLE PRECISION NOT NULL DEFAULT 0,
                        max_speed_ms DOUBLE PRECISION NOT NULL DEFAULT 0,
                        track_point_count INTEGER NOT NULL DEFAULT 0,
                        track_coordinates_json JSONB,
                        created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                    """);

                // Create index on activities for faster queries
                await connection.ExecuteAsync("""
                    CREATE INDEX IF NOT EXISTS idx_activities_start_date ON activities(start_date_time DESC);
                    CREATE INDEX IF NOT EXISTS idx_activities_activity_type ON activities(activity_type);
                    """);

                // Seed default activity types if none exist
                var activityTypeCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM activity_types");
                if (activityTypeCount == 0)
                {
                    _logger.LogInformation("Seeding default activity types...");
                    await SeedDefaultActivityTypesAsync(connection);
                }

                _logger.LogInformation("Database schema initialized successfully");
                return; // Success - exit the retry loop
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "Failed to connect to database on attempt {Attempt}/{MaxRetries}. Retrying in {Delay} seconds...", 
                    attempt, MaxRetries, RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize database schema after {MaxRetries} attempts", MaxRetries);
                throw;
            }
        }
    }

    private async Task SeedDefaultActivityTypesAsync(System.Data.IDbConnection connection)
    {
        var defaultTypes = new[]
        {
            new { Name = "Run", Icon = "directions_run", Color = "#FF5722", IsDefault = true },
            new { Name = "Nordic Ski", Icon = "downhill_skiing", Color = "#2196F3", IsDefault = true },
            new { Name = "Kayak", Icon = "kayaking", Color = "#4CAF50", IsDefault = true },
            new { Name = "Walk", Icon = "directions_walk", Color = "#FF9800", IsDefault = true },
            new { Name = "Cycle", Icon = "directions_bike", Color = "#9C27B0", IsDefault = true },
            new { Name = "Hike", Icon = "hiking", Color = "#795548", IsDefault = true },
            new { Name = "Swim", Icon = "pool", Color = "#00BCD4", IsDefault = true }
        };

        foreach (var activityType in defaultTypes)
        {
            await connection.ExecuteAsync("""
                INSERT INTO activity_types (name, icon, color, is_default)
                VALUES (@Name, @Icon, @Color, @IsDefault)
                ON CONFLICT (name) DO NOTHING
                """, activityType);
        }
    }
}
