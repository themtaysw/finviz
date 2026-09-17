namespace Taxonomy.Core;

/// <summary>A category in linear form: its full path and the number of its descendants.</summary>
public readonly record struct TaxonomyEntry(string Name, int Size);
