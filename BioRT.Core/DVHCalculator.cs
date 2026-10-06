using BioRT.Core.Models;

namespace BioRT.Core.DVH;

public class DVHCalculator
{
    public static DVHResult Calculate(
        StructureMask mask,
        DoseVolume dose,
        double binSizeGy = 0.1)
    {
        var values = new List<double>();

        for (int i = 0; i < dose.SizeX; i++)
            for (int j = 0; j < dose.SizeY; j++)
                for (int k = 0; k < dose.SizeZ; k++)
                {
                    if (mask.Mask[i, j, k])
                        values.Add(dose.Dose[i, j, k]);
                }

        if (values.Count == 0)
            return null;

        double maxDose = values.Max();
        int bins = (int)Math.Ceiling(maxDose / binSizeGy) + 1;

        var hist = new double[bins];

        foreach (var d in values)
        {
            int b = (int)(d / binSizeGy);
            hist[b]++;
        }

        // cumulative
        for (int i = bins - 2; i >= 0; i--)
            hist[i] += hist[i + 1];

        double total = hist[0];

        return new DVHResult
        {
            StructureName = mask.Name,
            DoseBins = Enumerable.Range(0, bins)
                .Select(i => i * binSizeGy)
                .ToArray(),

            VolumeBins = hist
                .Select(v => 100.0 * v / total)
                .ToArray(),

            MeanDose = values.Average(),
            MaxDose = maxDose
        };
    }
}
