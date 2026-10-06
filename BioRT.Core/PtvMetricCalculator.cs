using BioRT.Core.Models;

namespace BioRT.Core.DVH;

public static class PtvMetricCalculator
{
    public static double D2(StructureDVH dvh)
        => DoseMetricCalculator.DxPercent(dvh, 2);

    public static double D98(StructureDVH dvh)
        => DoseMetricCalculator.DxPercent(dvh, 98);

    public static double D95(StructureDVH dvh)
        => DoseMetricCalculator.DxPercent(dvh, 95);

    public static double D50(StructureDVH dvh)
        => DoseMetricCalculator.DxPercent(dvh, 50);

    public static double HI(double d2, double d98)
        => d2 / d98;
}
