using Taxonomy.Core;
using Taxonomy.Ingest;

namespace Taxonomy.UnitTests.Ingest;

public sealed class TaxonomyRecordTests
{
    [Fact]
    public void FromPreorder_DerivesIdsParentsDepthsAndLabels()
    {
        TaxonomyEntry[] entries =
        [
            new("root", 4),
            new("root > plant", 2),
            new("root > plant > algae", 0),
            new("root > plant > moss", 0),
            new("root > animal", 0),
            new("other root", 0),
        ];

        TaxonomyRecord[] expected =
        [
            new(1, null, 0, "root", entries[0]),
            new(2, 1, 1, "plant", entries[1]),
            new(3, 2, 2, "algae", entries[2]),
            new(4, 2, 2, "moss", entries[3]),
            new(5, 1, 1, "animal", entries[4]),
            new(6, null, 0, "other root", entries[5]),
        ];

        Assert.Equal(expected, TaxonomyRecord.FromPreorder(entries));
    }

    [Fact]
    public void FromPreorder_AttachesChildrenToTheirOwnSameNamedParent()
    {
        TaxonomyEntry[] entries =
        [
            new("flower", 3),
            new("flower > coneflower", 0),
            new("flower > coneflower", 1),
            new("flower > coneflower > rudbeckia", 0),
        ];

        var rudbeckia = TaxonomyRecord.FromPreorder(entries).Last();

        Assert.Equal(3, rudbeckia.ParentId);
    }

    public static TheoryData<TaxonomyEntry[]> InconsistentListings => new()
    {
        { [new("root", 2), new("root > a", 0)] },
        { [new("root", -1)] },
        { [new("root", 1), new("elsewhere > a", 0)] },
        { [new("root", 1), new("second root", 0)] },
        { [new("root", 0), new("root > a", 0)] },
        { [new("root", 2), new("root > a", 0), new("root > b", 1), new("root > b > c", 0)] },
    };

    [Theory]
    [MemberData(nameof(InconsistentListings))]
    public void FromPreorder_RejectsInconsistentListing(TaxonomyEntry[] entries)
    {
        Assert.Throws<FormatException>(() => TaxonomyRecord.FromPreorder(entries).ToList());
    }
}
