using System.Diagnostics.CodeAnalysis;

namespace Taxonomy.Core;

public static class TaxonomyPath
{
    public const string Separator = " > ";

    private const char Delimiter = '>';

    public static string Combine(string parent, string label) => string.Concat(parent, Separator, label);

    public static bool IsValidLabel([NotNullWhen(true)] string? label) =>
        !string.IsNullOrWhiteSpace(label) && !label.Contains(Delimiter);
}
