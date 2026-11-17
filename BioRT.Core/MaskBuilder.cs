using BioRT.Core.Models;

namespace BioRT.Core.DVH;

public class MaskBuilder
{
    public static StructureMask BuildMask(
        string name,
        List<double[]> contours,
        DoseVolume dose,
        RoiContourFillRule fillRule = RoiContourFillRule.Union)
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
        // XOR polygons must come from a single contour-Z plane for each
        // dose frame. Distinct planes rounded onto the same frame cannot
        // be XORed without validated resampling.
        if (fillRule == RoiContourFillRule.Xor)
        {
            foreach (var slice in contoursBySlice)
            {
                double planeZ = slice.Value[0][2];

                if (slice.Value.Any(
                        polygon => Math.Abs(polygon[2] - planeZ) > 1e-3))
                {
                    throw new NotSupportedException(
                        $"RTSTRUCT ROI '{name}': distinct contour-Z planes " +
                        "map to one RTDOSE frame; XOR Z-resampling " +
                        "is not validated.");
                }

                if (Math.Abs(planeZ - zPos[slice.Key]) >
                    dose.SpacingZ / 2.0 + 1e-3)
                {
                    throw new NotSupportedException(
                        $"RTSTRUCT ROI '{name}': XOR contour plane " +
                        "lies outside nearest dose-slice extent.");
                }
            }
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
                    // DICOM IPP denotes the centre of dose voxel [0,0,0].
                    // RtDoseReader currently accepts only unrotated axial grids.
                    // PixelSpacing is already mapped to SpacingX (columns)
                    // and SpacingY (rows) by RtDoseReader.
                    double x = x0 + i * dx;
                    double y = y0 + j * dy;
                    bool inside = false;

                    foreach (double[] contour in sliceContours)
                    {
                        if (!PointInPolygon(x, y, contour))
                            continue;

                        if (fillRule == RoiContourFillRule.Xor)
                        {
                            // DICOM CLOSEDPLANAR_XOR: parity of all polygons.
                            inside = !inside;
                        }
                        else
                        {
                            inside = true;
                            break;
                        }
                    }

                    if (inside)
                    {
                        mask[i, j, k] = true;
                        filled++;
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
