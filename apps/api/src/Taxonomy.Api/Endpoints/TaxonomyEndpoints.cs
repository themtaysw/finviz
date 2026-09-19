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

        nodes.MapGet("/roots", GetRoots).WithName("getRoots").ProducesValidationProblem();
        nodes.MapGet("/{id:int}", GetNode).WithName("getNode");
        nodes.MapGet("/{id:int}/children", GetChildren).WithName("getChildren").ProducesValidationProblem();
        nodes.MapGet("/{id:int}/tree", GetSubtree).WithName("getSubtree").ProducesValidationProblem();

        routes.MapGet("/api/search", Search).WithName("search").WithTags("Search").ProducesValidationProblem();

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

    private static async Task<Results<Ok<Page<NodeSummary>>, NotFound>> GetChildren(
        int id,
        TaxonomyRepository repository,
        CancellationToken cancellationToken,
        [FromQuery][Range(0, int.MaxValue)] int offset = 0,
        [FromQuery][Range(1, 500)] int limit = 100,
        [FromQuery][AllowedValues("source", "name", "size")] string order = "source") =>
        await repository.GetChildrenAsync(id, offset, limit, Enum.Parse<ChildOrder>(order, true), cancellationToken)
            is { } page
            ? TypedResults.Ok(page)
            : TypedResults.NotFound();

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
        [FromQuery][StringLength(100, MinimumLength = 1)] string q,
        TaxonomyRepository repository,
        CancellationToken cancellationToken,
        [FromQuery][Range(1, 100)] int limit = 30) =>
        TypedResults.Ok(await repository.SearchAsync(q.Trim(), limit, cancellationToken));
}
