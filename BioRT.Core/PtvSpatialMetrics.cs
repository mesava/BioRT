using BioRT.Core.Models;

namespace BioRT.Core.DVH;

public static class PtvSpatialMetrics
{
    public static double ComputeCI(
        StructureMask ptvMask,
        DoseVolume dose,
        double rxGy)
    {
        double voxelVolume =
            dose.SpacingX * dose.SpacingY * dose.SpacingZ / 1000.0; // mm3 → cm3

        double tv = 0.0;
        double piv = 0.0;
        double tv_piv = 0.0;

        for (int i = 0; i < dose.SizeX; i++)
            for (int j = 0; j < dose.SizeY; j++)
                for (int k = 0; k < dose.SizeZ; k++)
                {
                    bool inPTV = ptvMask.Mask[i, j, k];
                    bool inIso = dose.Dose[i, j, k] >= rxGy;

                    if (inPTV) tv += voxelVolume;
                    if (inIso) piv += voxelVolume;
                    if (inPTV && inIso) tv_piv += voxelVolume;
                }

        if (tv == 0 || piv == 0)
            return double.NaN;

        return (tv_piv * tv_piv) / (tv * piv);
    }
    public static double ComputeGI(
    DoseVolume dose,
    double rxGy)
    {
        double voxelVolume =
            dose.SpacingX * dose.SpacingY * dose.SpacingZ / 1000.0; // cm3

        double v100 = 0.0;
        double v50 = 0.0;

        for (int i = 0; i < dose.SizeX; i++)
            for (int j = 0; j < dose.SizeY; j++)
                for (int k = 0; k < dose.SizeZ; k++)
                {
                    double d = dose.Dose[i, j, k];

                    if (d >= rxGy)
                        v100 += voxelVolume;

                    if (d >= 0.5 * rxGy)
                        v50 += voxelVolume;
                }

        if (v100 == 0)
            return double.NaN;

        return v50 / v100;
    }

}