using BioRT.Core.Models;

namespace BioRT.Core.Radiobiology;

/// <summary>
/// Linear-quadratic isoeffect conversions for courses delivered in equal fractions.
///
/// For total physical dose D delivered in N fractions:
///   BED = D * (1 + d/(alpha/beta)), d = D/N
///   EQD_ref = BED / (1 + d_ref/(alpha/beta))
///
/// The DVH conversion assumes the same spatial dose pattern is delivered in every
/// fraction of the course. It must not be used blindly for composite doses built
/// from phases with different fractionation schedules.
/// </summary>
public static class FractionationCorrector
{
    public static double CalculateBed(
        double totalDoseGy,
        int fractions,
        double alphaBetaGy)
    {
        Validate(totalDoseGy, fractions, alphaBetaGy, referenceFractionGy: 2.0);

        double dosePerFractionGy = totalDoseGy / fractions;

        return totalDoseGy *
               (1.0 + dosePerFractionGy / alphaBetaGy);
    }

    public static double CalculateEquivalentDose(
        double totalDoseGy,
        int fractions,
        double alphaBetaGy,
        double referenceFractionGy = 2.0)
    {
        Validate(totalDoseGy, fractions, alphaBetaGy, referenceFractionGy);

        double dosePerFractionGy = totalDoseGy / fractions;

        return totalDoseGy *
               (dosePerFractionGy + alphaBetaGy) /
               (referenceFractionGy + alphaBetaGy);
    }

    /// <summary>
    /// Converts one equal-fraction course to an isoeffective total dose
    /// delivered in a specified number of equal fractions.
    ///
    /// Solves:
    ///   N1*d1*(d1 + alpha/beta) = N2*d2*(d2 + alpha/beta)
    /// for d2 and returns N2*d2.
    ///
    /// This is useful for literature that reports, for example, a
    /// five-fraction-equivalent total dose rather than EQD2.
    /// </summary>
    public static double CalculateEquivalentTotalDoseForFractions(
        double totalDoseGy,
        int sourceFractions,
        int targetFractions,
        double alphaBetaGy)
    {
        Validate(
            totalDoseGy,
            sourceFractions,
            alphaBetaGy,
            referenceFractionGy: 2.0);

        if (targetFractions <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetFractions),
                "Target number of fractions must be > 0.");
        }

        double sourceDosePerFractionGy =
            totalDoseGy / sourceFractions;

        double isoeffect =
            sourceFractions *
            sourceDosePerFractionGy *
            (sourceDosePerFractionGy + alphaBetaGy);

        double discriminant =
            alphaBetaGy * alphaBetaGy +
            4.0 * isoeffect / targetFractions;

        double targetDosePerFractionGy =
            (-alphaBetaGy + Math.Sqrt(discriminant)) / 2.0;

        return targetFractions * targetDosePerFractionGy;
    }

    /// <summary>
    /// Converts every dose bin of a cumulative DVH to an equivalent-dose scale.
    /// Cumulative volume values remain unchanged because the transformation is monotonic.
    /// </summary>
    public static StructureDVH ConvertCumulativeDvhToEquivalentDose(
        StructureDVH dvh,
        int fractions,
        double alphaBetaGy,
        double referenceFractionGy = 2.0)
    {
        ArgumentNullException.ThrowIfNull(dvh);

        if (dvh.Dose == null || dvh.Volume == null)
            throw new ArgumentException("DVH dose and volume arrays must not be null.", nameof(dvh));

        if (dvh.Dose.Length == 0 || dvh.Volume.Length == 0)
            throw new ArgumentException("DVH dose and volume arrays must not be empty.", nameof(dvh));

        if (dvh.Dose.Length != dvh.Volume.Length)
            throw new ArgumentException("DVH dose and volume arrays must have equal length.", nameof(dvh));

        var convertedDose = dvh.Dose
            .Select(d => CalculateEquivalentDose(
                Math.Max(0.0, d),
                fractions,
                alphaBetaGy,
                referenceFractionGy))
            .ToArray();

        double convertedMean = DifferentialMean(
            convertedDose,
            dvh.Volume);

        double convertedMax = CalculateEquivalentDose(
            Math.Max(0.0, dvh.MaxDose),
            fractions,
            alphaBetaGy,
            referenceFractionGy);

        return new StructureDVH
        {
            Name = dvh.Name,
            Dose = convertedDose,
            Volume = dvh.Volume.ToArray(),
            MeanDose = convertedMean,
            MaxDose = convertedMax
        };
    }

    private static double DifferentialMean(
        double[] dose,
        double[] cumulativeVolumePercent)
    {
        double weightedDose = 0.0;
        double totalFraction = 0.0;

        for (int i = 0; i < dose.Length; i++)
        {
            double current = Math.Clamp(
                cumulativeVolumePercent[i],
                0.0,
                100.0);

            double next =
                i + 1 < cumulativeVolumePercent.Length
                    ? Math.Clamp(
                        cumulativeVolumePercent[i + 1],
                        0.0,
                        100.0)
                    : 0.0;

            double differentialPercent = current - next;

            if (differentialPercent <= 0)
                continue;

            double fraction = differentialPercent / 100.0;

            weightedDose += fraction * dose[i];
            totalFraction += fraction;
        }

        if (totalFraction <= 0)
            throw new InvalidOperationException(
                "DVH contains no positive differential volume.");

        return weightedDose / totalFraction;
    }

    private static void Validate(
        double totalDoseGy,
        int fractions,
        double alphaBetaGy,
        double referenceFractionGy)
    {
        if (double.IsNaN(totalDoseGy) ||
            double.IsInfinity(totalDoseGy) ||
            totalDoseGy < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalDoseGy),
                "Total dose must be finite and >= 0 Gy.");
        }

        if (fractions <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(fractions),
                "Number of fractions must be > 0.");

        if (double.IsNaN(alphaBetaGy) ||
            double.IsInfinity(alphaBetaGy) ||
            alphaBetaGy <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(alphaBetaGy),
                "Alpha/beta must be finite and > 0 Gy.");
        }

        if (double.IsNaN(referenceFractionGy) ||
            double.IsInfinity(referenceFractionGy) ||
            referenceFractionGy <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(referenceFractionGy),
                "Reference fraction size must be finite and > 0 Gy.");
        }
    }
}
