using Npgsql;
using Taxonomy.Core;
using Taxonomy.Ingest;
using Taxonomy.Ingest.Database;

namespace Taxonomy.IntegrationTests.Database;

public sealed class TaxonomyLoaderTests(PostgresFixture postgres)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReplaceAllAsync_StoresReleasedStructure()
    {
        await using var dataSource = await CreateMigratedDatabaseAsync();
        await using var xml = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestData", "structure_released.xml"));
        var entries = ImageNetXmlParser.Parse(xml, CancellationToken);

        await new TaxonomyLoader(dataSource).ReplaceAllAsync(entries, CancellationToken);

        Assert.Equal(TaxonomyRecord.FromPreorder(entries), await ReadAllAsync(dataSource));
    }

    [Fact]
    public async Task ReplaceAllAsync_ReplacesPreviouslyLoadedData()
    {
        await using var dataSource = await CreateMigratedDatabaseAsync();
        var loader = new TaxonomyLoader(dataSource);
        TaxonomyEntry[] replacement = [new("fungus", 1), new("fungus > mushroom", 0)];

        await loader.ReplaceAllAsync([new("plant", 1), new("plant > moss", 0)], CancellationToken);
        await loader.ReplaceAllAsync(replacement, CancellationToken);

        Assert.Equal(TaxonomyRecord.FromPreorder(replacement), await ReadAllAsync(dataSource));
    }

    [Fact]
    public async Task ReplaceAllAsync_KeepsExistingDataWhenInputIsInvalid()
    {
        await using var dataSource = await CreateMigratedDatabaseAsync();
        var loader = new TaxonomyLoader(dataSource);
        TaxonomyEntry[] original = [new("plant", 1), new("plant > moss", 0)];

        await loader.ReplaceAllAsync(original, CancellationToken);
        await Assert.ThrowsAsync<FormatException>(
            () => loader.ReplaceAllAsync([new("fungus", 5), new("fungus > mushroom", 0)], CancellationToken));

        Assert.Equal(TaxonomyRecord.FromPreorder(original), await ReadAllAsync(dataSource));
    }

    private async Task<NpgsqlDataSource> CreateMigratedDatabaseAsync()
    {
        var dataSource = NpgsqlDataSource.Create(await postgres.CreateDatabaseAsync());
        await new SchemaMigrator(dataSource).MigrateAsync(CancellationToken);
        return dataSource;
    }

    private static async Task<List<TaxonomyRecord>> ReadAllAsync(NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT id, parent_id, depth, label, name, size FROM taxonomy_entry ORDER BY id");
        await using var reader = await command.ExecuteReaderAsync(CancellationToken);

        var records = new List<TaxonomyRecord>();
        while (await reader.ReadAsync(CancellationToken))
        {
            records.Add(new TaxonomyRecord(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.GetInt16(2),
                reader.GetString(3),
                new TaxonomyEntry(reader.GetString(4), reader.GetInt32(5))));
        }

        return records;
    }
}
