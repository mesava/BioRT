using BioRT.Core.Models;

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
        double gEud = CalculateGEUD(dvh, n);

        double t = (gEud - td50) / (m * td50);

        return NormalCdf(t);
    }

    // ================= gEUD =================

    public static double CalculateGEUD(
        StructureDVH dvh,
        double n)
    {
        double sum = 0.0;

        for (int i = 0; i < dvh.Dose.Length; i++)
        {
            double vFrac = dvh.Volume[i] / 100.0;

            if (vFrac <= 0)
                continue;

            sum += vFrac * Math.Pow(dvh.Dose[i], 1.0 / n);
        }

        return Math.Pow(sum, n);
    }

    // ================= Normal CDF =================

    private static double NormalCdf(double t)
    {
        // Abramowitz & Stegun approximation
        double sign = Math.Sign(t);
        t = Math.Abs(t) / Math.Sqrt(2.0);

        double a1 = 0.254829592;
        double a2 = -0.284496736;
        double a3 = 1.421413741;
        double a4 = -1.453152027;
        double a5 = 1.061405429;
        double p = 0.3275911;

        double erf = 1 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1)
                         * Math.Exp(-t * t)
                         / (Math.Sqrt(Math.PI) * t + p);

        return 0.5 * (1 + sign * erf);
    }
}
