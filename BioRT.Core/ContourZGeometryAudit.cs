using BioRT.Core.Models;

namespace BioRT.Core.DVH;

/// <summary>
/// Non-destructive QA of the current nearest-dose-plane mapping.
/// It does not resample contours, modify DVH masks or validate a TPS.
/// </summary>
public static class ContourZGeometryAudit
{
    private const double PlaneToleranceMm = 1e-3;

    public static ContourZGeometryReport Analyze(
        IReadOnlyList<double[]> contours,
        DoseVolume dose)
    {
        ArgumentNullException.ThrowIfNull(contours);
        ArgumentNullException.ThrowIfNull(dose);

        if (contours.Count == 0)
            throw new ArgumentException("ROI has no contours.", nameof(contours));

        double[] frames = dose.ZPositions;
        if (frames == null ||
            frames.Length != dose.SizeZ ||
            frames.Length == 0 ||
            frames.Any(z => !double.IsFinite(z)))
        {
            throw new ArgumentException(
                "RTDOSE Z positions do not match the number of dose frames.",
                nameof(dose));
        }

        if (!double.IsFinite(dose.SpacingZ) || dose.SpacingZ <= 0)
            throw new ArgumentException("RTDOSE has invalid Z spacing.", nameof(dose));

        if (frames.Length > 1)
        {
            double firstStep = frames[1] - frames[0];

            if (Math.Abs(firstStep) < PlaneToleranceMm)
                throw new ArgumentException("Duplicate RTDOSE frame Z positions.", nameof(dose));

            for (int i = 2; i < frames.Length; i++)
            {
                double step = frames[i] - frames[i - 1];
                if (Math.Sign(step) != Math.Sign(firstStep) ||
                    Math.Abs(step - firstStep) > PlaneToleranceMm)
                {
                    throw new ArgumentException(
                        "Irregular or nonmonotonic RTDOSE frame Z positions.",
                        nameof(dose));
                }
            }
        }

        var orderedZ = new List<double>(contours.Count);

        foreach (double[] polygon in contours)
        {
            if (polygon == null ||
                polygon.Length < 9 ||
                polygon.Length % 3 != 0 ||
                polygon.Any(v => !double.IsFinite(v)))
            {
                throw new ArgumentException(
                    "ROI has invalid contour coordinates.", nameof(contours));
            }

            double z = polygon[2];
            for (int i = 5; i < polygon.Length; i += 3)
            {
                if (Math.Abs(polygon[i] - z) > PlaneToleranceMm)
                    throw new ArgumentException(
                        "Non-axial ROI contour cannot be audited by this axial Z tool.",
                        nameof(contours));
            }

            orderedZ.Add(z);
        }

        orderedZ.Sort();
        var uniquePlanes = new List<(double Z, int Count)>();

        foreach (double z in orderedZ)
        {
            if (uniquePlanes.Count == 0 ||
                Math.Abs(z - uniquePlanes[^1].Z) > PlaneToleranceMm)
            {
                uniquePlanes.Add((z, 1));
            }
            else
            {
                var last = uniquePlanes[^1];
                uniquePlanes[^1] = (last.Z, last.Count + 1);
            }
        }

        double minFrameZ = frames.Min();
        double maxFrameZ = frames.Max();
        bool knownSlabThickness = frames.Length > 1;

        var mapping = new List<ContourPlaneMapping>(uniquePlanes.Count);
        var frameCounts = new Dictionary<int, int>();

        foreach ((double z, int count) in uniquePlanes)
        {
            int nearest = 0;
            double distance = Math.Abs(frames[0] - z);

            for (int i = 1; i < frames.Length; i++)
            {
                double candidate = Math.Abs(frames[i] - z);
                if (candidate < distance)
                {
                    nearest = i;
                    distance = candidate;
                }
            }

            bool outside =
                knownSlabThickness &&
                (z < minFrameZ - dose.SpacingZ / 2.0 - PlaneToleranceMm ||
                 z > maxFrameZ + dose.SpacingZ / 2.0 + PlaneToleranceMm);

            mapping.Add(new ContourPlaneMapping(
                z, count, nearest, frames[nearest], distance, outside));

            frameCounts.TryGetValue(nearest, out int previous);
            frameCounts[nearest] = previous + 1;
        }

        return new ContourZGeometryReport(
            contours.Count,
            mapping,
            knownSlabThickness,
            mapping.Count(m => m.OutsideDoseExtent),
            frameCounts.Count(pair => pair.Value > 1),
            mapping.Max(m => m.NearestDoseDistanceMm));
    }
}

public sealed record ContourPlaneMapping(
    double ContourZMm,
    int ContourCount,
    int NearestFrameIndex,
    double DoseFrameZMm,
    double NearestDoseDistanceMm,
    bool OutsideDoseExtent);

public sealed record ContourZGeometryReport(
    int TotalContours,
    IReadOnlyList<ContourPlaneMapping> Planes,
    bool HasKnownDoseSlabThickness,
    int OutsideDosePlaneCount,
    int CollidingDoseFrameCount,
    double MaximumNearestDoseDistanceMm)
{
    public int UniqueContourPlaneCount => Planes.Count;

    // This means no detected mapping collision or out-of-extent plane.
    // It does NOT demonstrate contour-to-dose geometry equivalence.
    public bool NoDetectedZMappingIssues =>
        HasKnownDoseSlabThickness &&
        OutsideDosePlaneCount == 0 &&
        CollidingDoseFrameCount == 0;
}
