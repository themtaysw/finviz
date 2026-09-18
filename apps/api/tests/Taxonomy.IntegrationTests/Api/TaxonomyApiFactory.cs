using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Taxonomy.Core;
using Taxonomy.Ingest.Database;

namespace Taxonomy.IntegrationTests.Api;

/// <summary>Runs the API against its own database, seeded with the given entries.</summary>
internal sealed class TaxonomyApiFactory(string connectionString, bool useKestrel) : WebApplicationFactory<Program>
{
    private IHost? _kestrelHost;

    public string ConnectionString { get; } = connectionString;

    /// <summary>The address of the real server, when the factory was asked for one.</summary>
    public Uri? ServerAddress { get; private set; }

    /// <remarks>
    /// <paramref name="useKestrel"/> serves over a real socket. The in-memory test server tears down in-flight work
    /// when a client disconnects, which hides whether the application itself propagates cancellation.
    /// </remarks>
    public static async Task<TaxonomyApiFactory> CreateAsync(
        PostgresFixture postgres,
        IReadOnlyList<TaxonomyEntry> entries,
        string applicationName,
        bool useKestrel = false)
    {
        var connectionString = await postgres.CreateDatabaseAsync(applicationName);
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await new SchemaMigrator(dataSource).MigrateAsync(cancellationToken);
        await new TaxonomyLoader(dataSource).ReplaceAllAsync(entries, cancellationToken);

        return new TaxonomyApiFactory(connectionString, useKestrel);
    }

    public HttpClient CreateRealClient()
    {
        _ = Services; // The factory builds its host lazily; this starts it, and with it the Kestrel one.

        return new HttpClient
        {
            BaseAddress = ServerAddress ?? throw new InvalidOperationException("The factory has no real server."),
        };
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Taxonomy", ConnectionString);

    protected override IHost CreateHost(IHostBuilder builder)
    {
        if (!useKestrel)
        {
            return base.CreateHost(builder);
        }

        // WebApplicationFactory needs its in-memory host, so build that first and only then add Kestrel to the
        // builder for a second host listening on a real port.
        var inMemoryHost = builder.Build();

        builder.ConfigureWebHost(host => host.UseKestrel().UseUrls("http://127.0.0.1:0"));
        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        var addresses = _kestrelHost.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        ServerAddress = new Uri(addresses!.Addresses.First());

        inMemoryHost.Start();
        return inMemoryHost;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _kestrelHost?.Dispose();
        }

        base.Dispose(disposing);
    }
}
