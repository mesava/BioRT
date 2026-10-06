using BioRT.Core.Models;

namespace BioRT.Core.Radiobiology;

/// <summary>
/// Generalized equivalent uniform dose (gEUD) for BioRT's cumulative DVH representation.
///
/// gEUD = (sum_i(v_i * D_i^a))^(1/a)
///
/// The DVH stored by BioRT is cumulative, therefore differential fractional volumes are
/// reconstructed from adjacent cumulative bins before applying the power-law equation.
/// </summary>
public static class GeneralizedEud
{
    public static double Calculate(StructureDVH dvh, double a)
    {
        ArgumentNullException.ThrowIfNull(dvh);

        if (dvh.Dose == null || dvh.Volume == null)
            throw new ArgumentException("DVH dose and volume arrays must not be null.", nameof(dvh));

        if (dvh.Dose.Length == 0 || dvh.Volume.Length == 0)
            throw new ArgumentException("DVH dose and volume arrays must not be empty.", nameof(dvh));

        if (dvh.Dose.Length != dvh.Volume.Length)
            throw new ArgumentException("DVH dose and volume arrays must have equal length.", nameof(dvh));

        double totalFraction = 0.0;

        // a -> 0 is the geometric-mean limit of the generalized mean.
        if (Math.Abs(a) < 1e-12)
        {
            double sumLog = 0.0;

            for (int i = 0; i < dvh.Dose.Length; i++)
            {
                double vFrac = DifferentialFraction(dvh.Volume, i);

                if (vFrac <= 0)
                    continue;

                double dose = Math.Max(0.0, dvh.Dose[i]);

                if (dose <= 0)
                    return 0.0;

                sumLog += vFrac * Math.Log(dose);
                totalFraction += vFrac;
            }

            if (totalFraction <= 0)
                throw new InvalidOperationException("DVH contains no positive differential volume.");

            return Math.Exp(sumLog / totalFraction);
        }

        double sum = 0.0;

        for (int i = 0; i < dvh.Dose.Length; i++)
        {
            double vFrac = DifferentialFraction(dvh.Volume, i);

            if (vFrac <= 0)
                continue;

            double dose = Math.Max(0.0, dvh.Dose[i]);

            // For a negative exponent, any non-zero volume at zero dose drives gEUD to zero.
            if (dose <= 0 && a < 0)
                return 0.0;

            sum += vFrac * Math.Pow(dose, a);
            totalFraction += vFrac;
        }

        if (totalFraction <= 0)
            throw new InvalidOperationException("DVH contains no positive differential volume.");

        return Math.Pow(sum / totalFraction, 1.0 / a);
    }

    private static double DifferentialFraction(double[] cumulativeVolumePercent, int index)
    {
        double current = Math.Clamp(cumulativeVolumePercent[index], 0.0, 100.0);
        double next =
            index + 1 < cumulativeVolumePercent.Length
                ? Math.Clamp(cumulativeVolumePercent[index + 1], 0.0, 100.0)
                : 0.0;

        double differentialPercent = current - next;

        return differentialPercent > 0
            ? differentialPercent / 100.0
            : 0.0;
    }
}
