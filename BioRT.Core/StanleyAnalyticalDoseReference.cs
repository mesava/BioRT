namespace BioRT.Core.DVH;

/// <summary>
/// Independent radial analytical reference for Stanley et al.,
/// "Accuracy of dose-volume metric calculation for small-volume
/// radiosurgery targets", Medical Physics 2021, Eq. 1,
/// DOI: 10.1002/mp.14645.
///
/// IMPORTANT: diameter is in mm, radial coordinate is in cm in
/// the published piecewise model. Nominal prescription: 20 Gy at
/// the sphere surface. The model here is research ground truth,
/// not a treatment-planning prescription or clinical TCP/NTCP model.
/// </summary>
public static class StanleyAnalyticalDoseReference
{
    public const double S1 = 0.249;
    public const double S2 = 7.019;
    public const double S3 = 0.029;
    public const double S4 = 1.927;
    public const double PrescriptionGy = 20.0;

    private const double NormalizedSurfaceDose = 1.0 - S1;

    public static double DoseAtRadiusGy(
        double radialDistanceCm,
        double sphereDiameterMm)
    {
        ValidateDiameter(sphereDiameterMm);

        if (!double.IsFinite(radialDistanceCm) || radialDistanceCm < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(radialDistanceCm));
        }

        double sphereRadiusCm = sphereDiameterMm / 20.0;

        double normalized =
            radialDistanceCm <= sphereRadiusCm
                ? 1.0 - S1 *
                    Math.Exp(-S2 * (sphereRadiusCm - radialDistanceCm))
                : S3 + (1.0 - S1 - S3) *
                    Math.Exp(-S4 * (radialDistanceCm - sphereRadiusCm));

        // The analytical distribution is normalized to 20 Gy on the
        // prescribed spherical surface, not at the central dose maximum.
        return PrescriptionGy * normalized / NormalizedSurfaceDose;
    }

    public static StanleyAnalyticalVolumes Calculate(
        double sphereDiameterMm)
    {
        ValidateDiameter(sphereDiameterMm);

        double sphereRadiusCm = sphereDiameterMm / 20.0;
        double halfPrescriptionNormalized =
            0.5 * NormalizedSurfaceDose;

        if (halfPrescriptionNormalized <= S3)
        {
            throw new InvalidOperationException(
                "Analytical 50%-isodose is undefined for current parameters.");
        }

        // 20 Gy is the reference dose at R; 10 Gy is found by
        // inverting the OUTER exponential branch of Stanley Eq. 1.
        double radiusAt10GyCm =
            sphereRadiusCm -
            Math.Log(
                (halfPrescriptionNormalized - S3) /
                (1.0 - S1 - S3)) / S4;

        double volume20Cc =
            4.0 * Math.PI / 3.0 *
            Math.Pow(sphereRadiusCm, 3);

        double volume10Cc =
            4.0 * Math.PI / 3.0 *
            Math.Pow(radiusAt10GyCm, 3);

        return new StanleyAnalyticalVolumes(
            sphereDiameterMm,
            sphereRadiusCm,
            radiusAt10GyCm,
            volume20Cc,
            volume10Cc,
            volume10Cc / volume20Cc,
            DoseAtRadiusGy(0.0, sphereDiameterMm));
    }

    private static void ValidateDiameter(double sphereDiameterMm)
    {
        if (!double.IsFinite(sphereDiameterMm) || sphereDiameterMm <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(sphereDiameterMm));
        }
    }
}

/// <summary>
/// Ground-truth, continuous-sphere reference. V20 and V10 are analytic
/// entire isodose volumes; the discrete comparator uses GTVxx_SHELL.
/// </summary>
public sealed record StanleyAnalyticalVolumes(
    double SphereDiameterMm,
    double SphereRadiusCm,
    double RadiusAt10GyCm,
    double V20GyCc,
    double V10GyCc,
    double GradientIndex,
    double CentralDoseGy);
