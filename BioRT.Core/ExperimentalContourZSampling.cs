using BioRT.Core.Models;

namespace BioRT.Core.DVH;

/// <summary>
/// Research-only numerical comparison of RTSTRUCT contour-Z sampling
/// conventions. Nothing here changes the clinical DVH, TCP/NTCP or
/// PlanAnalysisFingerprint. No three-dimensional contour morphing or
/// dose interpolation is performed.
/// </summary>
public static class ExperimentalContourZSampling
{
    private const double PlaneToleranceMm = 1e-3;
    private const long MaximumBenchmarkVoxels = 5_000_000L;

    public static ZSamplingComparison Compare(
        RoiContours roi,
        DoseVolume dose,
        double thresholdGy,
        ZEndCapPolicy endCaps)
    {
        ArgumentNullException.ThrowIfNull(roi);
        ArgumentNullException.ThrowIfNull(dose);

        if (!double.IsFinite(thresholdGy) || thresholdGy < 0.0)
            throw new ArgumentOutOfRangeException(nameof(thresholdGy));

        if (!Enum.IsDefined(endCaps))
            throw new ArgumentOutOfRangeException(nameof(endCaps));

        ValidateDose(dose);

        long totalVoxels =
            (long)dose.SizeX * dose.SizeY * dose.SizeZ;

        if (totalVoxels > MaximumBenchmarkVoxels)
        {
            throw new NotSupportedException(
                "Experimental Z-sampling benchmark exceeds the safe " +
                "5-million-voxel limit. Use a smaller synthetic grid.");
        }

        List<ContourPlane> planes = GroupContourPlanes(roi);

        if (planes.Count < 2)
        {
            throw new NotSupportedException(
                "Experimental Z-sampling requires at least two " +
                "distinct RTSTRUCT contour planes.");
        }

        // The legacy mask is the real current implementation, not a
        // separately reimplemented approximation.
        StructureMask legacyMask = MaskBuilder.BuildMask(
            "Z-sampling benchmark (legacy)",
            roi.Polygons,
            dose,
            roi.FillRule);

        var legacy = new WeightedAccumulation();
        var nearest = new WeightedAccumulation();
        var linear = new WeightedAccumulation();

        double minZ = planes[0].Z;
        double maxZ = planes[^1].Z;
        double endExtension =
            endCaps == ZEndCapPolicy.ExtendHalfDoseFrame
                ? dose.SpacingZ / 2.0
                : 0.0;

        for (int k = 0; k < dose.SizeZ; k++)
        {
            double z = dose.ZPositions[k];
            bool inContourExtent =
                z >= minZ - endExtension - 1e-9 &&
                z <= maxZ + endExtension + 1e-9;

            int lower = 0;
            int upper = 0;
            double t = 0.0;

            if (inContourExtent)
            {
                if (z <= minZ)
                {
                    lower = upper = 0;
                }
                else if (z >= maxZ)
                {
                    lower = upper = planes.Count - 1;
                }
                else
                {
                    while (lower + 1 < planes.Count &&
                           planes[lower + 1].Z < z)
                    {
                        lower++;
                    }

                    upper = lower + 1;
                    double delta = planes[upper].Z - planes[lower].Z;
                    t = (z - planes[lower].Z) / delta;
                }
            }

            for (int y = 0; y < dose.SizeY; y++)
            for (int x = 0; x < dose.SizeX; x++)
            {
                double physicalDose = dose.Dose[x, y, k];

                if (!double.IsFinite(physicalDose))
                    throw new InvalidOperationException(
                        "Benchmark dose contains a non-finite voxel.");

                legacy.Add(
                    legacyMask.Mask[x, y, k] ? 1.0 : 0.0,
                    physicalDose,
                    thresholdGy);

                if (!inContourExtent)
                    continue;

                double patientX =
                    dose.OriginX + x * dose.SpacingX;
                double patientY =
                    dose.OriginY + y * dose.SpacingY;

                double lowerInside = IsInside(
                    patientX, patientY, planes[lower], roi.FillRule)
                    ? 1.0 : 0.0;

                double upperInside =
                    upper == lower
                        ? lowerInside
                        : IsInside(
                            patientX, patientY, planes[upper], roi.FillRule)
                            ? 1.0 : 0.0;

                int selected =
                    upper == lower || t <= 0.5
                        ? lower
                        : upper;

                double nearestWeight =
                    selected == lower
                        ? lowerInside
                        : upperInside;

                // This is linear mixing of *binary occupancy*, not
                // contour/surface interpolation. It deliberately
                // permits fractional effective voxels for QA only.
                double fractionalWeight =
                    (1.0 - t) * lowerInside + t * upperInside;

                nearest.Add(
                    nearestWeight, physicalDose, thresholdGy);
                linear.Add(
                    fractionalWeight, physicalDose, thresholdGy);
            }
        }

        double voxelCc =
            dose.SpacingX * dose.SpacingY * dose.SpacingZ / 1000.0;

        return new ZSamplingComparison(
            endCaps,
            thresholdGy,
            planes.Count,
            legacy.Summary(
                ZSamplingMethod.LegacyNearestDoseFrame, voxelCc),
            nearest.Summary(
                ZSamplingMethod.NearestContourPlane, voxelCc),
            linear.Summary(
                ZSamplingMethod.LinearBinaryOccupancy, voxelCc));
    }

