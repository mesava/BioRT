using BioRT.Core.Models;

namespace BioRT.Core.DVH;

public static class DoseMetricCalculator
{
    // Dx% of the cumulative DVH (dose-bin discretization is 0.1 Gy).
    public static double DxPercent(StructureDVH dvh, double percent)
    {
        if (!double.IsFinite(percent) || percent < 0.0 || percent > 100.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percent),
                "Dx% must be a finite percentage from 0 through 100.");
        }

        // D0% is the hottest point; D100% approximates the coldest
        // occupied histogram bin, not the (always 100%) zero-dose bin.
        if (percent == 0.0)
            return dvh.MaxDose;

        if (percent == 100.0)
        {
            for (int i = 1; i < dvh.Volume.Length; i++)
            {
                if (dvh.Volume[i] < 100.0)
                    return dvh.Dose[i - 1];
            }

            return dvh.Dose.Last();
        }

        for (int i = 0; i < dvh.Volume.Length; i++)
        {
            if (dvh.Volume[i] <= percent)
                return dvh.Dose[i];
        }

        return dvh.Dose.Last();
    }

    public static double Dmean(StructureDVH dvh) => dvh.MeanDose;

    public static double Dmax(StructureDVH dvh) => dvh.MaxDose;

    public static double VxxGyPercent(StructureDVH dvh, double doseGy)
    {
        if (!double.IsFinite(doseGy) || doseGy < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(doseGy),
                "Vxx dose threshold must be finite and nonnegative.");
        }

        for (int i = 1; i < dvh.Dose.Length; i++)
        {
            if (dvh.Dose[i] >= doseGy)
            {
                double d1 = dvh.Dose[i - 1];
                double d2 = dvh.Dose[i];
                double v1 = dvh.Volume[i - 1];
                double v2 = dvh.Volume[i];

                if (Math.Abs(d2 - d1) < 1e-6)
                    return v2;

                double t = (doseGy - d1) / (d2 - d1);
                return v1 + t * (v2 - v1);
            }
        }

        return 0.0;
    }

    public static double VxxGyCc(
        StructureDVH dvh,
        double doseGy,
        double structureVolumeCc)
    {
        RequirePositiveSampledVolume(structureVolumeCc);

        double vPercent = VxxGyPercent(dvh, doseGy);
        return vPercent / 100.0 * structureVolumeCc;
    }

    public static double Dcc(
        StructureDVH dvh,
        double volumeCc,
        double structureVolumeCc)
    {
        RequirePositiveSampledVolume(structureVolumeCc);

        // Never turn D0.1cc in a 0.05cc ROI into an invalid Dx>100%.
        // The old function could then return 0 Gy and incorrectly PASS.
        if (!double.IsFinite(volumeCc) ||
            volumeCc <= 0.0 ||
            volumeCc > structureVolumeCc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(volumeCc),
                $"Requested Dcc must be finite, >0, and <= " +
                $"the dose-grid-sampled ROI volume ({structureVolumeCc:G17} cm3).");
        }

        double percent = 100.0 * volumeCc / structureVolumeCc;
        return DxPercent(dvh, percent);
    }

    private static void RequirePositiveSampledVolume(double volumeCc)
    {
        if (!double.IsFinite(volumeCc) || volumeCc <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(volumeCc),
                "Dose-grid-sampled ROI volume must be finite and positive.");
        }
    }
}
