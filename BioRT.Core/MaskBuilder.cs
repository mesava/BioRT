using BioRT.Core.Models;

namespace BioRT.Core.DVH;

public class MaskBuilder
{
    public static StructureMask BuildMask(
        string name,
        List<double[]> contours,
        DoseVolume dose)
    {
        int nx = dose.SizeX;
        int ny = dose.SizeY;
        int nz = dose.SizeZ;

        var mask = new bool[nx, ny, nz];

        double x0 = dose.OriginX;
        double y0 = dose.OriginY;

        double dx = dose.SpacingX;
        double dy = dose.SpacingY;

        var zPos = dose.ZPositions;

        // ================= Diagnostic =================

        double minZ = contours.Min(c => c[2]);
        double maxZ = contours.Max(c => c[2]);

        Console.WriteLine(
            $"[{name}] Contour Z range: {minZ:F1} – {maxZ:F1} mm");
        Console.WriteLine(
            $"[{name}] Dose Z range   : {zPos.Min():F1} – {zPos.Max():F1} mm");

        // ================= Group contours by nearest Z slice =================

        var contoursBySlice = new Dictionary<int, List<double[]>>();

        foreach (var contour in contours)
        {
            double z = contour[2];

            int k = FindNearestSlice(z, zPos);

            if (k < 0 || k >= nz)
                continue;

            if (!contoursBySlice.ContainsKey(k))
                contoursBySlice[k] = new List<double[]>();

            contoursBySlice[k].Add(contour);
        }

        // ================= Rasterization =================

        int filled = 0;

        foreach (var slice in contoursBySlice)
        {
            int k = slice.Key;
            var sliceContours = slice.Value;

            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    // Центр вокселя в patient coordinates
                    double x = x0 + (i + 0.5) * dx;
                    double y = y0 + (j + 0.5) * dy;

                    foreach (var contour in sliceContours)
                    {
                        if (PointInPolygon(x, y, contour))
                        {
                            mask[i, j, k] = true;
                            filled++;
                            break;
                        }
                    }
                }
        }

        Console.WriteLine($"[{name}] Mask voxels: {filled}");

        return new StructureMask
        {
            Name = name,
            Mask = mask
        };
    }

    // =========================================================
    // Find nearest RTDOSE slice by Z (TPS-like)
    // =========================================================
    private static int FindNearestSlice(double z, double[] zPositions)
    {
        int best = -1;
        double minDist = double.MaxValue;

        for (int k = 0; k < zPositions.Length; k++)
        {
            double d = Math.Abs(z - zPositions[k]);
            if (d < minDist)
            {
                minDist = d;
                best = k;
            }
        }

        return best;
    }

    // =========================================================
    // Standard Point-in-Polygon (Ray casting)
    // =========================================================
    private static bool PointInPolygon(
        double x, double y, double[] contour)
    {
        bool inside = false;
        int n = contour.Length / 3;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double xi = contour[3 * i];
            double yi = contour[3 * i + 1];

            double xj = contour[3 * j];
            double yj = contour[3 * j + 1];

            bool intersect =
                ((yi > y) != (yj > y)) &&
                (x < (xj - xi) * (y - yi) / (yj - yi) + xi);

            if (intersect)
                inside = !inside;
        }

        return inside;
    }
}
