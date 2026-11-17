using BioRT.Core.DVH;
using BioRT.Core.Models;
using Xunit;

namespace BioRT.Tests;

/// <summary>
/// Synthetic analytic XOR geometry. All contours are axial and share a
/// physical plane; pixel centres are at integer patient coordinates.
/// </summary>
public sealed class MaskBuilderXorTests
{
    [Fact]
    public void NestedHole_IsSubtractedInsteadOfUnioned()
    {
        DoseVolume dose = Dose5x5();
        List<double[]> contours =
        [
            Square(-0.4, -0.4, 4.4, 4.4),
            Square(0.5, 0.5, 3.5, 3.5)
        ];

        StructureMask xor = MaskBuilder.BuildMask(
            "donut", contours, dose, RoiContourFillRule.Xor);

        StructureMask union = MaskBuilder.BuildMask(
            "filled", contours, dose);

        Assert.Equal(16, Selected(xor));
        Assert.Equal(25, Selected(union));
        Assert.False(xor.Mask[2, 2, 0]);
        Assert.True(xor.Mask[0, 0, 0]);
        Assert.True(union.Mask[2, 2, 0]);
    }

    [Fact]
    public void NestedThreePolygonXor_RestoresAnInteriorIsland()
    {
        DoseVolume dose = Dose5x5();
        List<double[]> contours =
        [
            Square(-0.4, -0.4, 4.4, 4.4),
            Square(0.5, 0.5, 3.5, 3.5),
            Square(1.5, 1.5, 2.5, 2.5)
        ];

        StructureMask forward = MaskBuilder.BuildMask(
            "island", contours, dose, RoiContourFillRule.Xor);
        StructureMask reversed = MaskBuilder.BuildMask(
            "island-reordered",
            contours.AsEnumerable().Reverse().ToList(),
            dose,
            RoiContourFillRule.Xor);

        Assert.Equal(17, Selected(forward));
        Assert.Equal(17, Selected(reversed));
        Assert.True(forward.Mask[2, 2, 0]);
        Assert.False(forward.Mask[1, 1, 0]);

        for (int i = 0; i < 5; i++)
        for (int j = 0; j < 5; j++)
            Assert.Equal(forward.Mask[i, j, 0], reversed.Mask[i, j, 0]);
    }

    [Fact]
    public void DisjointXorPolygons_BothRemainIncluded()
    {
        StructureMask mask = MaskBuilder.BuildMask(
            "disjoint",
            [
                Square(-0.4, -0.4, 0.4, 0.4),
                Square(3.6, 3.6, 4.4, 4.4)
            ],
            Dose5x5(),
            RoiContourFillRule.Xor);

        Assert.Equal(2, Selected(mask));
        Assert.True(mask.Mask[0, 0, 0]);
        Assert.True(mask.Mask[4, 4, 0]);
    }

    [Fact]
    public void TwoDistinctContourPlanesMappingToOneFrame_FailSafely()
    {
        DoseVolume dose = Dose5x5();
        List<double[]> contours =
        [
            Square(-0.4, -0.4, 4.4, 4.4, z: 0.0),
            Square(0.5, 0.5, 3.5, 3.5, z: 0.2)
        ];

        Assert.Throws<NotSupportedException>(() =>
            MaskBuilder.BuildMask(
                "cross-plane",
                contours,
                dose,
                RoiContourFillRule.Xor));
    }

    private static int Selected(StructureMask mask)
    {
        int count = 0;
        foreach (bool inside in mask.Mask)
            if (inside) count++;

        return count;
    }

    private static DoseVolume Dose5x5() => new()
    {
        Dose = new double[5, 5, 1],
        OriginX = 0,
        OriginY = 0,
        OriginZ = 0,
        SpacingX = 1,
        SpacingY = 1,
        SpacingZ = 1,
        ZPositions = [0.0]
    };

    private static double[] Square(
        double x0, double y0,
        double x1, double y1,
        double z = 0)
        =>
        [
            x0, y0, z,
            x1, y0, z,
            x1, y1, z,
            x0, y1, z
        ];
}
