namespace Taxonomy.Core;

/// <summary>
/// Rebuilds a tree from entries in pre-order, the order they are stored in. The first entry becomes the root,
/// so any contiguous slice of a subtree can be built.
/// </summary>
/// <remarks>
/// Parents are resolved against a stack of open ancestors instead of a lookup by path, because paths are not
/// unique: a child belongs to the closest preceding entry with its parent's path. O(n·L) time, O(depth) extra space.
/// </remarks>
public sealed class TaxonomyTreeBuilder
{
    private readonly Stack<(string Path, List<TaxonomyNode> Children)> _open = new();

    public TaxonomyNode? Root { get; private set; }

    public static TaxonomyNode? Build(IEnumerable<TaxonomyEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var builder = new TaxonomyTreeBuilder();
        foreach (var entry in entries)
        {
            builder.Add(entry);
        }

        return builder.Root;
    }

    /// <exception cref="FormatException">
    /// </exception>
    public void Add(TaxonomyEntry entry)
    {
        var (path, size) = entry;
        ArgumentNullException.ThrowIfNull(path, nameof(entry));

        var separatorIndex = path.LastIndexOf(TaxonomyPath.Separator, StringComparison.Ordinal);
        var parentPath = separatorIndex < 0 ? [] : path.AsSpan(0, separatorIndex);
        var label = separatorIndex < 0 ? path : path[(separatorIndex + TaxonomyPath.Separator.Length)..];

        var children = new List<TaxonomyNode>();
        var node = new TaxonomyNode(label, size, children);

        if (Root is null)
        {
            Root = node;
        }
        else
        {
            while (_open.TryPeek(out var top) && !parentPath.SequenceEqual(top.Path))
            {
                _open.Pop();
            }

            if (!_open.TryPeek(out var parent))
            {
                throw new FormatException(
                    $"'{path}' does not follow its parent in pre-order or is outside the subtree being built.");
            }

            parent.Children.Add(node);
        }

        _open.Push((path, children));
    }
}
