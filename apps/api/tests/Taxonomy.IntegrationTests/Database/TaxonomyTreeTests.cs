using Npgsql;
using Taxonomy.Core;
using Taxonomy.Ingest;
using Taxonomy.Ingest.Database;

namespace Taxonomy.IntegrationTests.Database;

public sealed class TaxonomyTreeTests(PostgresFixture postgres)
{
    [Fact]
    public async Task TreeBuiltFromStoredRows_MatchesTreeBuiltFromSourceFile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dataSource = NpgsqlDataSource.Create(await postgres.CreateDatabaseAsync());
        await new SchemaMigrator(dataSource).MigrateAsync(cancellationToken);

        await using var xml = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestData", "structure_released.xml"));
        var entries = ImageNetXmlParser.Parse(xml, cancellationToken);
        await new TaxonomyLoader(dataSource).ReplaceAllAsync(entries, cancellationToken);

        var builder = new TaxonomyTreeBuilder();
        await using (var command = dataSource.CreateCommand("SELECT name, size FROM taxonomy_entry ORDER BY id"))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                builder.Add(new TaxonomyEntry(reader.GetString(0), reader.GetInt32(1)));
            }
        }

        Assert.Equivalent(TaxonomyTreeBuilder.Build(entries), builder.Root, strict: true);
    }
}
