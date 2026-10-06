using Xunit;
using BioRT.Core.Models;
using BioRT.Core.Radiobiology;

namespace BioRT.Tests;

public class LkbModelTests
{
    [Fact]
    public void UniformDoseAtTd50_GivesFiftyPercentNtcp()
    {
        var dvh = UniformCumulativeDvh(50.0);

        double ntcp = LkbModel.CalculateNTCP(
            dvh,
            td50: 50.0,
            m: 0.20,
            n: 1.0);

        Assert.Equal(0.5, ntcp, precision: 12);
    }

    [Fact]
    public void UniformDoseOneSigmaAboveTd50_MatchesStandardNormalCdf()
    {
        // t = (60 - 50) / (0.2 * 50) = +1
        var dvh = UniformCumulativeDvh(60.0);

        double ntcp = LkbModel.CalculateNTCP(
            dvh,
            td50: 50.0,
            m: 0.20,
            n: 1.0);

        Assert.Equal(0.8413447460685429, ntcp, precision: 12);
    }

    [Fact]
    public void UniformDoseOneSigmaBelowTd50_MatchesStandardNormalCdf()
    {
        // t = (40 - 50) / (0.2 * 50) = -1
        var dvh = UniformCumulativeDvh(40.0);

        double ntcp = LkbModel.CalculateNTCP(
            dvh,
            td50: 50.0,
            m: 0.20,
            n: 1.0);

        Assert.Equal(0.15865525393145707, ntcp, precision: 12);
    }

    [Fact]
    public void GeudWithN1_UsesDifferentialVolume_NotCumulativeVolume()
    {
        // 50% of the structure receives 10 Gy and 50% receives 20 Gy.
        // Cumulative DVH:
        // V(0)=100%, V(10)=100%, V(20)=50%, V(30)=0%.
        // For n=1, Deff must be the arithmetic mean = 15 Gy.
        var dvh = new StructureDVH
        {
            Name = "Synthetic",
            Dose = [0.0, 10.0, 20.0, 30.0],
            Volume = [100.0, 100.0, 50.0, 0.0],
            MeanDose = 15.0,
            MaxDose = 20.0
        };

        double geud = LkbModel.CalculateGEUD(dvh, n: 1.0);

        Assert.Equal(15.0, geud, precision: 12);
    }

    [Fact]
    public void GeudWithN05_MatchesAnalyticTwoBinResult()
    {
        // Deff = (0.5*10^2 + 0.5*20^2)^0.5 = sqrt(250)
        var dvh = new StructureDVH
        {
            Name = "Synthetic",
            Dose = [0.0, 10.0, 20.0, 30.0],
            Volume = [100.0, 100.0, 50.0, 0.0],
            MeanDose = 15.0,
            MaxDose = 20.0
        };

        double geud = LkbModel.CalculateGEUD(dvh, n: 0.5);

        Assert.Equal(Math.Sqrt(250.0), geud, precision: 12);
    }

    private static StructureDVH UniformCumulativeDvh(double doseGy)
    {
        // Synthetic cumulative DVH with all volume concentrated at one dose bin.
        return new StructureDVH
        {
            Name = "Uniform",
            Dose = [0.0, doseGy, doseGy + 0.1],
            Volume = [100.0, 100.0, 0.0],
            MeanDose = doseGy,
            MaxDose = doseGy
        };
    }
}
