using Npgsql;
using NpgsqlTypes;
using Taxonomy.Core;

namespace Taxonomy.Ingest.Database;

internal sealed class TaxonomyLoader(NpgsqlDataSource dataSource)
{
    /// <summary>
    /// Replaces the whole taxonomy in a single transaction, so readers see either the old or the new data set,
    /// never a partial one.
    /// </summary>
    public async Task ReplaceAllAsync(IReadOnlyList<TaxonomyEntry> entries, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var truncate = new NpgsqlCommand("TRUNCATE taxonomy_entry", connection))
        {
            await truncate.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var importer = await connection.BeginBinaryImportAsync(
            "COPY taxonomy_entry (id, parent_id, depth, label, name, size) FROM STDIN (FORMAT BINARY)",
            cancellationToken))
        {
            foreach (var record in TaxonomyRecord.FromPreorder(entries))
            {
                await importer.StartRowAsync(cancellationToken);
                await importer.WriteAsync(record.Id, NpgsqlDbType.Integer, cancellationToken);
                await importer.WriteAsync(record.ParentId, NpgsqlDbType.Integer, cancellationToken);
                await importer.WriteAsync((short)record.Depth, NpgsqlDbType.Smallint, cancellationToken);
                await importer.WriteAsync(record.Label, NpgsqlDbType.Text, cancellationToken);
                await importer.WriteAsync(record.Entry.Name, NpgsqlDbType.Text, cancellationToken);
                await importer.WriteAsync(record.Entry.Size, NpgsqlDbType.Integer, cancellationToken);
            }

            await importer.CompleteAsync(cancellationToken);
        }

        await using (var analyze = new NpgsqlCommand("ANALYZE taxonomy_entry", connection))
        {
            await analyze.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
