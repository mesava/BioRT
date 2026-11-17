using BioRT.Core.DVH;
using BioRT.Core.Models;
using Xunit;

namespace BioRT.Tests;

/// <summary>
/// Numerical guardrails for absolute-volume DVH criteria. These tests
/// verify the dose-grid sampled volume basis, not contour-derived volume
/// or clinically acceptable voxel-size precision.
/// </summary>
public sealed class AbsoluteVolumeCriteriaTests
{
    [Theory]
    [InlineData(-0.01)]
    [InlineData(0.0)]
    [InlineData(0.004)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Dcc_DoesNotReturnAFalselyPassingZeroForInvalidRoiVolume(
        double requestedCc)
    {
        StructureDVH dvh = ExampleDvh();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => DoseMetricCalculator.Dcc(
                dvh,
                requestedCc,
                structureVolumeCc: 0.003));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void DccAndVcc_RejectInvalidSampledStructureVolume(
        double structureVolumeCc)
    {
        StructureDVH dvh = ExampleDvh();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => DoseMetricCalculator.Dcc(
                dvh, 0.001, structureVolumeCc));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => DoseMetricCalculator.VxxGyCc(
                dvh, 5.0, structureVolumeCc));
    }

    [Fact]
    public void ThreeKnownDoseVoxels_AreConsistentForVccAndDcc()
    {
        StructureDVH dvh = ExampleDvh();

        // One voxel is 1 mm³ = 0.001 cm³.
        // Two of the three voxels receive at least 5 Gy.
        double v5Cc = DoseMetricCalculator.VxxGyCc(
            dvh,
            doseGy: 5.0,
            structureVolumeCc: 0.003);

        Assert.Equal(0.002, v5Cc, precision: 12);

        // D100% is the coldest occupied 0.1 Gy histogram bin.
        double dWholeRoi = DoseMetricCalculator.Dcc(
            dvh,
            volumeCc: 0.003,
            structureVolumeCc: 0.003);

        Assert.Equal(2.0, dWholeRoi, precision: 12);
        Assert.Equal(10.0, DoseMetricCalculator.DxPercent(dvh, 0.0));
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(101.0)]
    [InlineData(double.NaN)]
    public void DxPercent_RejectsInvalidPercentages(double percent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DoseMetricCalculator.DxPercent(
                ExampleDvh(), percent));
    }

    private static StructureDVH ExampleDvh()
    {
        var dose = new DoseVolume
        {
            Dose = new double[3, 1, 1],
            OriginX = 0,
            OriginY = 0,
            OriginZ = 0,
            SpacingX = 1,
            SpacingY = 1,
            SpacingZ = 1,
            ZPositions = [0]
        };

        dose.Dose[0, 0, 0] = 2.0;
        dose.Dose[1, 0, 0] = 10.0;
        dose.Dose[2, 0, 0] = 10.0;

        var mask = new StructureMask
        {
            Name = "three-voxels",
            Mask = new bool[3, 1, 1]
        };

        for (int i = 0; i < 3; i++)
            mask.Mask[i, 0, 0] = true;

        DVHResult calculated = DVHCalculator.Calculate(mask, dose)!;

        return new StructureDVH
        {
            Name = mask.Name,
            Dose = calculated.DoseBins,
            Volume = calculated.VolumeBins,
            MeanDose = calculated.MeanDose,
            MaxDose = calculated.MaxDose
        };
    }
}
