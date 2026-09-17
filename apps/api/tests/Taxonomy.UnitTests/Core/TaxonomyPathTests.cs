using Taxonomy.Core;

namespace Taxonomy.UnitTests.Core;

public sealed class TaxonomyPathTests
{
    [Theory]
    [InlineData("root", "root", null)]
    [InlineData("root > plant, flora", "plant, flora", "root")]
    [InlineData("root > plant > algae", "algae", "root > plant")]
    public void GetLabelAndGetParent_SplitOnLastSeparator(string path, string label, string? parent)
    {
        Assert.Equal(label, TaxonomyPath.GetLabel(path));
        Assert.Equal(parent, TaxonomyPath.GetParent(path));
    }
}
