using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Taxonomy.Api.Data;
using Taxonomy.Core;

namespace Taxonomy.Api.Endpoints;

internal static class TaxonomyEndpoints
{
    public static IEndpointRouteBuilder MapTaxonomy(this IEndpointRouteBuilder routes)
    {
        var nodes = routes.MapGroup("/api/nodes").WithTags("Nodes");

        nodes.MapGet("/roots", GetRoots);
        nodes.MapGet("/{id:int}", GetNode);
        nodes.MapGet("/{id:int}/children", GetChildren);
        nodes.MapGet("/{id:int}/tree", GetSubtree);

        routes.MapGet("/api/search", Search).WithTags("Search");

        return routes;
    }

    private static async Task<Ok<Page<NodeSummary>>> GetRoots(
        TaxonomyRepository repository,
        CancellationToken cancellationToken,
        [FromQuery][Range(0, int.MaxValue)] int offset = 0,
        [FromQuery][Range(1, 500)] int limit = 100) =>
        TypedResults.Ok(await repository.GetRootsAsync(offset, limit, cancellationToken));

    private static async Task<Results<Ok<NodeDetails>, NotFound>> GetNode(
        int id,
        TaxonomyRepository repository,
        CancellationToken cancellationToken) =>
        await repository.GetNodeAsync(id, cancellationToken) is { } node
            ? TypedResults.Ok(node)
            : TypedResults.NotFound();

    private static async Task<Results<Ok<Page<NodeSummary>>, NotFound, ValidationProblem>> GetChildren(
        int id,
        TaxonomyRepository repository,
        CancellationToken cancellationToken,
        [FromQuery][Range(0, int.MaxValue)] int offset = 0,
        [FromQuery][Range(1, 500)] int limit = 100,
        [FromQuery] string order = "source")
    {
        // Minimal APIs bind enums case-sensitively and fail with a 500 on anything else, so parse it here.
        if (!Enum.TryParse<ChildOrder>(order, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["order"] = [$"Must be one of: {string.Join(", ", Enum.GetNames<ChildOrder>()).ToLowerInvariant()}."],
            });
        }

        return await repository.GetChildrenAsync(id, offset, limit, parsed, cancellationToken) is { } page
            ? TypedResults.Ok(page)
            : TypedResults.NotFound();
    }

    private static async Task<Results<Ok<TaxonomyNode>, NotFound, ProblemHttpResult>> GetSubtree(
        int id,
        TaxonomyRepository repository,
        CancellationToken cancellationToken,
        [FromQuery][Range(1, 3)] int depth = 1)
    {
        try
        {
            return await repository.GetSubtreeAsync(id, depth, cancellationToken) is { } tree
                ? TypedResults.Ok(tree)
                : TypedResults.NotFound();
        }
        catch (SubtreeTooLargeException exception)
        {
            return TypedResults.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<Ok<IReadOnlyList<SearchMatch>>> Search(
        TaxonomyRepository repository,
        CancellationToken cancellationToken,
        [FromQuery][Required][StringLength(100, MinimumLength = 1)] string q = "",
        [FromQuery][Range(1, 100)] int limit = 30) =>
        TypedResults.Ok(await repository.SearchAsync(q.Trim(), limit, cancellationToken));
}
