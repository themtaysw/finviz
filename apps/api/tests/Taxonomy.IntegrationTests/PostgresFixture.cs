using Npgsql;
using Taxonomy.IntegrationTests;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace Taxonomy.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    /// <summary>
    /// Creates an empty database on the shared server, so tests can run in parallel without seeing each other's data,
    /// and returns its connection string.
    /// </summary>
    public async Task<string> CreateDatabaseAsync(string? applicationName = null)
    {
        var database = $"test_{Guid.NewGuid():N}";

        await using (var server = NpgsqlDataSource.Create(_container.GetConnectionString()))
        await using (var command = server.CreateCommand($"CREATE DATABASE {database}"))
        {
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = database,
            ApplicationName = applicationName ?? "tests",
        };

        return connectionString.ConnectionString;
    }
}
