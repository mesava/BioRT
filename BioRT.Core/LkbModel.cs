using BioRT.Core.Models;
using MathNet.Numerics.Distributions;

namespace BioRT.Core.Radiobiology;

public static class LkbModel
{
    // ================= PUBLIC API =================

    public static double CalculateNTCP(
        StructureDVH dvh,
        double td50,
        double m,
        double n)
    {
        if (td50 <= 0)
            throw new ArgumentOutOfRangeException(nameof(td50), "TD50 must be > 0.");

        if (m <= 0)
            throw new ArgumentOutOfRangeException(nameof(m), "m must be > 0.");

        double gEud = CalculateGEUD(dvh, n);

        double t = (gEud - td50) / (m * td50);

        return Normal.CDF(0.0, 1.0, t);
    }

    // ================= gEUD / Deff =================

    /// <summary>
    /// Calculates LKB effective dose / gEUD from the cumulative DVH representation
    /// currently used by BioRT.
    ///
    /// StructureDVH.Volume[i] is the cumulative percentage volume receiving
    /// at least Dose[i]. The LKB/gEUD equation requires differential fractional
    /// volumes vi, so each bin contribution is derived from the drop in the
    /// cumulative DVH between adjacent dose bins.
    ///
    /// For the LKB model, a = 1/n and:
    /// Deff = (sum(vi * Di^(1/n)))^n.
    /// </summary>
    public static double CalculateGEUD(
        StructureDVH dvh,
        double n)
    {
        ArgumentNullException.ThrowIfNull(dvh);

        if (n <= 0)
            throw new ArgumentOutOfRangeException(nameof(n), "LKB volume parameter n must be > 0.");

        if (dvh.Dose == null || dvh.Volume == null)
            throw new ArgumentException("DVH dose and volume arrays must not be null.", nameof(dvh));

        if (dvh.Dose.Length == 0 || dvh.Volume.Length == 0)
            throw new ArgumentException("DVH dose and volume arrays must not be empty.", nameof(dvh));

        if (dvh.Dose.Length != dvh.Volume.Length)
            throw new ArgumentException("DVH dose and volume arrays must have equal length.", nameof(dvh));

        double sum = 0.0;
        double totalFraction = 0.0;
        double exponent = 1.0 / n;

        for (int i = 0; i < dvh.Dose.Length; i++)
        {
            double currentCumulative = Math.Clamp(dvh.Volume[i], 0.0, 100.0);
            double nextCumulative =
                i + 1 < dvh.Volume.Length
                    ? Math.Clamp(dvh.Volume[i + 1], 0.0, 100.0)
                    : 0.0;

            // Differential volume in this dose bin.
            double differentialPercent = currentCumulative - nextCumulative;

            if (differentialPercent <= 0)
                continue;

            double vFrac = differentialPercent / 100.0;
            double dose = Math.Max(0.0, dvh.Dose[i]);

            sum += vFrac * Math.Pow(dose, exponent);
            totalFraction += vFrac;
        }

        if (totalFraction <= 0)
            throw new InvalidOperationException("DVH contains no positive differential volume.");

        // Normalize to protect against small numerical deviations from exactly 100%.
        return Math.Pow(sum / totalFraction, n);
    }
}
