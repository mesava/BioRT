using BioRT.Core.Models;

namespace BioRT.Core.DVH;

/// <summary>
/// Research-only, memory-bounded (no full 3D masks) sampler for the
/// Stanley et al. spherical SRS DICOM benchmark, DOI 10.1002/mp.14645.
///
/// Uses the SAME nearest-RTDOSE-frame, voxel-centre, ray-casting and
/// per-frame union/XOR conventions as the current MaskBuilder.
/// It is an internal numerical comparator, NOT clinical acceptance
/// or a replacement for anatomical registration/3D rasterization.
/// </summary>
public static class StanleySphereBenchmark
{
    public static StanleySphereBenchmarkResult Evaluate(
        DoseVolume dose,
        IReadOnlyDictionary<int, string> roiNames,
        IReadOnlyDictionary<int, RoiContours> rois,
        double nominalSphereDiameterMm)
    {
        ArgumentNullException.ThrowIfNull(dose);
        ArgumentNullException.ThrowIfNull(roiNames);
        ArgumentNullException.ThrowIfNull(rois);

        if (!double.IsFinite(nominalSphereDiameterMm) ||
            nominalSphereDiameterMm <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nominalSphereDiameterMm));
        }

        if (dose.Dose is null ||
            dose.SizeX == 0 || dose.SizeY == 0 || dose.SizeZ == 0 ||
            dose.ZPositions is null ||
            dose.ZPositions.Length != dose.SizeZ ||
            dose.SpacingX <= 0 || dose.SpacingY <= 0 ||
            dose.SpacingZ <= 0 ||
            !double.IsFinite(dose.SpacingX) ||
            !double.IsFinite(dose.SpacingY) ||
            !double.IsFinite(dose.SpacingZ))
        {
            throw new ArgumentException(
                "Benchmark requires a valid axial RTDOSE grid.", nameof(dose));
        }

        var byName = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<int, string> pair in roiNames)
        {
            string name = pair.Value.Trim();

            if (!byName.TryAdd(name, pair.Key))
            {
                throw new InvalidOperationException(
                    $"Ambiguous duplicate structure name: {name}.");
            }
        }

        string[] targets = byName.Keys
            .Where(IsSphereTarget)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        if (targets.Length == 0)
        {
            throw new InvalidOperationException(
                "No GTV01-style Stanley sphere targets were found.");
        }

        double voxelVolumeCc =
            dose.SpacingX * dose.SpacingY * dose.SpacingZ / 1000.0;

        double exactSphereVolumeCc =
            Math.PI * Math.Pow(nominalSphereDiameterMm, 3) / 6000.0;

        var results = new List<StanleySphereBenchmarkRow>(targets.Length);

        foreach (string targetName in targets)
        {
            string evaluationName = targetName + "_SHELL";

            if (!byName.TryGetValue(
                    evaluationName, out int shellRoiNumber))
            {
                throw new InvalidOperationException(
                    $"Missing evaluation shell for {targetName}.");
            }

            int targetNumber = byName[targetName];

            if (!rois.TryGetValue(
                    targetNumber, out RoiContours? targetRoi) ||
                !rois.TryGetValue(
                    shellRoiNumber, out RoiContours? shellRoi))
            {
                throw new InvalidOperationException(
                    $"Missing contour geometry for {targetName} " +
                    $"or {evaluationName}.");
            }

            SampleStats target = Sample(
                dose, targetRoi, voxelVolumeCc);
            SampleStats shell = Sample(
                dose, shellRoi, voxelVolumeCc);

            if (target.Voxels == 0 || shell.Voxels == 0)
            {
                throw new InvalidOperationException(
                    $"Empty sampled target or shell for {targetName}.");
            }

            double targetVolume = target.Voxels * voxelVolumeCc;
            double shellV20 = shell.V20Voxels * voxelVolumeCc;
            double shellV10 = shell.V10Voxels * voxelVolumeCc;

            results.Add(new StanleySphereBenchmarkRow(
                targetName,
                evaluationName,
                exactSphereVolumeCc,
                targetVolume,
                100.0 * (targetVolume / exactSphereVolumeCc - 1.0),
                shell.Voxels * voxelVolumeCc,
                shellV20,
                shellV10,
                target.SumDoseGy / target.Voxels,
                target.MaxDoseGy,
                shellV20 / targetVolume,
                shellV20 > 0 ? shellV10 / shellV20 : null));
        }

        return new StanleySphereBenchmarkResult(
            nominalSphereDiameterMm,
            voxelVolumeCc,
            dose.SizeX, dose.SizeY, dose.SizeZ,
            results);
    }

    private static bool IsSphereTarget(string name)
        => name.Length == 5 &&
           name.StartsWith("GTV", StringComparison.OrdinalIgnoreCase) &&
           char.IsDigit(name[3]) &&
           char.IsDigit(name[4]);

    private static SampleStats Sample(
        DoseVolume dose,
        RoiContours roi,
        double voxelVolumeCc)
    {
        if (!Enum.IsDefined(roi.FillRule))
            throw new InvalidOperationException("Unknown ROI fill rule.");

        var polygonsByFrame = new Dictionary<int, List<double[]>>();
        int nx = dose.SizeX;
        int ny = dose.SizeY;

        foreach (double[] polygon in roi.Polygons)
        {
            if (polygon is null ||
                polygon.Length < 9 ||
                polygon.Length % 3 != 0 ||
                polygon.Any(v => !double.IsFinite(v)))
            {
                throw new InvalidOperationException(
                    "Malformed ROI polygon in Stanley benchmark.");
            }

            double z = polygon[2];
            for (int point = 1; point < polygon.Length / 3; point++)
            {
                if (Math.Abs(polygon[3 * point + 2] - z) > 1e-3)
                {
                    throw new NotSupportedException(
                        "Non-axial contours are not supported.");
                }
            }

            int nearest = 0;
            double distance = Math.Abs(dose.ZPositions[0] - z);

            for (int k = 1; k < dose.SizeZ; k++)
            {
                double candidate = Math.Abs(dose.ZPositions[k] - z);
                if (candidate < distance)
                {
                    distance = candidate;
                    nearest = k;
                }
            }

            if (roi.FillRule == RoiContourFillRule.Xor &&
                polygonsByFrame.TryGetValue(
                    nearest, out List<double[]>? existing) &&
                existing.Count > 0 &&
                Math.Abs(existing[0][2] - z) > 1e-3)
            {
                throw new NotSupportedException(
                    "XOR contours from distinct Z planes mapped to " +
                    "the same RTDOSE frame.");
            }

            if (!polygonsByFrame.TryGetValue(
                    nearest, out List<double[]>? group))
            {
                group = [];
                polygonsByFrame.Add(nearest, group);
            }

            group.Add(polygon);
        }

        long selected = 0, above10 = 0, above20 = 0;
        double sum = 0.0, max = double.NegativeInfinity;

        foreach ((int k, List<double[]> polygons) in polygonsByFrame)
        {
            double minX = polygons.Min(p =>
                Enumerable.Range(0, p.Length / 3)
                    .Min(i => p[3 * i]));
            double maxX = polygons.Max(p =>
                Enumerable.Range(0, p.Length / 3)
                    .Max(i => p[3 * i]));
            double minY = polygons.Min(p =>
                Enumerable.Range(0, p.Length / 3)
                    .Min(i => p[3 * i + 1]));
            double maxY = polygons.Max(p =>
                Enumerable.Range(0, p.Length / 3)
                    .Max(i => p[3 * i + 1]));

            int firstX = (int)Math.Max(0,
                Math.Ceiling((minX - dose.OriginX) / dose.SpacingX));
            int lastX = (int)Math.Min(nx - 1,
                Math.Floor((maxX - dose.OriginX) / dose.SpacingX));
            int firstY = (int)Math.Max(0,
                Math.Ceiling((minY - dose.OriginY) / dose.SpacingY));
            int lastY = (int)Math.Min(ny - 1,
                Math.Floor((maxY - dose.OriginY) / dose.SpacingY));

            if (firstX > lastX || firstY > lastY)
                continue;

            for (int j = firstY; j <= lastY; j++)
            for (int i = firstX; i <= lastX; i++)
            {
                double x = dose.OriginX + i * dose.SpacingX;
                double y = dose.OriginY + j * dose.SpacingY;
                bool inside = false;

                foreach (double[] polygon in polygons)
                {
                    if (!PointInPolygon(x, y, polygon))
                        continue;

                    if (roi.FillRule == RoiContourFillRule.Xor)
                        inside = !inside;
                    else
                    {
                        inside = true;
                        break;
                    }
                }

                if (!inside) continue;

                double doseGy = dose.Dose[i, j, k];

                if (!double.IsFinite(doseGy))
                {
                    throw new InvalidOperationException(
                        "Non-finite RTDOSE sample in sphere benchmark.");
                }

                selected++;
                sum += doseGy;
                max = Math.Max(max, doseGy);

                if (doseGy >= 10.0) above10++;
                if (doseGy >= 20.0) above20++;
            }
        }

        return new SampleStats(
            selected, above10, above20, sum, max);
    }

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

            bool intersection =
                ((yi > y) != (yj > y)) &&
                (x < (xj - xi) * (y - yi) / (yj - yi) + xi);

            if (intersection)
                inside = !inside;
        }

        return inside;
    }

    private sealed record SampleStats(
        long Voxels,
        long V10Voxels,
        long V20Voxels,
        double SumDoseGy,
        double MaxDoseGy);
}

public sealed record StanleySphereBenchmarkRow(
    string Target,
    string EvaluationShell,
    double AnalyticalSphereVolumeCc,
    double SampledTargetVolumeCc,
    double TargetVolumeDifferencePercent,
    double SampledShellVolumeCc,
    double V20GyCc,
    double V10GyCc,
    double TargetMeanDoseGy,
    double TargetMaximumDoseGy,
    double RtOGConformityIndex,
    double? PaddickGradientIndex);

public sealed record StanleySphereBenchmarkResult(
    double SphereDiameterMm,
    double VoxelVolumeCc,
    int GridColumns,
    int GridRows,
    int GridFrames,
    IReadOnlyList<StanleySphereBenchmarkRow> Spheres)
{
    public int SphereCount => Spheres.Count;
    public double MeanTargetVolumeDifferencePercent =>
        Spheres.Average(x => x.TargetVolumeDifferencePercent);
}
