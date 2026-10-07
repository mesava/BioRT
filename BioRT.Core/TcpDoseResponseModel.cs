using BioRT.Core.Models;

namespace BioRT.Core.Radiobiology;

/// <summary>
/// TCP formulations reproduced from AAPM TG-166, Section II.D.
///
/// The input StructureDVH is cumulative. BioRT reconstructs differential
/// fractional volumes before evaluating the volume-weighted product:
///
/// TCP = product_i P(D_i)^v_i.
///
/// Implemented formulations:
/// - LQ Poisson (TG-166 Eq. 6);
/// - linear-Poisson with per-bin LQED2 (TG-166 Eq. 9);
/// - empirical logistic dose response (TG-166 Eq. 10).
/// </summary>
public static class TcpDoseResponseModel
{
    public readonly record struct LqParameters(
        double AlphaGyInv,
        double BetaGyInvSquared);

    /// <summary>
    /// Derives alpha and beta from D50, normalized gradient gamma and alpha/beta
    /// according to TG-166 Eqs. (7) and (8).
    /// </summary>
    public static LqParameters DeriveLqParameters(
        double d50Gy,
        double gamma,
        double alphaBetaGy)
    {
        ValidateDoseResponseParameters(
            d50Gy,
            gamma,
            alphaBetaGy);

        double c =
            Math.E * gamma -
            Math.Log(Math.Log(2.0));

        double alpha =
            c /
            (d50Gy *
             (1.0 + 2.0 / alphaBetaGy));

        double beta =
            c /
            (d50Gy *
             (alphaBetaGy + 2.0));

        return new LqParameters(alpha, beta);
    }

    /// <summary>
    /// TG-166 Eq. (6): Poisson TCP using an LQ cell-survival term.
    ///
    /// P(D_i) = exp[-exp(e*gamma - alpha*D_i - beta*D_i^2/N)]
    /// TCP = product_i P(D_i)^v_i
    /// </summary>
    public static double CalculateLqPoisson(
        StructureDVH dvh,
        int fractions,
        double d50Gy,
        double gamma,
        double alphaBetaGy)
    {
        if (fractions <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(fractions),
                "Number of fractions must be > 0.");

        LqParameters lq =
            DeriveLqParameters(
                d50Gy,
                gamma,
                alphaBetaGy);

        var bins = GetDifferentialBins(dvh);

        double logTcp = 0.0;

        foreach (var bin in bins)
        {
            double dose = Math.Max(0.0, bin.DoseGy);

            double exponent =
                Math.E * gamma -
                lq.AlphaGyInv * dose -
                lq.BetaGyInvSquared *
                dose * dose / fractions;

            double logLocalControl =
                NegativeExp(exponent);

            if (double.IsNegativeInfinity(logLocalControl))
                return 0.0;

            logTcp +=
                bin.FractionalVolume *
                logLocalControl;
        }

        return ProbabilityFromLog(logTcp);
    }

    /// <summary>
    /// TG-166 Eq. (9) after converting each differential DVH dose bin to LQED2.
    ///
    /// P(D_i) = exp[-exp(e*gamma
    ///          - (LQED2_i/D50)*(e*gamma - ln(ln 2)))]
    ///
    /// TG-166 states that this formulation is equivalent to the LQ Poisson
    /// formulation when the same fractionation assumptions are used.
    /// </summary>
    public static double CalculateLinearPoissonEqd2(
        StructureDVH dvh,
        int fractions,
        double d50Gy,
        double gamma,
        double alphaBetaGy)
    {
        if (fractions <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(fractions),
                "Number of fractions must be > 0.");

        ValidateDoseResponseParameters(
            d50Gy,
            gamma,
            alphaBetaGy);

        var bins = GetDifferentialBins(dvh);

        double c =
            Math.E * gamma -
            Math.Log(Math.Log(2.0));

        double logTcp = 0.0;

        foreach (var bin in bins)
        {
            double equivalentDose =
                FractionationCorrector.CalculateEquivalentDose(
                    totalDoseGy: Math.Max(0.0, bin.DoseGy),
                    fractions: fractions,
                    alphaBetaGy: alphaBetaGy,
                    referenceFractionGy: 2.0);

            double exponent =
                Math.E * gamma -
                equivalentDose / d50Gy * c;

            double logLocalControl =
                NegativeExp(exponent);

            if (double.IsNegativeInfinity(logLocalControl))
                return 0.0;

            logTcp +=
                bin.FractionalVolume *
                logLocalControl;
        }

        return ProbabilityFromLog(logTcp);
    }

