using Microsoft.Extensions.Configuration;
using Npgsql;

namespace OutfitPlanner.Infrastructure.Diagnostics;

public sealed class PostgresConnectionProbe
{
    private readonly IConfiguration _configuration;

    public PostgresConnectionProbe(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<string> CheckAsync(CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "not configured";
        }

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand("select 1");
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Equals(result, 1) ? "connected" : "unexpected response";
    }
}
