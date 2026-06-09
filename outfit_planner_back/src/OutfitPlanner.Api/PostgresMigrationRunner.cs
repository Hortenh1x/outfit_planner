using DbUp;

namespace OutfitPlanner.Api;

public sealed class PostgresMigrationRunner
{
    private readonly string _connectionString;
    private readonly string _migrationsPath;
    private readonly ILogger<PostgresMigrationRunner> _logger;

    public PostgresMigrationRunner(string connectionString, string migrationsPath, ILogger<PostgresMigrationRunner> logger)
    {
        _connectionString = connectionString;
        _migrationsPath = migrationsPath;
        _logger = logger;
    }

    public void Initialize()
    {
        if (!Directory.Exists(_migrationsPath))
        {
            throw new DirectoryNotFoundException($"Database migrations directory was not found: {_migrationsPath}");
        }

        var result = DeployChanges.To
            .PostgresqlDatabase(_connectionString)
            .WithScriptsFromFileSystem(_migrationsPath)
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
        {
            _logger.LogError(result.Error, "Database migration failed.");
            throw result.Error;
        }

        _logger.LogInformation("Database migrations are up to date.");
    }
}