    /// <summary>
    /// TG-166 Eq. (10), the empirical logistic model used by Okunieff et al.
    ///
    /// P(D_i) = exp[(D_i-D50)/k] / (1 + exp[(D_i-D50)/k])
    /// k = D50 / (4*gamma)
    ///
    /// The non-uniform target TCP is evaluated through TG-166 Eq. (5):
    /// TCP = product_i P(D_i)^v_i.
    /// </summary>
    public static double CalculateLogistic(
        StructureDVH dvh,
        double d50Gy,
        double gamma)
    {
        ValidatePositiveFinite(d50Gy, nameof(d50Gy));
        ValidatePositiveFinite(gamma, nameof(gamma));

        var bins = GetDifferentialBins(dvh);

        double k =
            d50Gy /
            (4.0 * gamma);

        double logTcp = 0.0;

        foreach (var bin in bins)
        {
            double z =
                (Math.Max(0.0, bin.DoseGy) - d50Gy) /
                k;

            double logLocalControl =
                LogSigmoid(z);

            if (double.IsNegativeInfinity(logLocalControl))
                return 0.0;

            logTcp +=
                bin.FractionalVolume *
                logLocalControl;
        }

        return ProbabilityFromLog(logTcp);
    }

    private static IReadOnlyList<DifferentialBin> GetDifferentialBins(
        StructureDVH dvh)
    {
        ArgumentNullException.ThrowIfNull(dvh);

        if (dvh.Dose == null || dvh.Volume == null)
            throw new ArgumentException(
                "DVH dose and volume arrays must not be null.",
                nameof(dvh));

        if (dvh.Dose.Length == 0 ||
            dvh.Volume.Length == 0)
        {
            throw new ArgumentException(
                "DVH dose and volume arrays must not be empty.",
                nameof(dvh));
        }

        if (dvh.Dose.Length != dvh.Volume.Length)
        {
            throw new ArgumentException(
                "DVH dose and volume arrays must have equal length.",
                nameof(dvh));
        }

        var raw = new List<DifferentialBin>();
        double totalFraction = 0.0;

        for (int i = 0; i < dvh.Dose.Length; i++)
        {
            double current =
                Math.Clamp(
                    dvh.Volume[i],
                    0.0,
                    100.0);

            double next =
                i + 1 < dvh.Volume.Length
                    ? Math.Clamp(
                        dvh.Volume[i + 1],
                        0.0,
                        100.0)
                    : 0.0;

            double differentialPercent =
                current - next;

            if (differentialPercent <= 0)
                continue;

            double fraction =
                differentialPercent / 100.0;

            raw.Add(
                new DifferentialBin(
                    Math.Max(0.0, dvh.Dose[i]),
                    fraction));

            totalFraction += fraction;
        }

        if (totalFraction <= 0)
        {
            throw new InvalidOperationException(
                "DVH contains no positive differential volume.");
        }

        // Protect against small numerical deviations from exactly 100%.
        return raw
            .Select(x => new DifferentialBin(
                x.DoseGy,
                x.FractionalVolume / totalFraction))
            .ToArray();
    }

    private static double NegativeExp(double exponent)
    {
        // log(P) = -exp(exponent).
        if (exponent > 709.0)
            return double.NegativeInfinity;

        if (exponent < -745.0)
            return 0.0;

        return -Math.Exp(exponent);
    }

    private static double LogSigmoid(double z)
    {
        if (z >= 0.0)
            return -Math.Log(1.0 + Math.Exp(-z));

        return z -
               Math.Log(1.0 + Math.Exp(z));
    }

    private static double ProbabilityFromLog(double logProbability)
    {
        if (double.IsNegativeInfinity(logProbability))
            return 0.0;

        if (logProbability >= 0.0)
            return 1.0;

        if (logProbability < -745.0)
            return 0.0;

        return Math.Clamp(
            Math.Exp(logProbability),
            0.0,
            1.0);
    }

    private static void ValidateDoseResponseParameters(
        double d50Gy,
        double gamma,
        double alphaBetaGy)
    {
        ValidatePositiveFinite(d50Gy, nameof(d50Gy));
        ValidatePositiveFinite(gamma, nameof(gamma));
        ValidatePositiveFinite(alphaBetaGy, nameof(alphaBetaGy));
    }

    private static void ValidatePositiveFinite(
        double value,
        string parameterName)
    {
        if (double.IsNaN(value) ||
            double.IsInfinity(value) ||
            value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Value must be finite and > 0.");
        }
    }

    private readonly record struct DifferentialBin(
        double DoseGy,
        double FractionalVolume);
}
