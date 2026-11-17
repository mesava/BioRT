using BioRT.Core.Models;

namespace BioRT.Core.DVH;

/// <summary>
/// Cross-sectional area integration between explicitly supplied RTSTRUCT
/// contour planes. This is an inter-plane numerical estimate, NOT a
/// clinically established geometric ROI volume: no end-cap extrapolation,
/// slab-thickness inference, topological polygon union, or resampling.
/// </summary>
public static class SimpleContourInterplaneVolume
{
    private const double PlaneToleranceMm = 1e-3;
    private const double AreaToleranceMm2 = 1e-8;

    public static ContourInterplaneVolumeResult Estimate(RoiContours roi)
    {
        ArgumentNullException.ThrowIfNull(roi);

        if (roi.FillRule != RoiContourFillRule.Union)
        {
            return Unavailable(
                "XOR contours require validated polygon Boolean area operations.");
        }

        if (roi.Polygons.Count < 2)
        {
            return Unavailable(
                "At least two contour planes are needed; no end-slab thickness is assumed.");
        }

        var areas = new List<ContourPlaneArea>(roi.Polygons.Count);

        foreach (double[] polygon in roi.Polygons)
        {
            if (polygon == null ||
                polygon.Length < 9 ||
                polygon.Length % 3 != 0 ||
                polygon.Any(v => !double.IsFinite(v)))
            {
                return Unavailable("Invalid contour coordinate array.");
            }

            double z = polygon[2];

            for (int i = 5; i < polygon.Length; i += 3)
            {
                if (Math.Abs(polygon[i] - z) > PlaneToleranceMm)
                {
                    return Unavailable(
                        "Non-axial contour is unsupported by this area integration.");
                }
            }

            if (HasSelfIntersection(polygon))
            {
                return Unavailable(
                    "Self-touching or self-intersecting/keyhole polygon needs topology validation.");
            }

            double areaMm2 = ShoelaceAreaMm2(polygon);

            if (!double.IsFinite(areaMm2) || areaMm2 <= AreaToleranceMm2)
                return Unavailable("Degenerate contour with zero/invalid area.");

            areas.Add(new ContourPlaneArea(z, areaMm2));
        }

        areas.Sort((a, b) => a.ZMm.CompareTo(b.ZMm));

        for (int i = 1; i < areas.Count; i++)
        {
            if (Math.Abs(areas[i].ZMm - areas[i - 1].ZMm) <=
                PlaneToleranceMm)
            {
                return Unavailable(
                    "Multiple contours on one Z plane require polygon union; " +
                    "their areas cannot simply be added.");
            }
        }

        double volumeMm3 = 0;

        for (int i = 1; i < areas.Count; i++)
        {
            double dz = areas[i].ZMm - areas[i - 1].ZMm;

            volumeMm3 +=
                0.5 * (areas[i - 1].AreaMm2 + areas[i].AreaMm2) * dz;
        }

        if (!double.IsFinite(volumeMm3) || volumeMm3 <= 0)
            return Unavailable("Invalid computed inter-plane volume.");

        return new ContourInterplaneVolumeResult(
            true,
            volumeMm3 / 1000.0,
            areas,
            null);
    }

    private static ContourInterplaneVolumeResult Unavailable(string reason)
        => new(false, null, Array.Empty<ContourPlaneArea>(), reason);

    private static double ShoelaceAreaMm2(double[] polygon)
    {
        int n = polygon.Length / 3;
        double baseX = polygon[0];
        double baseY = polygon[1];
        double twiceArea = 0;

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            double xi = polygon[3 * i] - baseX;
            double yi = polygon[3 * i + 1] - baseY;
            double xj = polygon[3 * j] - baseX;
            double yj = polygon[3 * j + 1] - baseY;
            twiceArea += xi * yj - xj * yi;
        }

        return Math.Abs(twiceArea) / 2.0;
    }

    // For the first scientific stage, do not claim an unambiguous area for
    // self-crossing, zero-width channel, or self-touching keyhole contours.
    private static bool HasSelfIntersection(double[] polygon)
    {
        int n = polygon.Length / 3;
        for (int i = 0; i < n; i++)
        {
            int nextI = (i + 1) % n;

            for (int j = i + 1; j < n; j++)
            {
                int nextJ = (j + 1) % n;

                // Adjacent segments share one vertex by definition.
                if (i == j || nextI == j || nextJ == i)
                    continue;

                if (Intersects(polygon, i, nextI, j, nextJ))
                    return true;
            }
        }

        return false;
    }

    private static bool Intersects(
        double[] p, int a, int b, int c, int d)
    {
        const double tolerance = 1e-9;

        double o1 = Orient(p, a, b, c);
        double o2 = Orient(p, a, b, d);
        double o3 = Orient(p, c, d, a);
        double o4 = Orient(p, c, d, b);

        if ((o1 > tolerance && o2 < -tolerance ||
             o1 < -tolerance && o2 > tolerance) &&
            (o3 > tolerance && o4 < -tolerance ||
             o3 < -tolerance && o4 > tolerance))
        {
            return true;
        }

        return (Math.Abs(o1) <= tolerance && OnSegment(p, a, b, c)) ||
               (Math.Abs(o2) <= tolerance && OnSegment(p, a, b, d)) ||
               (Math.Abs(o3) <= tolerance && OnSegment(p, c, d, a)) ||
               (Math.Abs(o4) <= tolerance && OnSegment(p, c, d, b));
    }

    private static double Orient(
        double[] p, int a, int b, int c)
    {
        double ax = p[3 * a], ay = p[3 * a + 1];
        double bx = p[3 * b], by = p[3 * b + 1];
        double cx = p[3 * c], cy = p[3 * c + 1];

        return (bx - ax) * (cy - ay) -
               (by - ay) * (cx - ax);
    }

    private static bool OnSegment(
        double[] p, int a, int b, int q)
    {
        const double tolerance = 1e-9;

        double ax = p[3 * a], ay = p[3 * a + 1];
        double bx = p[3 * b], by = p[3 * b + 1];
        double qx = p[3 * q], qy = p[3 * q + 1];

        return qx >= Math.Min(ax, bx) - tolerance &&
               qx <= Math.Max(ax, bx) + tolerance &&
               qy >= Math.Min(ay, by) - tolerance &&
               qy <= Math.Max(ay, by) + tolerance;
    }
}

public sealed record ContourPlaneArea(double ZMm, double AreaMm2);

public sealed record ContourInterplaneVolumeResult(
    bool Available,
    double? InterplaneVolumeCc,
    IReadOnlyList<ContourPlaneArea> PlaneAreas,
    string? UnavailableReason);
