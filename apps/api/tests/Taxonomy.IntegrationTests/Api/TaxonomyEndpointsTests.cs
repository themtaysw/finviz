using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Taxonomy.Api;
using Taxonomy.Core;

namespace Taxonomy.IntegrationTests.Api;

public sealed class TaxonomyEndpointsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly TaxonomyEntry[] Entries =
    [
        new("life", 6),
        new("life > plant", 3),
        new("life > plant > coneflower", 0),
        new("life > plant > coneflower", 1),
        new("life > plant > coneflower > rudbeckia", 0),
        new("life > animal", 1),
        new("life > animal > dog", 0),
    ];

    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    private TaxonomyApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TaxonomyApiFactory.CreateAsync(postgres, Entries, "taxonomy-api-endpoint-tests");
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetRoots_ReturnsTopLevelNodes()
    {
        var page = await GetAsync<Page<NodeSummary>>("/api/nodes/roots");

        var root = Assert.Single(page.Items);
        Assert.Equal(new NodeSummary(1, "life", 6, 2), root);
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task GetNode_ReturnsPathAndAncestors()
    {
        var node = await GetAsync<NodeDetails>("/api/nodes/5");

        Assert.Equal("rudbeckia", node.Label);
        Assert.Equal("life > plant > coneflower > rudbeckia", node.Path);
        Assert.Equal(3, node.Depth);
        Assert.Equal(0, node.Index);
        Assert.Equal(
            [new NodeAncestor(1, "life", 0, 2), new NodeAncestor(2, "plant", 0, 2), new NodeAncestor(4, "coneflower", 1, 1)],
            node.Ancestors);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(4, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 0)]
    public async Task GetNode_ReportsPositionAmongSiblings(int id, int index)
    {
        var node = await GetAsync<NodeDetails>($"/api/nodes/{id}");

        Assert.Equal(index, node.Index);
    }

    [Fact]
    public async Task GetChildren_KeepsSameNamedSiblingsApart()
    {
        var page = await GetAsync<Page<NodeSummary>>("/api/nodes/2/children");

        Assert.Equal(
            [new NodeSummary(3, "coneflower", 0, 0), new NodeSummary(4, "coneflower", 1, 1)],
            page.Items);
        Assert.Equal(2, page.Total);
    }

    [Fact]
    public async Task GetChildren_PagesAndReportsTheFullTotal()
    {
        var page = await GetAsync<Page<NodeSummary>>("/api/nodes/1/children?offset=1&limit=1");

        Assert.Equal("animal", Assert.Single(page.Items).Label);
        Assert.Equal(2, page.Total);
        Assert.Equal(1, page.Offset);
    }

    [Theory]
    [InlineData("size", new[] { "plant", "animal" })]
    [InlineData("name", new[] { "animal", "plant" })]
    [InlineData("SOURCE", new[] { "plant", "animal" })]
    public async Task GetChildren_Orders(string order, string[] expected)
    {
        var page = await GetAsync<Page<NodeSummary>>($"/api/nodes/1/children?order={order}");

        Assert.Equal(expected, page.Items.Select(item => item.Label));
    }

    [Fact]
    public async Task Search_RanksExactLabelMatchesFirst()
    {
        var matches = await GetAsync<SearchMatch[]>("/api/search?q=cone");

        Assert.Equal(2, matches.Length);
        Assert.All(matches, match => Assert.Equal("coneflower", match.Label));
        Assert.Contains(matches, match => match.ChildCount == 1);
    }

    [Fact]
    public async Task Search_IsCaseInsensitiveAndReturnsPaths()
    {
        var matches = await GetAsync<SearchMatch[]>("/api/search?q=DOG");

        var match = Assert.Single(matches);
        Assert.Equal("life > animal > dog", match.Path);
    }

    [Fact]
    public async Task Search_TreatsWildcardsAsLiterals()
    {
        Assert.Empty(await GetAsync<SearchMatch[]>("/api/search?q=%25"));
        Assert.Empty(await GetAsync<SearchMatch[]>("/api/search?q=_og"));
    }

    [Fact]
    public async Task GetSubtree_ReturnsNestedNodesForTheRequestedDepth()
    {
        var tree = await GetAsync<TaxonomyNode>("/api/nodes/2/tree?depth=1");

        Assert.Equal("plant", tree.Name);
        Assert.Equal(["coneflower", "coneflower"], tree.Children.Select(child => child.Name));
        Assert.All(tree.Children, child => Assert.Empty(child.Children));
    }

    [Theory]
    [InlineData("/api/nodes/999", HttpStatusCode.NotFound)]
    [InlineData("/api/nodes/999/children", HttpStatusCode.NotFound)]
    [InlineData("/api/nodes/999/tree", HttpStatusCode.NotFound)]
    [InlineData("/api/nodes/1/children?limit=0", HttpStatusCode.BadRequest)]
    [InlineData("/api/nodes/1/children?offset=-1", HttpStatusCode.BadRequest)]
    [InlineData("/api/nodes/1/children?order=bogus", HttpStatusCode.BadRequest)]
    [InlineData("/api/nodes/1/tree?depth=9", HttpStatusCode.BadRequest)]
    [InlineData("/api/search", HttpStatusCode.BadRequest)]
    [InlineData("/api/search?q=", HttpStatusCode.BadRequest)]
    public async Task Endpoints_RejectUnknownIdsAndInvalidArguments(string url, HttpStatusCode expected)
    {
        using var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    private async Task<T> GetAsync<T>(string url)
    {
        using var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(Json, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException($"{url} returned null.");
    }
}
