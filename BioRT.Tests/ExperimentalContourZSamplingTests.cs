using BioRT.Core.DVH;
using BioRT.Core.Models;
using Xunit;

namespace BioRT.Tests;

/// <summary>
/// Analytical experiments with explicitly declared Z end-cap conventions.
/// This is not clinical commissioning of interpolation or a replacement
/// for Monaco DVH and structure-volume ground truth.
/// </summary>
public sealed class ExperimentalContourZSamplingTests
{
    [Fact]
    public void ConstantAreaPrism_ExposesLegacyMissingIntermediateDoseFrames()
    {
        DoseVolume dose = MakeGrid(5, 5, 5);
        for (int z = 0; z < 5; z++)
        for (int y = 0; y < 5; y++)
        for (int x = 0; x < 5; x++)
            dose.Dose[x, y, z] = z * z;

        RoiContours roi = Roi(
            Square(-0.4, -0.4, 4.4, 4.4, 0),
            Square(-0.4, -0.4, 4.4, 4.4, 4));

        ZSamplingComparison result =
            ExperimentalContourZSampling.Compare(
                roi, dose,
                thresholdGy: 10.0,
                ZEndCapPolicy.NoExtrapolation);

        // Each dose voxel has 1 mm³. The *declared synthetic*
        // 5-frame reference slab is [-0.5,4.5] mm:
        // A=25 mm², thickness=5 mm => 0.125 cc.
        Assert.Equal(2, result.SourceContourPlaneCount);
        Assert.Equal(0.050, result.Legacy.VolumeCc, precision: 12);
        Assert.Equal(0.125, result.NearestContour.VolumeCc, precision: 12);
        Assert.Equal(0.125, result.LinearOccupancy.VolumeCc, precision: 12);

        Assert.Equal(8.0, result.Legacy.MeanDoseGy!.Value, precision: 12);
        Assert.Equal(6.0, result.NearestContour.MeanDoseGy!.Value, precision: 12);
        Assert.Equal(6.0, result.LinearOccupancy.MeanDoseGy!.Value, precision: 12);

        Assert.Equal(50.0, result.Legacy.VThresholdPercent!.Value, precision: 12);
        Assert.Equal(20.0, result.NearestContour.VThresholdPercent!.Value, precision: 12);
        Assert.Equal(20.0, result.LinearOccupancy.VThresholdPercent!.Value, precision: 12);
    }

    [Fact]
    public void TaperedCrossSections_SeparateNearestFromFractionalOccupancy()
    {
        DoseVolume dose = MakeGrid(2, 2, 5);
        RoiContours roi = Roi(
            Square(-0.4, -0.4, 1.4, 1.4, 0),
            Square(-0.4, -0.4, 0.4, 0.4, 4));

        ZSamplingComparison result =
            ExperimentalContourZSampling.Compare(
                roi, dose,
                thresholdGy: 0.0,
                ZEndCapPolicy.NoExtrapolation);

        // Legacy: 4 + 1 = 5 occupied voxels.
        // Nearest: 4+4+4+1+1 = 14 occupied voxels.
        // Linear mask mixture:
        // constant one voxel * 5 frames = 5;
        // other 3 voxels * (1+.75+.5+.25+0) = 7.5.
        Assert.Equal(0.005, result.Legacy.VolumeCc, precision: 12);
        Assert.Equal(0.014, result.NearestContour.VolumeCc, precision: 12);
        Assert.Equal(0.0125, result.LinearOccupancy.VolumeCc, precision: 12);

        // The 0.0125 cc comparator is a specifically constructed
        // *linearly varying occupancy* reference, not proof that
        // real contour boundaries should interpolate this way.
    }

    [Fact]
    public void EndCapChoice_IsExplicitAndCanChangeSmallStructureVolume()
    {
        DoseVolume dose = MakeGrid(1, 1, 5);
        RoiContours roi = Roi(
            Square(-0.4, -0.4, 0.4, 0.4, 0.4),
            Square(-0.4, -0.4, 0.4, 0.4, 3.6));

        ZSamplingComparison none =
            ExperimentalContourZSampling.Compare(
                roi, dose, 0.0,
                ZEndCapPolicy.NoExtrapolation);

        ZSamplingComparison halfFrame =
            ExperimentalContourZSampling.Compare(
                roi, dose, 0.0,
                ZEndCapPolicy.ExtendHalfDoseFrame);

        Assert.Equal(0.002, none.Legacy.VolumeCc, precision: 12);
        Assert.Equal(0.003, none.NearestContour.VolumeCc, precision: 12);
        Assert.Equal(0.003, none.LinearOccupancy.VolumeCc, precision: 12);

        Assert.Equal(0.005, halfFrame.NearestContour.VolumeCc, precision: 12);
        Assert.Equal(0.005, halfFrame.LinearOccupancy.VolumeCc, precision: 12);
    }

    [Fact]
    public void NestedXorHole_IsRespectedByAllThreeMethods()
    {
        DoseVolume dose = MakeGrid(3, 3, 5);

        RoiContours roi = new()
        {
            FillRule = RoiContourFillRule.Xor,
            Polygons =
            [
                Square(-0.4, -0.4, 2.4, 2.4, 0),
                Square(0.6, 0.6, 1.4, 1.4, 0),
                Square(-0.4, -0.4, 2.4, 2.4, 4),
                Square(0.6, 0.6, 1.4, 1.4, 4)
            ]
        };

        ZSamplingComparison result =
            ExperimentalContourZSampling.Compare(
                roi, dose, 0.0,
                ZEndCapPolicy.NoExtrapolation);

        Assert.Equal(0.016, result.Legacy.VolumeCc, precision: 12);
        Assert.Equal(0.040, result.NearestContour.VolumeCc, precision: 12);
        Assert.Equal(0.040, result.LinearOccupancy.VolumeCc, precision: 12);
    }

    [Fact]
    public void MissingSecondContourPlane_CannotInferStructureThickness()
    {
        DoseVolume dose = MakeGrid(1, 1, 5);

        Assert.Throws<NotSupportedException>(() =>
            ExperimentalContourZSampling.Compare(
                Roi(Square(-0.4, -0.4, 0.4, 0.4, 0)),
                dose, 0.0,
                ZEndCapPolicy.NoExtrapolation));
    }

    [Fact]
    public void IrregularDoseZSteps_AreRejected()
    {
        DoseVolume dose = MakeGrid(1, 1, 5);
        dose.ZPositions = [0, 1, 2, 3.2, 4.2];

        Assert.Throws<NotSupportedException>(() =>
            ExperimentalContourZSampling.Compare(
                Roi(
                    Square(-0.4, -0.4, 0.4, 0.4, 0),
                    Square(-0.4, -0.4, 0.4, 0.4, 4)),
                dose, 0.0,
                ZEndCapPolicy.NoExtrapolation));
    }

    private static DoseVolume MakeGrid(int nx, int ny, int nz)
        => new()
        {
            Dose = new double[nx, ny, nz],
            OriginX = 0,
            OriginY = 0,
            OriginZ = 0,
            SpacingX = 1,
            SpacingY = 1,
            SpacingZ = 1,
            ZPositions = Enumerable.Range(0, nz)
                .Select(x => (double)x)
                .ToArray()
        };

    private static RoiContours Roi(params double[][] polygons)
        => new()
        {
            FillRule = RoiContourFillRule.Union,
            Polygons = polygons.ToList()
        };

    private static double[] Square(
        double x0, double y0, double x1, double y1, double z)
        =>
        [
            x0, y0, z,
            x1, y0, z,
            x1, y1, z,
            x0, y1, z
        ];
}
