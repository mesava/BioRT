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
        if (n <= 0)
            throw new ArgumentOutOfRangeException(nameof(n), "LKB volume parameter n must be > 0.");

        // LKB Deff is identical to gEUD with a = 1/n.
        return GeneralizedEud.Calculate(dvh, a: 1.0 / n);
    }
}
