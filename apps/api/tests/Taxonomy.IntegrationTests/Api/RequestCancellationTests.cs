using Npgsql;
using Taxonomy.Core;

namespace Taxonomy.IntegrationTests.Api;

public sealed class RequestCancellationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string ApplicationName = "taxonomy-api-cancellation-tests";

    private static readonly TaxonomyEntry[] Entries = [new("life", 1), new("life > plant", 0)];

    private TaxonomyApiFactory _factory = null!;
    private NpgsqlDataSource _dataSource = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TaxonomyApiFactory.CreateAsync(postgres, Entries, ApplicationName, useKestrel: true);
        _dataSource = NpgsqlDataSource.Create(_factory.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task CancelledRequest_StopsTheQueryOnTheServer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateRealClient();
        using var request = new CancellationTokenSource();

        await using var blocker = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var lockTransaction = await blocker.BeginTransactionAsync(cancellationToken);
        await using (var takeLock = new NpgsqlCommand(
            "LOCK TABLE taxonomy_entry IN ACCESS EXCLUSIVE MODE", blocker, lockTransaction))
        {
            await takeLock.ExecuteNonQueryAsync(cancellationToken);
        }

        var pending = client.GetAsync("/api/nodes/1/children", request.Token);
        await WaitForApiQueriesAsync(expected: 1, cancellationToken);

        await request.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await WaitForApiQueriesAsync(expected: 0, cancellationToken);

        await lockTransaction.RollbackAsync(cancellationToken);
    }

    [Fact]
    public async Task UncancelledRequest_CompletesOnceTheLockIsReleased()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateRealClient();

        await using var blocker = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var lockTransaction = await blocker.BeginTransactionAsync(cancellationToken);
        await using (var takeLock = new NpgsqlCommand(
            "LOCK TABLE taxonomy_entry IN ACCESS EXCLUSIVE MODE", blocker, lockTransaction))
        {
            await takeLock.ExecuteNonQueryAsync(cancellationToken);
        }

        var pending = client.GetAsync("/api/nodes/1/children", cancellationToken);
        await WaitForApiQueriesAsync(expected: 1, cancellationToken);
        await lockTransaction.RollbackAsync(cancellationToken);

        using var response = await pending;
        response.EnsureSuccessStatusCode();
    }

    private async Task WaitForApiQueriesAsync(int expected, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT count(*)
            FROM pg_stat_activity
            WHERE application_name = @applicationName
              AND state = 'active'
              AND query NOT ILIKE '%pg_stat_activity%'
            """;

        var deadline = DateTime.UtcNow.AddSeconds(10);
        long running;

        do
        {
            await using var command = _dataSource.CreateCommand(sql);
            command.Parameters.AddWithValue("applicationName", ApplicationName);
            running = (long)(await command.ExecuteScalarAsync(cancellationToken))!;

            if (running == expected)
            {
                return;
            }

            await Task.Delay(50, cancellationToken);
        }
        while (DateTime.UtcNow < deadline);

        Assert.Fail($"Expected {expected} running API queries, found {running}.");
    }
}
