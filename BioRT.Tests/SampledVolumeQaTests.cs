using BioRT.Core.DVH;
using BioRT.Core.Models;
using Xunit;

namespace BioRT.Tests;

/// <summary>
/// Analytical tests for the CURRENT dose-grid-sampled ROI volume.
/// This is not a replacement for independent contour-derived volume
/// or clinically validated resampling between RTSTRUCT planes.
/// </summary>
public sealed class SampledVolumeQaTests
{
    [Fact]
    public void AxisAlignedPrism_MatchesKnownVoxelAndGeometricVolume()
    {
        // RTDOSE voxel centre spacing:
        // x: 0,2,4,6,8 mm; y: 0,1,2,3,4 mm;
        // z: 0,2,4 mm. Each voxel occupies 2x1x2 = 4 mm³.
        //
        // Contour rectangle covers exactly centres x=0..6
        // and y=0..2, with plane extent from z=-1 to z=5
        // for the 3 selected frame centres: 8x3x6 = 144 mm³.
        DoseVolume dose = new()
        {
            Dose = new double[5, 5, 3],
            OriginX = 0,
            OriginY = 0,
            OriginZ = 0,
            SpacingX = 2,
            SpacingY = 1,
            SpacingZ = 2,
            ZPositions = [0, 2, 4]
        };

        for (int x = 0; x < 5; x++)
        for (int y = 0; y < 5; y++)
        for (int z = 0; z < 3; z++)
            dose.Dose[x, y, z] = x <= 1 ? 5.0 : 15.0;

        var contours = new List<double[]>
        {
            Rect(-1, -0.5, 7, 2.5, z: 0),
            Rect(-1, -0.5, 7, 2.5, z: 2),
            Rect(-1, -0.5, 7, 2.5, z: 4)
        };

        StructureMask mask = MaskBuilder.BuildMask(
            "analytic-box",
            contours,
            dose);

        int count = mask.Mask.Cast<bool>().Count(inside => inside);
        Assert.Equal(36, count);

        double voxelVolumeCc =
            dose.SpacingX * dose.SpacingY * dose.SpacingZ / 1000.0;

        double sampledVolumeCc = count * voxelVolumeCc;
        double analyticPrismVolumeCc = 8.0 * 3.0 * 6.0 / 1000.0;

        Assert.Equal(0.004, voxelVolumeCc, precision: 12);
        Assert.Equal(analyticPrismVolumeCc, sampledVolumeCc, precision: 12);
        Assert.Equal(0.144, sampledVolumeCc, precision: 12);

        DVHResult dvh = DVHCalculator.Calculate(mask, dose)!;

        var cumulative = new StructureDVH
        {
            Name = "analytic-box",
            Dose = dvh.DoseBins,
            Volume = dvh.VolumeBins,
            MeanDose = dvh.MeanDose,
            MaxDose = dvh.MaxDose
        };

        // 18 of 36 voxels have 15 Gy, the others 5 Gy.
        Assert.Equal(10.0, dvh.MeanDose, precision: 12);
        Assert.Equal(15.0, dvh.MaxDose, precision: 12);
        Assert.Equal(50.0, DoseMetricCalculator.VxxGyPercent(cumulative, 10.0), precision: 9);
        Assert.Equal(
            0.072,
            DoseMetricCalculator.VxxGyCc(
                cumulative, 10.0, sampledVolumeCc),
            precision: 12);
    }

    [Fact]
    public void SmallStructure_OneVoxelRepresentsLargeFractionOfVolume()
    {
        // One voxel on this grid is 4 mm³. A five-voxel structure is
        // sampled in 20% volume quanta, not in continuous cm³.
        int selectedVoxelCount = 5;
        double voxelVolumeCc = 0.004;
        double sampledVolumeCc = selectedVoxelCount * voxelVolumeCc;

        double oneVoxelPercent = 100.0 * voxelVolumeCc / sampledVolumeCc;

        Assert.Equal(0.020, sampledVolumeCc, precision: 12);
        Assert.Equal(20.0, oneVoxelPercent, precision: 12);
    }

    private static double[] Rect(
        double x0, double y0,
        double x1, double y1,
        double z)
        =>
        [
            x0, y0, z,
            x1, y0, z,
            x1, y1, z,
            x0, y1, z
        ];
}
