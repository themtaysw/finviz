using Taxonomy.Core;

namespace Taxonomy.Ingest;

/// <summary>
/// A <see cref="TaxonomyEntry"/> with the columns derived from its position in pre-order.
/// <see cref="Id"/> is that position (1-based), so a node's descendants are exactly the ids
/// <c>Id + 1 .. Id + Size</c>.
/// </summary>
public readonly record struct TaxonomyRecord(int Id, int? ParentId, int Depth, string Label, TaxonomyEntry Entry)
{
    /// <summary>
    /// Numbers the entries and resolves each one's parent by subtree span rather than by path,
    /// which keeps same-named siblings apart. O(n) time, O(depth) extra space.
    /// </summary>
    /// <exception cref="FormatException">The entries are not a consistent pre-order listing.</exception>
    public static IEnumerable<TaxonomyRecord> FromPreorder(IReadOnlyList<TaxonomyEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var ancestors = new Stack<(int Id, int LastDescendantId, string Name)>();

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var id = i + 1;
            var lastDescendantId = id + entry.Size;

            while (ancestors.TryPeek(out var top) && top.LastDescendantId < id)
            {
                ancestors.Pop();
            }

            if (entry.Size < 0 || lastDescendantId > entries.Count)
            {
                throw new FormatException($"Entry {id} ('{entry.Name}') has size {entry.Size} which overruns the list.");
            }

            var hasParent = ancestors.TryPeek(out var parent);
            var parentName = TaxonomyPath.GetParent(entry.Name);

            if (hasParent ? parentName != parent.Name : parentName is not null)
            {
                throw new FormatException($"Entry {id} ('{entry.Name}') is not a child of the entry that contains it.");
            }

            if (hasParent && lastDescendantId > parent.LastDescendantId)
            {
                throw new FormatException($"Entry {id} ('{entry.Name}') extends past the end of its parent.");
            }

            yield return new TaxonomyRecord(
                id,
                hasParent ? parent.Id : null,
                ancestors.Count,
                TaxonomyPath.GetLabel(entry.Name),
                entry);

            ancestors.Push((id, lastDescendantId, entry.Name));
        }
    }
}
