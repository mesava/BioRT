using BioRT.Core.DVH;
using BioRT.Core.Models;
using Xunit;

namespace BioRT.Tests;

public sealed class ContourZGeometryAuditTests
{
    [Fact]
    public void AlignedAxialPlanes_MapOneToOneWithoutDetectedZCollision()
    {
        var report = ContourZGeometryAudit.Analyze(
            [
                Square(z: 0.2),
                Square(z: 2.1),
                Square(z: 4.1)
            ],
            Dose3Frames());

        Assert.Equal(3, report.TotalContours);
        Assert.Equal(3, report.UniqueContourPlaneCount);
        Assert.Equal(0, report.CollidingDoseFrameCount);
        Assert.Equal(0, report.OutsideDosePlaneCount);
        Assert.True(report.NoDetectedZMappingIssues);
        Assert.Equal(0.2, report.MaximumNearestDoseDistanceMm, precision: 10);
    }

    [Fact]
    public void TwoDifferentContourZPlanesMappingToOneDoseFrame_AreReported()
    {
        var report = ContourZGeometryAudit.Analyze(
            [Square(z: 0.1), Square(z: 0.3), Square(z: 2.1)],
            Dose3Frames());

        Assert.Equal(3, report.UniqueContourPlaneCount);
        Assert.Equal(1, report.CollidingDoseFrameCount);
        Assert.False(report.NoDetectedZMappingIssues);
        Assert.Equal(0, report.OutsideDosePlaneCount);
    }

    [Fact]
    public void MultipleContoursOnSamePlane_AreNotMistakenForZCollision()
    {
        var report = ContourZGeometryAudit.Analyze(
            [Square(z: 0), Square(z: 0), Square(z: 2)],
            Dose3Frames());

        Assert.Equal(3, report.TotalContours);
        Assert.Equal(2, report.UniqueContourPlaneCount);
        Assert.Equal(0, report.CollidingDoseFrameCount);
        Assert.Equal(2, report.Planes[0].ContourCount);
    }

    [Fact]
    public void PlaneBeyondDoseSlab_IsExplicitlyReported()
    {
        var report = ContourZGeometryAudit.Analyze(
            [Square(z: 0), Square(z: 2), Square(z: 6.5)],
            Dose3Frames());

        Assert.Equal(1, report.OutsideDosePlaneCount);
        Assert.True(report.Planes.Last().OutsideDoseExtent);
        Assert.False(report.NoDetectedZMappingIssues);
    }

    [Fact]
    public void OneFrameDose_HasUnknownSlabThickness_AndCannotBeDeclaredClean()
    {
        DoseVolume dose = Dose3Frames();
        dose.Dose = new double[1, 1, 1];
        dose.ZPositions = [0.0];

        var report = ContourZGeometryAudit.Analyze(
            [Square(z: 0.1)], dose);

        Assert.False(report.HasKnownDoseSlabThickness);
        Assert.False(report.NoDetectedZMappingIssues);
    }

    [Fact]
    public void NonuniformDoseFrameSteps_AreRejectedBeforeZMapping()
    {
        DoseVolume dose = Dose3Frames();
        dose.ZPositions = [0, 2, 5];

        Assert.Throws<ArgumentException>(() =>
            ContourZGeometryAudit.Analyze([Square(z: 2)], dose));
    }

    private static DoseVolume Dose3Frames() => new()
    {
        Dose = new double[1, 1, 3],
        OriginX = 0,
        OriginY = 0,
        OriginZ = 0,
        SpacingX = 1,
        SpacingY = 1,
        SpacingZ = 2,
        ZPositions = [0, 2, 4]
    };

    private static double[] Square(double z)
        => [0, 0, z, 1, 0, z, 1, 1, z, 0, 1, z];
}

public sealed class SimpleContourInterplaneVolumeTests
{
    [Fact]
    public void TwoConstantAreaPlanes_IntegrateKnownPrismWithoutEndCapExtrapolation()
    {
        var roi = Roi(
            Rect(0, 0, 10, 10, z: 0),
            Rect(0, 0, 10, 10, z: 10));

        ContourInterplaneVolumeResult result =
            SimpleContourInterplaneVolume.Estimate(roi);

        Assert.True(result.Available);
        Assert.Equal(1.0, result.InterplaneVolumeCc!.Value, precision: 12);
        Assert.Equal(100.0, result.PlaneAreas[0].AreaMm2, precision: 12);
        Assert.Equal(100.0, result.PlaneAreas[1].AreaMm2, precision: 12);
    }

    [Fact]
    public void UnequalPlaneSpacing_UsesAreaTrapezoids_WithoutExtrapolation()
    {
        var roi = Roi(
            Rect(0, 0, 10, 10, z: 0),
            Rect(0, 0, 20, 10, z: 5),
            Rect(0, 0, 10, 10, z: 10));

        ContourInterplaneVolumeResult result =
            SimpleContourInterplaneVolume.Estimate(roi);

        // 0.5*(100+200)*5 + 0.5*(200+100)*5 = 1500 mm³.
        Assert.True(result.Available);
        Assert.Equal(1.5, result.InterplaneVolumeCc!.Value, precision: 12);
    }

    [Fact]
    public void SinglePlane_CannotInferThreeDimensionalThickness()
    {
        ContourInterplaneVolumeResult result =
            SimpleContourInterplaneVolume.Estimate(
                Roi(Rect(0, 0, 10, 10, z: 0)));

        Assert.False(result.Available);
        Assert.Null(result.InterplaneVolumeCc);
        Assert.Contains("two contour planes", result.UnavailableReason);
    }

    [Fact]
    public void MultipleIslandsOnOnePlane_RefuseNaiveAreaSummation()
    {
        ContourInterplaneVolumeResult result =
            SimpleContourInterplaneVolume.Estimate(
                Roi(
                    Rect(0, 0, 10, 10, z: 0),
                    Rect(20, 20, 25, 25, z: 0),
                    Rect(0, 0, 10, 10, z: 10)));

        Assert.False(result.Available);
        Assert.Contains("Multiple contours", result.UnavailableReason);
    }

    [Fact]
    public void XorHole_RefusesNaiveShoelaceArea()
    {
        RoiContours roi = new()
        {
            FillRule = RoiContourFillRule.Xor,
            Polygons =
            [
                Rect(0, 0, 10, 10, z: 0),
                Rect(3, 3, 7, 7, z: 0)
            ]
        };

        ContourInterplaneVolumeResult result =
            SimpleContourInterplaneVolume.Estimate(roi);

        Assert.False(result.Available);
        Assert.Contains("XOR", result.UnavailableReason);
    }

    [Fact]
    public void SelfIntersectingBowTieContour_RefusesUnreliableVolume()
    {
        double[] bowTie =
        [
            0, 0, 0,
            10, 10, 0,
            0, 10, 0,
            10, 0, 0
        ];

        ContourInterplaneVolumeResult result =
            SimpleContourInterplaneVolume.Estimate(
                Roi(bowTie, Rect(0, 0, 10, 10, z: 10)));

        Assert.False(result.Available);
        Assert.Contains("Self-touching", result.UnavailableReason);
    }

    private static RoiContours Roi(params double[][] polygons) => new()
    {
        Polygons = polygons.ToList(),
        FillRule = RoiContourFillRule.Union
    };

    private static double[] Rect(
        double left, double bottom, double right, double top, double z)
        =>
        [
            left, bottom, z,
            right, bottom, z,
            right, top, z,
            left, top, z
        ];
}
