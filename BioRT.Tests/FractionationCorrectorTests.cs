using BioRT.Core.Models;
using BioRT.Core.Radiobiology;
using Xunit;

namespace BioRT.Tests;

public class FractionationCorrectorTests
{
    [Fact]
    public void EqD2_IsIdentityForTwoGyFractions()
    {
        double eqd2 = FractionationCorrector.CalculateEquivalentDose(
            totalDoseGy: 60.0,
            fractions: 30,
            alphaBetaGy: 3.0,
            referenceFractionGy: 2.0);

        Assert.Equal(60.0, eqd2, precision: 12);
    }

    [Fact]
    public void EqD2_MatchesClosedFormHypofractionatedExample()
    {
        // 30 Gy / 5 fx = 6 Gy/fx, alpha/beta = 3 Gy
        // EQD2 = 30 * (6 + 3) / (2 + 3) = 54 Gy.
        double eqd2 = FractionationCorrector.CalculateEquivalentDose(
            totalDoseGy: 30.0,
            fractions: 5,
            alphaBetaGy: 3.0,
            referenceFractionGy: 2.0);

        Assert.Equal(54.0, eqd2, precision: 12);
    }

    [Fact]
    public void Bed_MatchesClosedFormExample()
    {
        // BED = 30 * (1 + 6/3) = 90 Gy.
        double bed = FractionationCorrector.CalculateBed(
            totalDoseGy: 30.0,
            fractions: 5,
            alphaBetaGy: 3.0);

        Assert.Equal(90.0, bed, precision: 12);
    }

    [Fact]
    public void PerBinEqd2Conversion_PreservesCumulativeVolumes()
    {
        var dvh = new StructureDVH
        {
            Name = "Synthetic",
            Dose = [0.0, 10.0, 20.0, 30.0],
            Volume = [100.0, 100.0, 50.0, 0.0],
            MeanDose = 15.0,
            MaxDose = 20.0
        };

        StructureDVH converted =
            FractionationCorrector.ConvertCumulativeDvhToEquivalentDose(
                dvh,
                fractions: 5,
                alphaBetaGy: 3.0,
                referenceFractionGy: 2.0);

        Assert.Equal(dvh.Volume, converted.Volume);
        Assert.Equal(
            FractionationCorrector.CalculateEquivalentDose(10.0, 5, 3.0, 2.0),
            converted.Dose[1],
            precision: 12);
        Assert.Equal(
            FractionationCorrector.CalculateEquivalentDose(20.0, 5, 3.0, 2.0),
            converted.Dose[2],
            precision: 12);
    }

    [Fact]
    public void PerBinEqd2Mean_IsCalculatedFromDifferentialBins()
    {
        // Equal volumes at 10 Gy and 20 Gy over 5 fractions.
        // 10 Gy -> EQD2 10 Gy; 20 Gy -> EQD2 28 Gy.
        // Mean EQD2 = 19 Gy.
        var dvh = new StructureDVH
        {
            Name = "Synthetic",
            Dose = [0.0, 10.0, 20.0, 30.0],
            Volume = [100.0, 100.0, 50.0, 0.0],
            MeanDose = 15.0,
            MaxDose = 20.0
        };

        StructureDVH converted =
            FractionationCorrector.ConvertCumulativeDvhToEquivalentDose(
                dvh,
                fractions: 5,
                alphaBetaGy: 3.0,
                referenceFractionGy: 2.0);

        Assert.Equal(19.0, converted.MeanDose, precision: 12);
    }

    [Fact]
    public void EquivalentTotalDose_IsIdentityWhenFractionCountIsUnchanged()
    {
        double equivalent =
            FractionationCorrector.CalculateEquivalentTotalDoseForFractions(
                totalDoseGy: 45.0,
                sourceFractions: 5,
                targetFractions: 5,
                alphaBetaGy: 10.0);

        Assert.Equal(45.0, equivalent, precision: 12);
    }

    [Fact]
    public void EquivalentTotalDose_PreservesLqIsoeffect()
    {
        const double totalDose = 36.0;
        const int sourceFractions = 6;
        const int targetFractions = 5;
        const double alphaBeta = 10.0;

        double equivalent =
            FractionationCorrector.CalculateEquivalentTotalDoseForFractions(
                totalDose,
                sourceFractions,
                targetFractions,
                alphaBeta);

        double d1 = totalDose / sourceFractions;
        double d2 = equivalent / targetFractions;

        double sourceEffect =
            sourceFractions * d1 * (d1 + alphaBeta);

        double targetEffect =
            targetFractions * d2 * (d2 + alphaBeta);

        Assert.Equal(sourceEffect, targetEffect, precision: 10);
    }

    [Fact]
    public void InvalidFractionCount_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FractionationCorrector.CalculateEquivalentDose(
                totalDoseGy: 30.0,
                fractions: 0,
                alphaBetaGy: 3.0));
    }
}
