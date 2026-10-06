using BioRT.Core.Models;
using BioRT.Core.Radiobiology;
using Xunit;

namespace BioRT.Tests;

public class GeneralizedEudTests
{
    [Fact]
    public void A1_EqualsArithmeticMeanForTwoEqualVolumeBins()
    {
        var dvh = TwoBinDvh(10.0, 20.0);

        double geud = GeneralizedEud.Calculate(dvh, a: 1.0);

        Assert.Equal(15.0, geud, precision: 12);
    }

    [Fact]
    public void A2_EqualsRootMeanSquareForTwoEqualVolumeBins()
    {
        var dvh = TwoBinDvh(10.0, 20.0);

        double geud = GeneralizedEud.Calculate(dvh, a: 2.0);

        Assert.Equal(Math.Sqrt(250.0), geud, precision: 12);
    }

    [Fact]
    public void ANegative1_EqualsHarmonicMeanForTwoEqualVolumeBins()
    {
        var dvh = TwoBinDvh(10.0, 20.0);

        double geud = GeneralizedEud.Calculate(dvh, a: -1.0);

        Assert.Equal(40.0 / 3.0, geud, precision: 12);
    }

    [Fact]
    public void AZero_UsesGeometricMeanLimit()
    {
        var dvh = TwoBinDvh(10.0, 20.0);

        double geud = GeneralizedEud.Calculate(dvh, a: 0.0);

        Assert.Equal(Math.Sqrt(200.0), geud, precision: 12);
    }

    [Fact]
    public void NegativeA_WithNonzeroVolumeAtZeroDose_ReturnsZero()
    {
        var dvh = new StructureDVH
        {
            Name = "ColdSpot",
            Dose = [0.0, 10.0, 20.0],
            Volume = [100.0, 50.0, 0.0],
            MeanDose = 5.0,
            MaxDose = 10.0
        };

        double geud = GeneralizedEud.Calculate(dvh, a: -10.0);

        Assert.Equal(0.0, geud, precision: 12);
    }

    [Fact]
    public void LkbDeff_UsesSameGeneralizedEudPowerLaw()
    {
        var dvh = TwoBinDvh(10.0, 20.0);

        double lkb = LkbModel.CalculateGEUD(dvh, n: 0.5);
        double geud = GeneralizedEud.Calculate(dvh, a: 2.0);

        Assert.Equal(geud, lkb, precision: 12);
    }

    private static StructureDVH TwoBinDvh(double d1, double d2)
    {
        double max = Math.Max(d1, d2);
        double min = Math.Min(d1, d2);

        return new StructureDVH
        {
            Name = "Synthetic",
            Dose = [0.0, min, max, max + 1.0],
            Volume = [100.0, 100.0, 50.0, 0.0],
            MeanDose = (d1 + d2) / 2.0,
            MaxDose = max
        };
    }
}
