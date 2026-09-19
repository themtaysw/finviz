namespace Taxonomy.Core;

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
