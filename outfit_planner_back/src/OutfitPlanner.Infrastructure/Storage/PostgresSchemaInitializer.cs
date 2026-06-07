using Npgsql;

namespace OutfitPlanner.Infrastructure.Storage;

public sealed class PostgresSchemaInitializer
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _schemaPath;

    public PostgresSchemaInitializer(NpgsqlDataSource dataSource, string schemaPath)
    {
        _dataSource = dataSource;
        _schemaPath = schemaPath;
    }

    public void Initialize()
    {
        var schema = File.ReadAllText(_schemaPath);
        using var command = _dataSource.CreateCommand(schema);
        command.ExecuteNonQuery();
    }
}
