namespace Taxonomy.Api;

internal sealed record NodeSummary(int Id, string Label, int Size, int ChildCount);

internal sealed record NodeAncestor(int Id, string Label, int Index, int ChildCount);

internal sealed record NodeDetails(
    int Id,
    string Label,
    string Path,
    int Size,
    int ChildCount,
    int Depth,
    int Index,
    IReadOnlyList<NodeAncestor> Ancestors);

internal sealed record Page<T>(IReadOnlyList<T> Items, int Total, int Offset, int Limit);

internal sealed record SearchMatch(int Id, string Label, string Path, int Size, int ChildCount, int Depth);

internal enum ChildOrder
{
    Source,
    Name,
    Size,
}
