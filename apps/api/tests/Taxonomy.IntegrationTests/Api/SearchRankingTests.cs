using System.Net.Http.Json;
using System.Text.Json;
using Taxonomy.Api;
using Taxonomy.Core;

namespace Taxonomy.IntegrationTests.Api;

public sealed class SearchRankingTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly TaxonomyEntry[] Entries =
    [
        new("root", 6),
        new("root > hot dog", 0),
        new("root > dogwood", 0),
        new("root > dog", 0),
        new("root > cad, bounder, dog", 0),
        new("root > dog, domestic dog, Canis familiaris", 1),
        new("root > dog, domestic dog, Canis familiaris > puppy", 0),
    ];

    private TaxonomyApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TaxonomyApiFactory.CreateAsync(postgres, Entries, "taxonomy-api-search-tests");
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Search_RanksSynonymMatchesThenPrefixesThenContains()
    {
        var matches = await SearchAsync("dog");

        Assert.Equal(
            ["dog, domestic dog, Canis familiaris", "dog", "cad, bounder, dog", "dogwood", "hot dog"],
            matches.Select(match => match.Label));
    }

    [Fact]
    public async Task Search_TreatsASynonymPrefixAsAPrefix()
    {
        var matches = await SearchAsync("domestic");

        Assert.Equal("dog, domestic dog, Canis familiaris", Assert.Single(matches).Label);
    }

    private async Task<SearchMatch[]> SearchAsync(string query) =>
        await _client.GetFromJsonAsync<SearchMatch[]>(
            $"/api/search?q={Uri.EscapeDataString(query)}",
            JsonSerializerOptions.Web,
            TestContext.Current.CancellationToken) ?? [];
}
