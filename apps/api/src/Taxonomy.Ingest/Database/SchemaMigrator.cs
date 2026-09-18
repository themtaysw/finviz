using Npgsql;

namespace Taxonomy.Ingest.Database;

/// <summary>
/// Applies the embedded <c>Migrations/*.sql</c> scripts in name order, each at most once.
/// </summary>
public sealed class SchemaMigrator(NpgsqlDataSource dataSource)
{
    // Arbitrary, but fixed: serialises concurrent runs (e.g. overlapping deploys) across processes.
    private const long AdvisoryLockKey = 7_261_207_345_190_811;

    private const string ResourcePrefix = "Taxonomy.Ingest.Database.Migrations.";

    public async Task<IReadOnlyList<string>> MigrateAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteAsync(connection, "SELECT pg_advisory_xact_lock(@key)", cancellationToken, ("key", AdvisoryLockKey));
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS schema_migration
            (
                version    text        PRIMARY KEY,
                applied_at timestamptz NOT NULL DEFAULT now()
            )
            """,
            cancellationToken);

        var applied = await GetAppliedVersionsAsync(connection, cancellationToken);
        var pending = new List<string>();

        foreach (var (version, resource) in DiscoverScripts())
        {
            if (applied.Contains(version))
            {
                continue;
            }

            await ExecuteAsync(connection, await ReadResourceAsync(resource, cancellationToken), cancellationToken);
            await ExecuteAsync(
                connection,
                "INSERT INTO schema_migration (version) VALUES (@version)",
                cancellationToken,
                ("version", version));

            pending.Add(version);
        }

        await transaction.CommitAsync(cancellationToken);
        return pending;
    }

    private static IEnumerable<(string Version, string Resource)> DiscoverScripts() =>
        typeof(SchemaMigrator).Assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(name => (Version: Path.GetFileNameWithoutExtension(name[ResourcePrefix.Length..]), Resource: name))
            .OrderBy(script => script.Version, StringComparer.Ordinal);

    private static async Task<HashSet<string>> GetAppliedVersionsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT version FROM schema_migration", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var versions = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            versions.Add(reader.GetString(0));
        }

        return versions;
    }

    private static async Task<string> ReadResourceAsync(string resource, CancellationToken cancellationToken)
    {
        await using var stream = typeof(SchemaMigrator).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded migration '{resource}'.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
