using BioRT.Core.DVH;
using BioRT.Core.Models;
using Xunit;

namespace BioRT.Tests;

public class PtvSpatialMetricsTests
{
    [Fact]
    public void PerfectPrescriptionCoverage_HasCiOne()
    {
        var dose = SyntheticDose();
        var mask = new StructureMask
        {
            Name = "PTV",
            Mask = new bool[2, 1, 1]
        };
        mask.Mask[0, 0, 0] = true;

        double ci = PtvSpatialMetrics.ComputeCI(
            mask,
            dose,
            rxGy: 10.0);

        Assert.Equal(1.0, ci, precision: 12);
    }

    [Fact]
    public void TwoToOneFiftyPercentIsodoseVolume_HasGiTwo()
    {
        var dose = SyntheticDose();

        double gi = PtvSpatialMetrics.ComputeGI(
            dose,
            rxGy: 10.0);

        Assert.Equal(2.0, gi, precision: 12);
    }

    private static DoseVolume SyntheticDose()
    {
        var values = new double[2, 1, 1];
        values[0, 0, 0] = 10.0;
        values[1, 0, 0] = 5.0;

        return new DoseVolume
        {
            Dose = values,
            SpacingX = 10.0,
            SpacingY = 10.0,
            SpacingZ = 10.0,
            OriginX = 0.0,
            OriginY = 0.0,
            OriginZ = 0.0,
            ZPositions = [0.0]
        };
    }
}
