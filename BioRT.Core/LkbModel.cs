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

        return CalculateNTCPFromEffectiveDose(
            gEud,
            td50,
            m);
    }

    /// <summary>
    /// Calculates LKB NTCP when the effective uniform dose has already been
    /// derived on the dose basis required by the parameter set.
    /// </summary>
    public static double CalculateNTCPFromEffectiveDose(
        double effectiveDoseGy,
        double td50,
        double m)
    {
        if (effectiveDoseGy < 0 ||
            double.IsNaN(effectiveDoseGy) ||
            double.IsInfinity(effectiveDoseGy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(effectiveDoseGy),
                "Effective dose must be finite and >= 0 Gy.");
        }

        if (td50 <= 0)
            throw new ArgumentOutOfRangeException(nameof(td50), "TD50 must be > 0.");

        if (m <= 0)
            throw new ArgumentOutOfRangeException(nameof(m), "m must be > 0.");

        double t = (effectiveDoseGy - td50) / (m * td50);

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
