using BioRT.Core.DVH;
using BioRT.Core.Models;
using Xunit;

namespace BioRT.Tests;

/// <summary>
/// Research-only synthetic verification of the Stanley sphere-shell
/// metrics. This does NOT validate BioRT against the publisher's
/// large supplementary DICOM files; those stay outside public git.
/// </summary>
public sealed class StanleySphereBenchmarkTests
{
    [Fact]
    public void PairedTargetAndShell_GiveKnownVolumesV10V20CiGi()
    {
        DoseVolume dose = SmallDose();

        var names = new Dictionary<int, string>
        {
            [1] = "BODY",
            [2] = "GTV01",
            [3] = "GTV01_SHELL"
        };

        var target = Roi(
            Square(0.6, 0.6, 1.4, 1.4, 0),
            Square(0.6, 0.6, 1.4, 1.4, 1),
            Square(0.6, 0.6, 1.4, 1.4, 2));

        var shell = Roi(
            Square(-0.4, -0.4, 2.4, 2.4, 0),
            Square(-0.4, -0.4, 2.4, 2.4, 1),
            Square(-0.4, -0.4, 2.4, 2.4, 2));

        var rois = new Dictionary<int, RoiContours>
        {
            [2] = target,
            [3] = shell
        };

        StanleySphereBenchmarkResult report =
            StanleySphereBenchmark.Evaluate(
                dose, names, rois, nominalSphereDiameterMm: 10.0);

        StanleySphereBenchmarkRow row =
            Assert.Single(report.Spheres);

        Assert.Equal(1, report.SphereCount);
        Assert.Equal("GTV01", row.Target);
        Assert.Equal("GTV01_SHELL", row.EvaluationShell);
        Assert.Equal(
            Math.PI / 6.0,
            row.AnalyticalSphereVolumeCc,
            precision: 12);

        // Three 1 mm³ target voxels, 27 shell voxels.
        Assert.Equal(0.003, row.SampledTargetVolumeCc, precision: 12);
        Assert.Equal(0.027, row.SampledShellVolumeCc, precision: 12);
        Assert.Equal(0.003, row.V20GyCc, precision: 12);
        Assert.Equal(0.006, row.V10GyCc, precision: 12);
        Assert.Equal(25.0, row.TargetMeanDoseGy, precision: 12);
        Assert.Equal(25.0, row.TargetMaximumDoseGy, precision: 12);
        Assert.Equal(1.0, row.RtOGConformityIndex, precision: 12);
        Assert.Equal(
            2.0,
            row.PaddickGradientIndex!.Value,
            precision: 12);

        // Compare the same ROI geometry to production MaskBuilder
        // with exactly the same nearest Z and XY polygon convention.
        StructureMask reference = MaskBuilder.BuildMask(
            "GTV01",
            target.Polygons,
            dose,
            RoiContourFillRule.Union);

        Assert.Equal(
            3,
            reference.Mask.Cast<bool>().Count(v => v));

        Assert.Equal(
            reference.Mask.Cast<bool>().Count(v => v) / 1000.0,
            row.SampledTargetVolumeCc,
            precision: 12);
    }

    [Fact]
    public void MissingEvaluationShell_IsNotSilentlySubstituted()
    {
        DoseVolume dose = SmallDose();

        var names = new Dictionary<int, string>
        {
            [1] = "GTV01"
        };

        var rois = new Dictionary<int, RoiContours>
        {
            [1] = Roi(Square(0.6, 0.6, 1.4, 1.4, 0))
        };

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() =>
                StanleySphereBenchmark.Evaluate(
                    dose, names, rois, 10.0));

        Assert.Contains("Missing evaluation shell", exception.Message);
    }

    [Fact]
    public void DuplicateSphereName_IsRejectedRatherThanMixed()
    {
        DoseVolume dose = SmallDose();

        var names = new Dictionary<int, string>
        {
            [1] = "GTV01",
            [2] = "gtv01",
            [3] = "GTV01_SHELL"
        };

        Assert.Throws<InvalidOperationException>(() =>
            StanleySphereBenchmark.Evaluate(
                dose,
                names,
                new Dictionary<int, RoiContours>(),
                10.0));
    }

    private static DoseVolume SmallDose()
    {
        var values = new double[3, 3, 3];

        for (int k = 0; k < 3; k++)
        for (int j = 0; j < 3; j++)
        for (int i = 0; i < 3; i++)
            values[i, j, k] = 5.0;

        for (int k = 0; k < 3; k++)
        {
            values[1, 1, k] = 25.0;
            values[2, 1, k] = 15.0;
        }

        return new DoseVolume
        {
            Dose = values,
            OriginX = 0.0,
            OriginY = 0.0,
            OriginZ = 0.0,
            SpacingX = 1.0,
            SpacingY = 1.0,
            SpacingZ = 1.0,
            ZPositions = [0.0, 1.0, 2.0]
        };
    }

    private static RoiContours Roi(params double[][] polygons)
        => new()
        {
            FillRule = RoiContourFillRule.Union,
            Polygons = polygons.ToList()
        };

    private static double[] Square(
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
