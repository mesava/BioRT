using BioRT.Core.DVH;
using BioRT.Core.Models;
using Xunit;

namespace BioRT.Tests;

public class DoseMetricCalculatorTests
{
    [Fact]
    public void VxxGyPercent_InterpolatesCumulativeDvh()
    {
        var dvh = new StructureDVH
        {
            Name = "Synthetic",
            Dose = [0.0, 10.0, 20.0, 30.0],
            Volume = [100.0, 80.0, 40.0, 0.0],
            MeanDose = 0.0,
            MaxDose = 30.0
        };

        double v15 = DoseMetricCalculator.VxxGyPercent(dvh, 15.0);

        Assert.Equal(60.0, v15, precision: 12);
    }

    [Fact]
    public void VxxGyCc_UsesStructureVolume()
    {
        var dvh = new StructureDVH
        {
            Name = "Synthetic",
            Dose = [0.0, 10.0, 20.0, 30.0],
            Volume = [100.0, 80.0, 40.0, 0.0],
            MeanDose = 0.0,
            MaxDose = 30.0
        };

        double v15cc = DoseMetricCalculator.VxxGyCc(
            dvh,
            doseGy: 15.0,
            structureVolumeCc: 200.0);

        Assert.Equal(120.0, v15cc, precision: 12);
    }
}
