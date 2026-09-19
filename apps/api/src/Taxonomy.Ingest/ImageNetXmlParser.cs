using System.Xml;
using Taxonomy.Core;

namespace Taxonomy.Ingest;

public static class ImageNetXmlParser
{
    private const string SynsetElement = "synset";
    private const string LabelAttribute = "words";

    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true,
    };

    public static IReadOnlyList<TaxonomyEntry> Parse(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = XmlReader.Create(stream, ReaderSettings);
        var entries = new List<TaxonomyEntry>();
        var openSynsets = new Stack<int>();

        while (reader.Read())
        {
            if (reader.LocalName != SynsetElement)
            {
                continue;
            }

            if (reader.NodeType == XmlNodeType.Element)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var label = ReadLabel(reader);
                var name = openSynsets.TryPeek(out var parent)
                    ? TaxonomyPath.Combine(entries[parent].Name, label)
                    : label;

                entries.Add(new TaxonomyEntry(name, Size: 0));

                if (!reader.IsEmptyElement)
                {
                    openSynsets.Push(entries.Count - 1);
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                var index = openSynsets.Pop();
                entries[index] = entries[index] with { Size = entries.Count - index - 1 };
            }
        }

        return entries;
    }

    private static string ReadLabel(XmlReader reader)
    {
        var label = reader.GetAttribute(LabelAttribute);
        if (TaxonomyPath.IsValidLabel(label))
        {
            return label;
        }

        var line = reader is IXmlLineInfo info ? info.LineNumber : 0;
        throw new FormatException($"Synset on line {line} has an invalid '{LabelAttribute}' attribute: \"{label}\".");
    }
}
