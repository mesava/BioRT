using BioRT.Core.Matching;
using Xunit;

namespace BioRT.Tests;

public class LungStructureMatcherTests
{
    private static string AliasesPath => Path.Combine(
        AppContext.BaseDirectory,
        "data",
        "aliases.json");

    [Theory]
    [InlineData("Lungs", "lung_total")]
    [InlineData("Total_Lungs", "lung_total")]
    [InlineData("Bilateral-Lungs", "lung_total")]
    [InlineData("Lungs-GTV", "lung_total_minus_gtv")]
    [InlineData("Lungs_GTV", "lung_total_minus_gtv")]
    [InlineData("Ipsilateral_Lung", "lung_ipsilateral")]
    public void ExplicitLungAliases_MapToExpectedCanonicalStructure(
        string structure,
        string expected)
    {
        var matcher = new StructureMatcher(AliasesPath);

        Assert.Equal(expected, matcher.Match(structure));
    }

    [Theory]
    [InlineData("Lung_L")]
    [InlineData("Lung_R")]
    [InlineData("Left Lung")]
    [InlineData("Right Lung")]
    public void IndividualLungSides_AreNotSilentlyMappedToTotalLung(
        string structure)
    {
        var matcher = new StructureMatcher(AliasesPath);

        Assert.Null(matcher.Match(structure));
    }
}
