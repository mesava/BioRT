using BioRT.Core.Models;
using BioRT.Core.Radiobiology;
using Xunit;

namespace BioRT.Tests;

public class TcpDoseResponseModelTests
{
    [Fact]
    public void DerivedLqParameters_PreserveRequestedAlphaBetaRatio()
    {
        var p = TcpDoseResponseModel.DeriveLqParameters(
            d50Gy: 60.0,
            gamma: 2.0,
            alphaBetaGy: 10.0);

        Assert.True(p.AlphaGyInv > 0.0);
        Assert.True(p.BetaGyInvSquared > 0.0);

        Assert.Equal(
            10.0,
            p.AlphaGyInv / p.BetaGyInvSquared,
            precision: 12);
    }

    [Fact]
    public void LqPoisson_UniformD50AtTwoGyFractions_GivesFiftyPercent()
    {
        var dvh = UniformCumulativeDvh(60.0);

        double tcp = TcpDoseResponseModel.CalculateLqPoisson(
            dvh,
            fractions: 30,
            d50Gy: 60.0,
            gamma: 2.0,
            alphaBetaGy: 10.0);

        Assert.Equal(0.5, tcp, precision: 12);
    }

    [Fact]
    public void LinearPoissonEqd2_UniformD50AtTwoGyFractions_GivesFiftyPercent()
    {
        var dvh = UniformCumulativeDvh(60.0);

        double tcp = TcpDoseResponseModel.CalculateLinearPoissonEqd2(
            dvh,
            fractions: 30,
            d50Gy: 60.0,
            gamma: 2.0,
            alphaBetaGy: 10.0);

        Assert.Equal(0.5, tcp, precision: 12);
    }

    [Fact]
    public void Logistic_UniformD50_GivesFiftyPercent()
    {
        var dvh = UniformCumulativeDvh(60.0);

        double tcp = TcpDoseResponseModel.CalculateLogistic(
            dvh,
            d50Gy: 60.0,
            gamma: 2.0);

        Assert.Equal(0.5, tcp, precision: 12);
    }

    [Fact]
    public void LqPoisson_AndLinearPoissonEqd2_AreEquivalent()
    {
        var dvh = TwoBinCumulativeDvh(
            dose1Gy: 30.0,
            dose2Gy: 60.0);

        double lq = TcpDoseResponseModel.CalculateLqPoisson(
            dvh,
            fractions: 30,
            d50Gy: 60.0,
            gamma: 1.8,
            alphaBetaGy: 10.0);

        double linear = TcpDoseResponseModel.CalculateLinearPoissonEqd2(
            dvh,
            fractions: 30,
            d50Gy: 60.0,
            gamma: 1.8,
            alphaBetaGy: 10.0);

        Assert.Equal(lq, linear, precision: 12);
    }

    [Fact]
    public void LogisticTcp_IncreasesWithUniformDose()
    {
        double low = TcpDoseResponseModel.CalculateLogistic(
            UniformCumulativeDvh(50.0),
            d50Gy: 60.0,
            gamma: 2.0);

        double high = TcpDoseResponseModel.CalculateLogistic(
            UniformCumulativeDvh(70.0),
            d50Gy: 60.0,
            gamma: 2.0);

        Assert.True(low < 0.5);
        Assert.True(high > 0.5);
        Assert.True(high > low);
    }

    [Fact]
    public void PoissonTcp_IncreasesWithUniformDose()
    {
        double low = TcpDoseResponseModel.CalculateLinearPoissonEqd2(
            UniformCumulativeDvh(50.0),
            fractions: 25,
            d50Gy: 60.0,
            gamma: 2.0,
            alphaBetaGy: 10.0);

        double high = TcpDoseResponseModel.CalculateLinearPoissonEqd2(
            UniformCumulativeDvh(70.0),
            fractions: 35,
            d50Gy: 60.0,
            gamma: 2.0,
            alphaBetaGy: 10.0);

        Assert.True(low < 0.5);
        Assert.True(high > 0.5);
        Assert.True(high > low);
    }

    [Fact]
    public void ColdSubvolume_ReducesTcpForSameHighDoseElsewhere()
    {
        var uniformHigh = UniformCumulativeDvh(70.0);
        var halfCold = TwoBinCumulativeDvh(
            dose1Gy: 45.0,
            dose2Gy: 70.0);

        double uniformTcp =
            TcpDoseResponseModel.CalculateLogistic(
                uniformHigh,
                d50Gy: 60.0,
                gamma: 2.0);

        double heterogeneousTcp =
            TcpDoseResponseModel.CalculateLogistic(
                halfCold,
                d50Gy: 60.0,
                gamma: 2.0);

        Assert.True(heterogeneousTcp < uniformTcp);
    }

    [Fact]
    public void InvalidFractionCount_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TcpDoseResponseModel.CalculateLqPoisson(
                UniformCumulativeDvh(60.0),
                fractions: 0,
                d50Gy: 60.0,
                gamma: 2.0,
                alphaBetaGy: 10.0));
    }

    private static StructureDVH UniformCumulativeDvh(double doseGy)
    {
        return new StructureDVH
        {
            Name = "SyntheticTarget",
            Dose = [0.0, doseGy, doseGy + 0.1],
            Volume = [100.0, 100.0, 0.0],
            MeanDose = doseGy,
            MaxDose = doseGy
        };
    }

    private static StructureDVH TwoBinCumulativeDvh(
        double dose1Gy,
        double dose2Gy)
    {
        double low = Math.Min(dose1Gy, dose2Gy);
        double high = Math.Max(dose1Gy, dose2Gy);

        return new StructureDVH
        {
            Name = "SyntheticTarget",
            Dose = [0.0, low, high, high + 0.1],
            Volume = [100.0, 100.0, 50.0, 0.0],
            MeanDose = (low + high) / 2.0,
            MaxDose = high
        };
    }
}
