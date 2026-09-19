using Npgsql;
using NpgsqlTypes;
using Taxonomy.Core;

namespace Taxonomy.Ingest.Database;

public sealed class TaxonomyLoader(NpgsqlDataSource dataSource)
{
    public async Task ReplaceAllAsync(IReadOnlyList<TaxonomyEntry> entries, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var truncate = new NpgsqlCommand("TRUNCATE taxonomy_entry", connection))
        {
            await truncate.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var importer = await connection.BeginBinaryImportAsync(
            "COPY taxonomy_entry (id, parent_id, depth, label, name, size, child_count) FROM STDIN (FORMAT BINARY)",
            cancellationToken))
        {
            var records = TaxonomyRecord.FromPreorder(entries).ToArray();
            var childCounts = CountChildren(records);

            foreach (var record in records)
            {
                await importer.StartRowAsync(cancellationToken);
                await importer.WriteAsync(record.Id, NpgsqlDbType.Integer, cancellationToken);
                await importer.WriteAsync(record.ParentId, NpgsqlDbType.Integer, cancellationToken);
                await importer.WriteAsync((short)record.Depth, NpgsqlDbType.Smallint, cancellationToken);
                await importer.WriteAsync(record.Label, NpgsqlDbType.Text, cancellationToken);
                await importer.WriteAsync(record.Entry.Name, NpgsqlDbType.Text, cancellationToken);
                await importer.WriteAsync(record.Entry.Size, NpgsqlDbType.Integer, cancellationToken);
                await importer.WriteAsync(childCounts[record.Id], NpgsqlDbType.Integer, cancellationToken);
            }

            await importer.CompleteAsync(cancellationToken);
        }

        await using (var analyze = new NpgsqlCommand("ANALYZE taxonomy_entry", connection))
        {
            await analyze.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static int[] CountChildren(TaxonomyRecord[] records)
    {
        var counts = new int[records.Length + 1];
        foreach (var record in records)
        {
            if (record.ParentId is { } parentId)
            {
                counts[parentId]++;
            }
        }

        return counts;
    }
}
