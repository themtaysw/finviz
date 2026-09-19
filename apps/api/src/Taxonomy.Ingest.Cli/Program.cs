using System.Diagnostics;
using System.Text.Json;
using Npgsql;
using Taxonomy.Core;
using Taxonomy.Ingest;
using Taxonomy.Ingest.Database;

const string Usage = """
    Usage:
      taxonomy-ingest export <structure.xml> [output.json]   Write the linear form as JSON
      taxonomy-ingest migrate                                Apply database migrations
      taxonomy-ingest load <structure.xml>                   Migrate, then replace the stored taxonomy

    The database is read from the ConnectionStrings__Taxonomy environment variable.
    """;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

var cancellationToken = cancellation.Token;

try
{
    switch (args)
    {
        case ["export", var source]:
            await ExportAsync(source, Console.OpenStandardOutput(), cancellationToken);
            return 0;

        case ["export", var source, var destination]:
            await using (var output = File.Create(destination))
            {
                await ExportAsync(source, output, cancellationToken);
            }
            return 0;

        case ["migrate"]:
            await using (var dataSource = CreateDataSource())
            {
                await MigrateAsync(dataSource, cancellationToken);
            }
            return 0;

        case ["load", var source]:
            await using (var dataSource = CreateDataSource())
            {
                await MigrateAsync(dataSource, cancellationToken);
                await LoadAsync(dataSource, source, cancellationToken);
            }
            return 0;

        default:
            await Console.Error.WriteLineAsync(Usage);
            return 2;
    }
}
catch (OperationCanceledException)
{
    return 130;
}

static async Task ExportAsync(string source, Stream output, CancellationToken cancellationToken)
{
    var entries = await ParseAsync(source, cancellationToken);
    await JsonSerializer.SerializeAsync(output, entries, JsonSerializerOptions.Web, cancellationToken);
}

static async Task MigrateAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
{
    var applied = await new SchemaMigrator(dataSource).MigrateAsync(cancellationToken);
    await Console.Error.WriteLineAsync(
        applied.Count == 0 ? "Schema is up to date." : $"Applied migrations: {string.Join(", ", applied)}");
}

static async Task LoadAsync(NpgsqlDataSource dataSource, string source, CancellationToken cancellationToken)
{
    var entries = await ParseAsync(source, cancellationToken);

    var stopwatch = Stopwatch.StartNew();
    await new TaxonomyLoader(dataSource).ReplaceAllAsync(entries, cancellationToken);
    await Console.Error.WriteLineAsync($"Loaded {entries.Count:N0} entries in {stopwatch.ElapsedMilliseconds} ms.");
}

static async Task<IReadOnlyList<TaxonomyEntry>> ParseAsync(string source, CancellationToken cancellationToken)
{
    var stopwatch = Stopwatch.StartNew();
    await using var input = File.OpenRead(source);
    var entries = ImageNetXmlParser.Parse(input, cancellationToken);
    await Console.Error.WriteLineAsync($"Parsed {entries.Count:N0} entries in {stopwatch.ElapsedMilliseconds} ms.");
    return entries;
}

static NpgsqlDataSource CreateDataSource()
{
    var builder = new NpgsqlDataSourceBuilder(
        Environment.GetEnvironmentVariable("ConnectionStrings__Taxonomy")
        ?? "Host=localhost;Port=5433;Database=taxonomy;Username=taxonomy;Password=taxonomy");
    builder.ConnectionStringBuilder.GssEncryptionMode = GssEncryptionMode.Disable;
    return builder.Build();
}
