using Taxonomy.Ingest.Database;

namespace Taxonomy.IntegrationTests.Database;

public sealed class SchemaMigratorTests(PostgresFixture postgres)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MigrateAsync_AppliesEachScriptOnlyOnce()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync();
        var migrator = new SchemaMigrator(dataSource);

        var first = await migrator.MigrateAsync(CancellationToken);
        var second = await migrator.MigrateAsync(CancellationToken);

        Assert.Contains("0001_create_taxonomy_entry", first);
        Assert.Empty(second);
    }

    [Fact]
    public async Task MigrateAsync_ConcurrentRunsDoNotApplyScriptsTwice()
    {
        await using var dataSource = await postgres.CreateDatabaseAsync();

        var runs = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => new SchemaMigrator(dataSource).MigrateAsync(CancellationToken)));

        var applied = runs.SelectMany(versions => versions).ToList();
        Assert.Equal(applied.Distinct(StringComparer.Ordinal), applied);
        Assert.Contains("0001_create_taxonomy_entry", applied);
    }
}