    private static void ValidateDose(DoseVolume dose)
    {
        if (dose.Dose == null ||
            dose.SizeX <= 0 || dose.SizeY <= 0 || dose.SizeZ < 2 ||
            dose.ZPositions == null ||
            dose.ZPositions.Length != dose.SizeZ ||
            !double.IsFinite(dose.SpacingX) || dose.SpacingX <= 0 ||
            !double.IsFinite(dose.SpacingY) || dose.SpacingY <= 0 ||
            !double.IsFinite(dose.SpacingZ) || dose.SpacingZ <= 0)
        {
            throw new InvalidOperationException(
                "Benchmark requires a non-empty dose grid with " +
                "positive spacing and at least two Z frames.");
        }

        double firstStep =
            dose.ZPositions[1] - dose.ZPositions[0];

        if (!double.IsFinite(firstStep) ||
            Math.Abs(firstStep) <= PlaneToleranceMm ||
            Math.Abs(Math.Abs(firstStep) - dose.SpacingZ) >
            PlaneToleranceMm)
        {
            throw new InvalidOperationException(
                "Dose Z steps and SpacingZ are inconsistent.");
        }

        for (int k = 1; k < dose.SizeZ; k++)
        {
            double step = dose.ZPositions[k] - dose.ZPositions[k - 1];

            if (!double.IsFinite(step) ||
                Math.Abs(step - firstStep) > PlaneToleranceMm)
            {
                throw new NotSupportedException(
                    "Irregular or nonmonotonic dose Z frames are unsupported.");
            }
        }
    }

    private static List<ContourPlane> GroupContourPlanes(RoiContours roi)
    {
        if (roi.Polygons.Count == 0)
            throw new InvalidOperationException("ROI has no polygons.");

        var ordered = new List<(double Z, double[] Points)>();

        foreach (double[] polygon in roi.Polygons)
        {
            if (polygon == null ||
                polygon.Length < 9 ||
                polygon.Length % 3 != 0 ||
                polygon.Any(v => !double.IsFinite(v)))
            {
                throw new InvalidOperationException(
                    "ROI contains invalid contour vertices.");
            }

            double z = polygon[2];

            for (int i = 5; i < polygon.Length; i += 3)
            {
                if (Math.Abs(polygon[i] - z) > PlaneToleranceMm)
                    throw new NotSupportedException(
                        "Non-axial contour planes cannot be benchmarked.");
            }

            ordered.Add((z, polygon));
        }

        ordered.Sort((a, b) => a.Z.CompareTo(b.Z));
        var planes = new List<ContourPlane>();

        foreach ((double z, double[] polygon) in ordered)
        {
            if (planes.Count == 0 ||
                Math.Abs(z - planes[^1].Z) > PlaneToleranceMm)
            {
                planes.Add(new ContourPlane(z));
            }

            planes[^1].Polygons.Add(polygon);
        }

        return planes;
    }

    private static bool IsInside(
        double x,
        double y,
        ContourPlane plane,
        RoiContourFillRule fillRule)
    {
        bool inside = false;

        foreach (double[] polygon in plane.Polygons)
        {
            if (!PointInPolygon(x, y, polygon))
                continue;

            if (fillRule == RoiContourFillRule.Xor)
            {
                inside = !inside;
            }
            else
            {
                return true;
            }
        }

        return inside;
    }

    // Same ray-casting centre-in-polygon convention as current
    // MaskBuilder, deliberately local to this experimental benchmark.
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

    private sealed class ContourPlane(double z)
    {
        public double Z { get; } = z;
        public List<double[]> Polygons { get; } = [];
    }

    private sealed class WeightedAccumulation
    {
        private double _effectiveVoxels;
        private double _doseSum;
        private double _thresholdVoxels;

        public void Add(double fraction, double doseGy, double thresholdGy)
        {
            if (fraction <= 0.0)
                return;

            _effectiveVoxels += fraction;
            _doseSum += fraction * doseGy;

            if (doseGy >= thresholdGy)
                _thresholdVoxels += fraction;
        }

        public ZSamplingMetrics Summary(
            ZSamplingMethod method,
            double voxelVolumeCc)
            => new(
                method,
                _effectiveVoxels,
                _effectiveVoxels * voxelVolumeCc,
                _effectiveVoxels > 0.0
                    ? _doseSum / _effectiveVoxels
                    : null,
                _effectiveVoxels > 0.0
                    ? 100.0 * _thresholdVoxels / _effectiveVoxels
                    : null);
    }
}

/// <summary>
/// Explicitly defines whether a synthetic contour end-plane is assumed
/// to cover an additional half dose-frame beyond its Z location.
/// The correct convention for clinical RTSTRUCT is NOT inferred.
/// </summary>
public enum ZEndCapPolicy
{
    NoExtrapolation = 0,
    ExtendHalfDoseFrame = 1
}

public enum ZSamplingMethod
{
    LegacyNearestDoseFrame,
    NearestContourPlane,
    LinearBinaryOccupancy
}

public sealed record ZSamplingMetrics(
    ZSamplingMethod Method,
    double EffectiveVoxels,
    double VolumeCc,
    double? MeanDoseGy,
    double? VThresholdPercent);

public sealed record ZSamplingComparison(
    ZEndCapPolicy EndCaps,
    double ThresholdGy,
    int SourceContourPlaneCount,
    ZSamplingMetrics Legacy,
    ZSamplingMetrics NearestContour,
    ZSamplingMetrics LinearOccupancy);
