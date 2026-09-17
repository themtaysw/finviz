using Taxonomy.Core;
using Taxonomy.Ingest;

namespace Taxonomy.UnitTests.Core;

public sealed class TaxonomyTreeBuilderTests
{
    [Fact]
    public void Build_NestsEntriesUnderTheirParents()
    {
        TaxonomyEntry[] entries =
        [
            new("ImageNet 2011 Fall Release", 5),
            new("ImageNet 2011 Fall Release > plant, flora, plant life", 4),
            new("ImageNet 2011 Fall Release > plant, flora, plant life > phytoplankton", 2),
            new("ImageNet 2011 Fall Release > plant, flora, plant life > phytoplankton > planktonic algae", 0),
            new("ImageNet 2011 Fall Release > plant, flora, plant life > phytoplankton > diatom", 0),
            new("ImageNet 2011 Fall Release > plant, flora, plant life > microflora", 0),
        ];

        var expected = Node("ImageNet 2011 Fall Release", 5,
            Node("plant, flora, plant life", 4,
                Node("phytoplankton", 2,
                    Node("planktonic algae", 0),
                    Node("diatom", 0)),
                Node("microflora", 0)));

        Assert.Equivalent(expected, TaxonomyTreeBuilder.Build(entries), strict: true);
    }

    [Fact]
    public void Build_AttachesChildrenToTheClosestSameNamedParent()
    {
        TaxonomyEntry[] entries =
        [
            new("flower", 3),
            new("flower > coneflower", 0),
            new("flower > coneflower", 1),
            new("flower > coneflower > rudbeckia", 0),
        ];

        var expected = Node("flower", 3,
            Node("coneflower", 0),
            Node("coneflower", 1,
                Node("rudbeckia", 0)));

        Assert.Equivalent(expected, TaxonomyTreeBuilder.Build(entries), strict: true);
    }

    [Fact]
    public void Build_AcceptsASubtreeSliceWithDeeperLevelsOmitted()
    {
        TaxonomyEntry[] entries =
        [
            new("root > plant", 4),
            new("root > plant > phytoplankton", 2),
            new("root > plant > microflora", 0),
        ];

        var expected = Node("plant", 4,
            Node("phytoplankton", 2),
            Node("microflora", 0));

        Assert.Equivalent(expected, TaxonomyTreeBuilder.Build(entries), strict: true);
    }

    [Fact]
    public void Build_ReturnsNullForNoEntries()
    {
        Assert.Null(TaxonomyTreeBuilder.Build([]));
    }

    public static TheoryData<TaxonomyEntry[]> InvalidOrderings => new()
    {
        { [new("root", 0), new("other", 0)] },
        { [new("root > a", 0), new("root > b", 0)] },
        { [new("root", 2), new("root > a > b", 0), new("root > a", 1)] },
        { [new("root", 3), new("root > a", 1), new("root > b", 0), new("root > a > c", 0)] },
    };

    [Theory]
    [MemberData(nameof(InvalidOrderings))]
    public void Build_RejectsEntriesNotInPreorder(TaxonomyEntry[] entries)
    {
        Assert.Throws<FormatException>(() => TaxonomyTreeBuilder.Build(entries));
    }

    [Fact]
    public void Build_ReleasedStructureFile_RoundTripsToTheLinearForm()
    {
        using var xml = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestData", "structure_released.xml"));
        var entries = ImageNetXmlParser.Parse(xml, TestContext.Current.CancellationToken);

        var root = TaxonomyTreeBuilder.Build(entries);

        Assert.NotNull(root);
        Assert.Equal(entries, Flatten(root));
    }

    private static TaxonomyNode Node(string name, int size, params TaxonomyNode[] children) => new(name, size, children);

    private static IEnumerable<TaxonomyEntry> Flatten(TaxonomyNode root)
    {
        var pending = new Stack<(TaxonomyNode Node, string Path)>();
        pending.Push((root, root.Name));

        while (pending.TryPop(out var current))
        {
            yield return new TaxonomyEntry(current.Path, current.Node.Size);

            for (var i = current.Node.Children.Count - 1; i >= 0; i--)
            {
                var child = current.Node.Children[i];
                pending.Push((child, TaxonomyPath.Combine(current.Path, child.Name)));
            }
        }
    }
}
