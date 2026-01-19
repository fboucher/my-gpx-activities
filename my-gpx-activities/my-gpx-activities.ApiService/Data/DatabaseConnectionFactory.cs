using Npgsql;

namespace my_gpx_activities.ApiService.Data;

public interface IDatabaseConnectionFactory
{
    Task<NpgsqlConnection> CreateConnectionAsync();
}

public class DatabaseConnectionFactory : IDatabaseConnectionFactory
{
    private readonly NpgsqlDataSource _dataSource;

    public DatabaseConnectionFactory(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<NpgsqlConnection> CreateConnectionAsync()
    {
        var connection = await _dataSource.OpenConnectionAsync();
        return connection;
    }
}
