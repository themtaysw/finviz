using Taxonomy.Core;

namespace Taxonomy.Ingest;

public readonly record struct TaxonomyRecord(int Id, int? ParentId, int Depth, string Label, TaxonomyEntry Entry)
{
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
