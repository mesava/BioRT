using BioRT.Core.Models;

namespace BioRT.Core.DVH;

public static class DoseMetricCalculator
{
    // ================= Dxx% =================
    public static double DxPercent(StructureDVH dvh, double percent)
    {
        for (int i = 0; i < dvh.Volume.Length; i++)
        {
            if (dvh.Volume[i] <= percent)
                return dvh.Dose[i];
        }
        return dvh.Dose.Last();
    }

    // ================= Dmean =================
    public static double Dmean(StructureDVH dvh)
        => dvh.MeanDose;

    // ================= Dmax =================
    public static double Dmax(StructureDVH dvh)
        => dvh.MaxDose;

    // ================= VxxGy (%) =================
    public static double VxxGyPercent(StructureDVH dvh, double doseGy)
    {
        for (int i = 1; i < dvh.Dose.Length; i++)
        {
            if (dvh.Dose[i] >= doseGy)
            {
                // линейная интерполяция
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

        // если doseGy выше max дозы
        return 0.0;
    }

    // ================= VxxGy (cm3) =================
    public static double VxxGyCc(
        StructureDVH dvh,
        double doseGy,
        double structureVolumeCc)
    {
        double vPercent = VxxGyPercent(dvh, doseGy);
        return vPercent / 100.0 * structureVolumeCc;
    }

    // ================= Dcc =================
    public static double Dcc(
        StructureDVH dvh,
        double volumeCc,
        double structureVolumeCc)
    {
        double percent = 100.0 * volumeCc / structureVolumeCc;
        return DxPercent(dvh, percent);
    }
}