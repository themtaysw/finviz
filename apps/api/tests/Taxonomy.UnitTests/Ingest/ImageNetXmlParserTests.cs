using System.Text;
using System.Xml;
using Taxonomy.Core;
using Taxonomy.Ingest;

namespace Taxonomy.UnitTests.Ingest;

public sealed class ImageNetXmlParserTests
{
    [Fact]
    public void Parse_FlattensSynsetsInDocumentOrderWithDescendantCounts()
    {
        const string xml = """
            <ImageNetStructure>
              <releaseData>fall2011</releaseData>
              <synset wnid="fall11" words="ImageNet 2011 Fall Release" gloss="ImageNet 2011 Fall Release.">
                <synset wnid="n00017222" words="plant, flora, plant life" gloss="(botany) a living organism">
                  <synset wnid="n01383896" words="phytoplankton" gloss='photosynthetic constituent
            of plankton'>
                    <synset wnid="n01384084" words="planktonic algae" gloss="unicellular algae"></synset>
                    <synset wnid="n01401106" words="diatom" gloss="microscopic alga"></synset>
                  </synset>
                  <synset wnid="n11530008" words="microflora" gloss="microscopic plants"></synset>
                </synset>
              </synset>
            </ImageNetStructure>
            """;

        TaxonomyEntry[] expected =
        [
            new("ImageNet 2011 Fall Release", 5),
            new("ImageNet 2011 Fall Release > plant, flora, plant life", 4),
            new("ImageNet 2011 Fall Release > plant, flora, plant life > phytoplankton", 2),
            new("ImageNet 2011 Fall Release > plant, flora, plant life > phytoplankton > planktonic algae", 0),
            new("ImageNet 2011 Fall Release > plant, flora, plant life > phytoplankton > diatom", 0),
            new("ImageNet 2011 Fall Release > plant, flora, plant life > microflora", 0),
        ];

        Assert.Equal(expected, Parse(xml));
    }

    [Fact]
    public void Parse_TreatsSelfClosingSynsetAsLeaf()
    {
        const string xml = """
            <ImageNetStructure>
              <synset words="root">
                <synset words="leaf" />
                <synset words="sibling"></synset>
              </synset>
            </ImageNetStructure>
            """;

        TaxonomyEntry[] expected = [new("root", 2), new("root > leaf", 0), new("root > sibling", 0)];

        Assert.Equal(expected, Parse(xml));
    }

    [Fact]
    public void Parse_KeepsSameNamedSiblingsAsSeparateEntries()
    {
        const string xml = """
            <ImageNetStructure>
              <synset words="flower">
                <synset words="coneflower"></synset>
                <synset words="coneflower">
                  <synset words="rudbeckia"></synset>
                </synset>
              </synset>
            </ImageNetStructure>
            """;

        TaxonomyEntry[] expected =
        [
            new("flower", 3),
            new("flower > coneflower", 0),
            new("flower > coneflower", 1),
            new("flower > coneflower > rudbeckia", 0),
        ];

        Assert.Equal(expected, Parse(xml));
    }

    [Theory]
    [InlineData("""wnid="n1" """)]
    [InlineData("""words="" """)]
    [InlineData("""words="   " """)]
    [InlineData("""words="a > b" """)]
    [InlineData("""words="a>b" """)]
    public void Parse_RejectsInvalidLabel(string attributes)
    {
        var xml = $"""
            <ImageNetStructure>
              <synset words="root">
                <synset {attributes}></synset>
              </synset>
            </ImageNetStructure>
            """;

        var exception = Assert.Throws<FormatException>(() => Parse(xml));
        Assert.Contains("line 3", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsDocumentTypeDefinitions()
    {
        const string xml = """
            <!DOCTYPE ImageNetStructure [<!ENTITY secret SYSTEM "file:///etc/passwd">]>
            <ImageNetStructure>
              <synset words="&secret;"></synset>
            </ImageNetStructure>
            """;

        Assert.Throws<XmlException>(() => Parse(xml));
    }

    [Fact]
    public void Parse_ThrowsWhenCancelled()
    {
        using var stream = ToStream("""<ImageNetStructure><synset words="root"></synset></ImageNetStructure>""");

        Assert.Throws<OperationCanceledException>(
            () => ImageNetXmlParser.Parse(stream, new CancellationToken(canceled: true)));
    }

    [Fact]
    public void Parse_ReleasedStructureFile()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestData", "structure_released.xml"));

        var entries = ImageNetXmlParser.Parse(stream, TestContext.Current.CancellationToken);

        Assert.Equal(60_942, entries.Count);
        Assert.Equal(new TaxonomyEntry("ImageNet 2011 Fall Release", 60_941), entries[0]);
        Assert.Equal(new TaxonomyEntry("ImageNet 2011 Fall Release > plant, flora, plant life", 4_699), entries[1]);
        Assert.Equal(
            new TaxonomyEntry("ImageNet 2011 Fall Release > plant, flora, plant life > phytoplankton", 2),
            entries[2]);

        // Paths are not unique: some parents have several children with the same label.
        Assert.Equal(60_718, entries.Select(e => e.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Parse_ReleasedStructureFile_SizesMatchSubtreeSpans()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestData", "structure_released.xml"));

        var entries = ImageNetXmlParser.Parse(stream, TestContext.Current.CancellationToken);

        // In pre-order a subtree is a contiguous run, so an entry's last descendant sits exactly `Size` rows later
        // and the row after that is outside its subtree.
        for (var i = 0; i < entries.Count; i++)
        {
            var (name, size) = entries[i];
            var prefix = name + TaxonomyPath.Separator;
            var last = i + size;

            Assert.True(size == 0 || entries[last].Name.StartsWith(prefix, StringComparison.Ordinal), name);
            Assert.True(
                last + 1 == entries.Count || !entries[last + 1].Name.StartsWith(prefix, StringComparison.Ordinal),
                name);
        }
    }

    private static IReadOnlyList<TaxonomyEntry> Parse(string xml)
    {
        using var stream = ToStream(xml);
        return ImageNetXmlParser.Parse(stream);
    }

    private static MemoryStream ToStream(string xml) => new(Encoding.UTF8.GetBytes(xml));
}
