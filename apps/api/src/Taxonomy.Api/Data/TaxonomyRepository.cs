using Dapper;
using Npgsql;
using Taxonomy.Core;

namespace Taxonomy.Api.Data;

internal sealed class TaxonomyRepository(NpgsqlDataSource dataSource)
{
    private const int MaxSubtreeRows = 10_000;

    public async Task<Page<NodeSummary>> GetRootsAsync(int offset, int limit, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT count(*) FROM taxonomy_entry WHERE depth = 0;

            SELECT id AS "Id", label AS "Label", size AS "Size", child_count AS "ChildCount"
            FROM taxonomy_entry
            WHERE depth = 0
            ORDER BY id
            OFFSET @offset LIMIT @limit;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var results = await connection.QueryMultipleAsync(
            Command(sql, new { offset, limit }, cancellationToken));

        var total = await results.ReadSingleAsync<int>();
        var items = (await results.ReadAsync<NodeSummary>()).AsList();

        return new Page<NodeSummary>(items, total, offset, limit);
    }

    public async Task<NodeDetails?> GetNodeAsync(int id, CancellationToken cancellationToken)
    {
        // Siblings are the rows at the same depth between the parent and the node.
        const string sql = """
            WITH RECURSIVE chain AS (
                SELECT id, parent_id, label, name, size, child_count, depth
                FROM taxonomy_entry
                WHERE id = @id
                UNION ALL
                SELECT p.id, p.parent_id, p.label, p.name, p.size, p.child_count, p.depth
                FROM chain c
                JOIN taxonomy_entry p ON p.id = c.parent_id
            )
            SELECT c.id AS "Id", c.label AS "Label", c.name AS "Path", c.size AS "Size",
                   c.child_count AS "ChildCount", c.depth::int AS "Depth",
                   (SELECT count(*)::int
                    FROM taxonomy_entry s
                    WHERE s.depth = c.depth AND s.id > coalesce(c.parent_id, 0) AND s.id < c.id) AS "Index"
            FROM chain c
            ORDER BY c.depth
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var chain = (await connection.QueryAsync<NodeRow>(Command(sql, new { id }, cancellationToken))).AsList();

        if (chain.Count == 0)
        {
            return null;
        }

        var node = chain[^1];
        var ancestors = chain
            .Take(chain.Count - 1)
            .Select(row => new NodeAncestor(row.Id, row.Label, row.Index, row.ChildCount))
            .ToArray();

        return new NodeDetails(
            node.Id, node.Label, node.Path, node.Size, node.ChildCount, node.Depth, node.Index, ancestors);
    }

    public async Task<Page<NodeSummary>?> GetChildrenAsync(
        int id,
        int offset,
        int limit,
        ChildOrder order,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT child_count FROM taxonomy_entry WHERE id = @id;

            SELECT c.id AS "Id", c.label AS "Label", c.size AS "Size", c.child_count AS "ChildCount"
            FROM taxonomy_entry p
            JOIN taxonomy_entry c ON c.depth = p.depth + 1 AND c.id > p.id AND c.id <= p.id + p.size
            WHERE p.id = @id
            ORDER BY {OrderBy(order)}
            OFFSET @offset LIMIT @limit;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var results = await connection.QueryMultipleAsync(
            Command(sql, new { id, offset, limit }, cancellationToken));

        var total = await results.ReadSingleOrDefaultAsync<int?>();
        if (total is null)
        {
            return null;
        }

        var items = (await results.ReadAsync<NodeSummary>()).AsList();

        return new Page<NodeSummary>(items, total.Value, offset, limit);
    }

    public async Task<IReadOnlyList<SearchMatch>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS "Id", label AS "Label", name AS "Path", size AS "Size",
                   child_count AS "ChildCount", depth::int AS "Depth"
            FROM taxonomy_entry
            WHERE label ILIKE @contains
            ORDER BY lower(@query) = ANY (string_to_array(lower(label), ', ')) DESC,
                     (label ILIKE @startsWith OR label ILIKE @synonymStartsWith) DESC,
                     size DESC,
                     length(label),
                     id
            LIMIT @limit
            """;

        var escaped = EscapeLikeWildcards(query);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var matches = await connection.QueryAsync<SearchMatch>(Command(
            sql,
            new
            {
                query,
                contains = $"%{escaped}%",
                startsWith = $"{escaped}%",
                synonymStartsWith = $"%, {escaped}%",
                limit,
            },
            cancellationToken));

        return matches.AsList();
    }

    public async Task<TaxonomyNode?> GetSubtreeAsync(int id, int depth, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.name, c.size
            FROM taxonomy_entry p
            JOIN taxonomy_entry c ON c.id >= p.id AND c.id <= p.id + p.size AND c.depth <= p.depth + @depth
            WHERE p.id = @id
            ORDER BY c.id
            LIMIT @limit
            """;

        var builder = new TaxonomyTreeBuilder();
        var rows = 0;

        // Dapper's unbuffered query doesn't take a cancellation token.
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("depth", depth);
        command.Parameters.AddWithValue("limit", MaxSubtreeRows + 1);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (++rows > MaxSubtreeRows)
            {
                throw new SubtreeTooLargeException(MaxSubtreeRows);
            }

            builder.Add(new TaxonomyEntry(reader.GetString(0), reader.GetInt32(1)));
        }

        return builder.Root;
    }

    private static string OrderBy(ChildOrder order) => order switch
    {
        ChildOrder.Name => "c.label, c.id",
        ChildOrder.Size => "c.size DESC, c.id",
        _ => "c.id",
    };

    private static string EscapeLikeWildcards(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static CommandDefinition Command(string sql, object parameters, CancellationToken cancellationToken) =>
        new(sql, parameters, cancellationToken: cancellationToken);

    private sealed record NodeRow(int Id, string Label, string Path, int Size, int ChildCount, int Depth, int Index);
}

internal sealed class SubtreeTooLargeException(int limit)
    : Exception($"The requested subtree has more than {limit} nodes. Request a smaller depth.")
{
    public int Limit { get; } = limit;
}
