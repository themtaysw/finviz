namespace Taxonomy.Core;

public sealed record TaxonomyNode(string Name, int Size, IReadOnlyList<TaxonomyNode> Children);
