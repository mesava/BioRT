using System.Globalization;
using BioRT.Core.Models;

namespace BioRT.Core.Analysis;

public sealed class PlanAnalysisToleranceOptions
{
    /// <summary>
    /// Absolute dose tolerance used for Gy-valued quantities.
    /// Defaults are intentionally numerical-regression tolerances, not clinical tolerances.
    /// </summary>
    public double DoseAbsoluteGy { get; init; } = 1e-6;

    public double DoseRelative { get; init; } = 1e-9;

    public double VolumeAbsoluteCc { get; init; } = 1e-6;

    public double VolumeRelative { get; init; } = 1e-9;

    public double GeometryAbsoluteMm { get; init; } = 1e-6;

    public double DimensionlessAbsolute { get; init; } = 1e-9;

    public double ProbabilityAbsolute { get; init; } = 1e-10;

    public double DvhVolumeAbsolutePercent { get; init; } = 1e-8;

    internal void Validate()
    {
        foreach (double value in new[]
                 {
                     DoseAbsoluteGy,
                     DoseRelative,
                     VolumeAbsoluteCc,
                     VolumeRelative,
                     GeometryAbsoluteMm,
                     DimensionlessAbsolute,
                     ProbabilityAbsolute,
                     DvhVolumeAbsolutePercent
                 })
        {
            if (!double.IsFinite(value) || value < 0.0)
                throw new ArgumentOutOfRangeException(nameof(PlanAnalysisToleranceOptions));
        }
    }
}

public sealed record PlanAnalysisDifference(
    string Path,
    string Baseline,
    string Candidate,
    string Rule);

public sealed class PlanAnalysisComparisonResult
{
    public required IReadOnlyList<PlanAnalysisDifference> Differences { get; init; }

    public bool IsMatch => Differences.Count == 0;
}

/// <summary>
/// Tolerance-based scientific regression comparison for completed BioRT analyses.
///
/// This complements, rather than replaces, the exact SHA-256 commissioning fingerprint.
/// The default tolerances are deliberately tiny numerical tolerances intended to absorb
/// harmless floating-point noise. They are not claims of clinical equivalence.
/// </summary>
public static class PlanAnalysisComparator
{
    public static PlanAnalysisComparisonResult Compare(
        PlanData baselinePlan,
        PlanAnalysisResult baseline,
        PlanData candidatePlan,
        PlanAnalysisResult candidate,
        PlanAnalysisToleranceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(baselinePlan);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(candidatePlan);
        ArgumentNullException.ThrowIfNull(candidate);

        options ??= new PlanAnalysisToleranceOptions();
        options.Validate();

        var differences = new List<PlanAnalysisDifference>();

        CompareExact(differences, "plan.fractions", baselinePlan.Fractions, candidatePlan.Fractions);
        CompareNumber(
            differences,
            "plan.dose_per_fraction_gy",
            baselinePlan.DosePerFraction,
            candidatePlan.DosePerFraction,
            options.DoseAbsoluteGy,
            options.DoseRelative);
        CompareNumber(
            differences,
            "plan.total_dose_gy",
            baselinePlan.TotalDose,
            candidatePlan.TotalDose,
            options.DoseAbsoluteGy,
            options.DoseRelative);

        CompareDoseGeometry(differences, baselinePlan.Dose, candidatePlan.Dose, options);
        CompareStructures(differences, baseline.Structures, candidate.Structures, options);
        ComparePtv(differences, baseline.PtvMetrics, candidate.PtvMetrics, options);
        CompareCriteria(differences, baseline.ClinicalCriteria, candidate.ClinicalCriteria, options);
        CompareNtcp(differences, baseline.Ntcp, candidate.Ntcp, options);
        CompareTcp(differences, baseline.Tcp, candidate.Tcp, options);
        CompareStringSequence(
            differences,
            "warnings",
            baseline.Warnings.OrderBy(x => x, StringComparer.Ordinal),
            candidate.Warnings.OrderBy(x => x, StringComparer.Ordinal));

        return new PlanAnalysisComparisonResult
        {
            Differences = differences
        };
    }

    private static void CompareDoseGeometry(
        List<PlanAnalysisDifference> differences,
        DoseVolume? baseline,
        DoseVolume? candidate,
        PlanAnalysisToleranceOptions options)
    {
        if (baseline is null || candidate is null)
        {
            if ((baseline is null) != (candidate is null))
                Add(differences, "dose_grid", baseline is null ? "null" : "present", candidate is null ? "null" : "present", "exact");

            return;
        }

        CompareExact(differences, "dose_grid.size_x", baseline.SizeX, candidate.SizeX);
        CompareExact(differences, "dose_grid.size_y", baseline.SizeY, candidate.SizeY);
        CompareExact(differences, "dose_grid.size_z", baseline.SizeZ, candidate.SizeZ);

        CompareAbsolute(differences, "dose_grid.spacing_x_mm", baseline.SpacingX, candidate.SpacingX, options.GeometryAbsoluteMm);
        CompareAbsolute(differences, "dose_grid.spacing_y_mm", baseline.SpacingY, candidate.SpacingY, options.GeometryAbsoluteMm);
        CompareAbsolute(differences, "dose_grid.spacing_z_mm", baseline.SpacingZ, candidate.SpacingZ, options.GeometryAbsoluteMm);
        CompareAbsolute(differences, "dose_grid.origin_x_mm", baseline.OriginX, candidate.OriginX, options.GeometryAbsoluteMm);
        CompareAbsolute(differences, "dose_grid.origin_y_mm", baseline.OriginY, candidate.OriginY, options.GeometryAbsoluteMm);
        CompareAbsolute(differences, "dose_grid.origin_z_mm", baseline.OriginZ, candidate.OriginZ, options.GeometryAbsoluteMm);

        CompareExact(differences, "dose_grid.z_positions.count", baseline.ZPositions.Length, candidate.ZPositions.Length);

        int zCount = Math.Min(baseline.ZPositions.Length, candidate.ZPositions.Length);
        for (int i = 0; i < zCount; i++)
        {
            CompareAbsolute(
                differences,
                $"dose_grid.z_positions[{i}]_mm",
                baseline.ZPositions[i],
                candidate.ZPositions[i],
                options.GeometryAbsoluteMm);
        }
    }

    private static void CompareStructures(
        List<PlanAnalysisDifference> differences,
        IReadOnlyList<StructureAnalysisResult> baseline,
        IReadOnlyList<StructureAnalysisResult> candidate,
        PlanAnalysisToleranceOptions options)
    {
        var left = baseline.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var right = candidate.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();

        CompareExact(differences, "structures.count", left.Length, right.Length);

        int count = Math.Min(left.Length, right.Length);
        for (int i = 0; i < count; i++)
        {
            string path = $"structures[{i}]";
            CompareText(differences, $"{path}.name", left[i].Name, right[i].Name, ignoreCase: true);

            CompareNumber(
                differences,
                $"{path}.volume_cc",
                left[i].VolumeCc,
                right[i].VolumeCc,
                options.VolumeAbsoluteCc,
                options.VolumeRelative);

            CompareNumber(
                differences,
                $"{path}.mean_dose_gy",
                left[i].Dvh.MeanDose,
                right[i].Dvh.MeanDose,
                options.DoseAbsoluteGy,
                options.DoseRelative);

            CompareNumber(
                differences,
                $"{path}.max_dose_gy",
                left[i].Dvh.MaxDose,
                right[i].Dvh.MaxDose,
                options.DoseAbsoluteGy,
                options.DoseRelative);

            CompareExact(differences, $"{path}.dvh.count", left[i].Dvh.Dose.Length, right[i].Dvh.Dose.Length);

            int dvhCount = Math.Min(left[i].Dvh.Dose.Length, right[i].Dvh.Dose.Length);
            for (int j = 0; j < dvhCount; j++)
            {
                CompareNumber(
                    differences,
                    $"{path}.dvh[{j}].dose_gy",
                    left[i].Dvh.Dose[j],
                    right[i].Dvh.Dose[j],
                    options.DoseAbsoluteGy,
                    options.DoseRelative);

                CompareAbsolute(
                    differences,
                    $"{path}.dvh[{j}].volume_percent",
                    left[i].Dvh.Volume[j],
                    right[i].Dvh.Volume[j],
                    options.DvhVolumeAbsolutePercent);
            }
        }
    }

    private static void ComparePtv(
        List<PlanAnalysisDifference> differences,
        IReadOnlyList<PtvAnalysisResult> baseline,
        IReadOnlyList<PtvAnalysisResult> candidate,
        PlanAnalysisToleranceOptions options)
    {
        var left = baseline
            .OrderBy(x => x.CriterionStructureName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.MatchedStructureName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var right = candidate
            .OrderBy(x => x.CriterionStructureName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.MatchedStructureName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        CompareExact(differences, "ptv.count", left.Length, right.Length);

        int count = Math.Min(left.Length, right.Length);
        for (int i = 0; i < count; i++)
        {
            string path = $"ptv[{i}]";
            CompareText(differences, $"{path}.criterion_structure", left[i].CriterionStructureName, right[i].CriterionStructureName, true);
            CompareText(differences, $"{path}.matched_structure", left[i].MatchedStructureName, right[i].MatchedStructureName, true);
            CompareNumber(differences, $"{path}.rx_gy", left[i].PrescriptionDoseGy, right[i].PrescriptionDoseGy, options.DoseAbsoluteGy, options.DoseRelative);
            CompareNumber(differences, $"{path}.volume_cc", left[i].VolumeCc, right[i].VolumeCc, options.VolumeAbsoluteCc, options.VolumeRelative);
            CompareNumber(differences, $"{path}.d2_gy", left[i].D2Gy, right[i].D2Gy, options.DoseAbsoluteGy, options.DoseRelative);
            CompareNumber(differences, $"{path}.d98_gy", left[i].D98Gy, right[i].D98Gy, options.DoseAbsoluteGy, options.DoseRelative);
            CompareNumber(differences, $"{path}.d95_gy", left[i].D95Gy, right[i].D95Gy, options.DoseAbsoluteGy, options.DoseRelative);
            CompareNumber(differences, $"{path}.d50_gy", left[i].D50Gy, right[i].D50Gy, options.DoseAbsoluteGy, options.DoseRelative);
            CompareAbsolute(differences, $"{path}.hi", left[i].Hi, right[i].Hi, options.DimensionlessAbsolute);
            CompareAbsolute(differences, $"{path}.ci", left[i].Ci, right[i].Ci, options.DimensionlessAbsolute);
            CompareAbsolute(differences, $"{path}.gi", left[i].Gi, right[i].Gi, options.DimensionlessAbsolute);
        }
    }

    private static void CompareCriteria(
        List<PlanAnalysisDifference> differences,
        IReadOnlyList<ClinicalCriterionEvaluation> baseline,
        IReadOnlyList<ClinicalCriterionEvaluation> candidate,
        PlanAnalysisToleranceOptions options)
    {
        var left = baseline
            .OrderBy(x => x.MatchedStructureName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Criterion.Raw, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var right = candidate
            .OrderBy(x => x.MatchedStructureName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Criterion.Raw, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        CompareExact(differences, "criteria.count", left.Length, right.Length);

        int count = Math.Min(left.Length, right.Length);
        for (int i = 0; i < count; i++)
        {
            string path = $"criteria[{i}]";
            CompareText(differences, $"{path}.criterion_structure", left[i].CriterionStructureName, right[i].CriterionStructureName, true);
            CompareText(differences, $"{path}.matched_structure", left[i].MatchedStructureName, right[i].MatchedStructureName, true);
            CompareText(differences, $"{path}.raw", left[i].Criterion.Raw, right[i].Criterion.Raw, false);
            CompareExact(differences, $"{path}.type", left[i].Criterion.Type, right[i].Criterion.Type);
            CompareText(differences, $"{path}.operator", left[i].Criterion.Operator, right[i].Criterion.Operator, false);
            CompareCriterionValue(differences, $"{path}.value", left[i], right[i], options);
            CompareExact(differences, $"{path}.pass", left[i].Pass, right[i].Pass);
        }
    }

    private static void CompareCriterionValue(
        List<PlanAnalysisDifference> differences,
        string path,
        ClinicalCriterionEvaluation baseline,
        ClinicalCriterionEvaluation candidate,
        PlanAnalysisToleranceOptions options)
    {
        if (baseline.Criterion.Type != candidate.Criterion.Type)
            return;

        switch (baseline.Criterion.Type)
        {
            case CriterionType.VxxGyPercent:
                CompareAbsolute(
                    differences,
                    path,
                    baseline.Value,
                    candidate.Value,
                    options.DvhVolumeAbsolutePercent);
                break;

            case CriterionType.VxxGyCc:
                CompareNumber(
                    differences,
                    path,
                    baseline.Value,
                    candidate.Value,
                    options.VolumeAbsoluteCc,
                    options.VolumeRelative);
                break;

            default:
                CompareNumber(
                    differences,
                    path,
                    baseline.Value,
                    candidate.Value,
                    options.DoseAbsoluteGy,
                    options.DoseRelative);
                break;
        }
    }

    private static void CompareNtcp(
        List<PlanAnalysisDifference> differences,
        IReadOnlyList<NtcpAnalysisResult> baseline,
        IReadOnlyList<NtcpAnalysisResult> candidate,
        PlanAnalysisToleranceOptions options)
    {
        var left = baseline
            .OrderBy(x => x.StructureName ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Result.ModelId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var right = candidate
            .OrderBy(x => x.StructureName ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Result.ModelId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        CompareExact(differences, "ntcp.count", left.Length, right.Length);

        int count = Math.Min(left.Length, right.Length);
        for (int i = 0; i < count; i++)
        {
            string path = $"ntcp[{i}]";
            CompareText(differences, $"{path}.structure", left[i].StructureName, right[i].StructureName, true);
            CompareText(differences, $"{path}.canonical_structure", left[i].CanonicalStructure, right[i].CanonicalStructure, true);
            CompareText(differences, $"{path}.model_id", left[i].Result.ModelId, right[i].Result.ModelId, false);
            CompareText(differences, $"{path}.model_family", left[i].Result.ModelFamily, right[i].Result.ModelFamily, false);
            CompareText(differences, $"{path}.endpoint", left[i].Result.EndpointName, right[i].Result.EndpointName, false);
            CompareText(differences, $"{path}.time_point", left[i].Result.TimePoint, right[i].Result.TimePoint, false);
            CompareExact(differences, $"{path}.status", left[i].Result.Status, right[i].Result.Status);
            CompareExact(differences, $"{path}.reference_only", left[i].ReferenceOnly, right[i].ReferenceOnly);
            CompareNullable(differences, $"{path}.probability", left[i].Result.Probability, right[i].Result.Probability, options.ProbabilityAbsolute, 0.0);
            CompareNullable(differences, $"{path}.effective_dose_gy", left[i].Result.EffectiveDoseGy, right[i].Result.EffectiveDoseGy, options.DoseAbsoluteGy, options.DoseRelative);
            CompareText(differences, $"{path}.dose_basis", left[i].Result.AppliedDoseBasis, right[i].Result.AppliedDoseBasis, false);
            CompareStringSequence(differences, $"{path}.warnings", left[i].Result.Warnings.OrderBy(x => x, StringComparer.Ordinal), right[i].Result.Warnings.OrderBy(x => x, StringComparer.Ordinal));
            CompareStringSequence(differences, $"{path}.missing_inputs", left[i].Result.MissingInputs.OrderBy(x => x, StringComparer.Ordinal), right[i].Result.MissingInputs.OrderBy(x => x, StringComparer.Ordinal));
        }
    }

    private static void CompareTcp(
        List<PlanAnalysisDifference> differences,
        TcpAnalysisResult? baseline,
        TcpAnalysisResult? candidate,
        PlanAnalysisToleranceOptions options)
    {
        if (baseline is null || candidate is null)
        {
            if ((baseline is null) != (candidate is null))
                Add(differences, "tcp", baseline is null ? "null" : "present", candidate is null ? "null" : "present", "exact");

            return;
        }

        CompareText(differences, "tcp.target_structure", baseline.TargetStructureName, candidate.TargetStructureName, true);
        CompareNullable(differences, "tcp.target_volume_cc", baseline.TargetVolumeCc, candidate.TargetVolumeCc, options.VolumeAbsoluteCc, options.VolumeRelative);
        CompareText(differences, "tcp.model_id", baseline.Result.ModelId, candidate.Result.ModelId, false);
        CompareText(differences, "tcp.model_family", baseline.Result.ModelFamily, candidate.Result.ModelFamily, false);
        CompareText(differences, "tcp.endpoint", baseline.Result.EndpointName, candidate.Result.EndpointName, false);
        CompareText(differences, "tcp.time_point", baseline.Result.TimePoint, candidate.Result.TimePoint, false);
        CompareExact(differences, "tcp.status", baseline.Result.Status, candidate.Result.Status);
        CompareNullable(differences, "tcp.probability", baseline.Result.Probability, candidate.Result.Probability, options.ProbabilityAbsolute, 0.0);
        CompareNullable(differences, "tcp.effective_dose_gy", baseline.Result.EffectiveDoseGy, candidate.Result.EffectiveDoseGy, options.DoseAbsoluteGy, options.DoseRelative);
        CompareText(differences, "tcp.dose_basis", baseline.Result.AppliedDoseBasis, candidate.Result.AppliedDoseBasis, false);
        CompareStringSequence(differences, "tcp.warnings", baseline.Result.Warnings.OrderBy(x => x, StringComparer.Ordinal), candidate.Result.Warnings.OrderBy(x => x, StringComparer.Ordinal));
        CompareStringSequence(differences, "tcp.missing_inputs", baseline.Result.MissingInputs.OrderBy(x => x, StringComparer.Ordinal), candidate.Result.MissingInputs.OrderBy(x => x, StringComparer.Ordinal));
    }

    private static void CompareNullable(
        List<PlanAnalysisDifference> differences,
        string path,
        double? baseline,
        double? candidate,
        double absoluteTolerance,
        double relativeTolerance)
    {
        if (baseline is null || candidate is null)
        {
            if (baseline.HasValue != candidate.HasValue)
                Add(differences, path, Format(baseline), Format(candidate), "same nullability");

            return;
        }

        CompareNumber(differences, path, baseline.Value, candidate.Value, absoluteTolerance, relativeTolerance);
    }

    private static void CompareNumber(
        List<PlanAnalysisDifference> differences,
        string path,
        double baseline,
        double candidate,
        double absoluteTolerance,
        double relativeTolerance)
    {
        if (!double.IsFinite(baseline) || !double.IsFinite(candidate))
        {
            if (!baseline.Equals(candidate))
                Add(differences, path, Format(baseline), Format(candidate), "exact for non-finite values");

            return;
        }

        double allowed =
            absoluteTolerance +
            relativeTolerance * Math.Max(Math.Abs(baseline), Math.Abs(candidate));

        if (Math.Abs(baseline - candidate) > allowed)
        {
            Add(
                differences,
                path,
                Format(baseline),
                Format(candidate),
                $"|Δ| <= {Format(allowed)}");
        }
    }

    private static void CompareAbsolute(
        List<PlanAnalysisDifference> differences,
        string path,
        double baseline,
        double candidate,
        double tolerance)
        => CompareNumber(differences, path, baseline, candidate, tolerance, 0.0);

    private static void CompareExact<T>(
        List<PlanAnalysisDifference> differences,
        string path,
        T baseline,
        T candidate)
    {
        if (!EqualityComparer<T>.Default.Equals(baseline, candidate))
            Add(
                differences,
                path,
                baseline is null ? "null" : baseline.ToString() ?? "",
                candidate is null ? "null" : candidate.ToString() ?? "",
                "exact");
    }

    private static void CompareText(
        List<PlanAnalysisDifference> differences,
        string path,
        string? baseline,
        string? candidate,
        bool ignoreCase)
    {
        var comparison =
            ignoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        if (!string.Equals(baseline, candidate, comparison))
            Add(differences, path, baseline ?? "null", candidate ?? "null", ignoreCase ? "exact, case-insensitive" : "exact");
    }

    private static void CompareStringSequence(
        List<PlanAnalysisDifference> differences,
        string path,
        IEnumerable<string> baseline,
        IEnumerable<string> candidate)
    {
        string[] left = baseline.ToArray();
        string[] right = candidate.ToArray();

        CompareExact(differences, $"{path}.count", left.Length, right.Length);

        int count = Math.Min(left.Length, right.Length);
        for (int i = 0; i < count; i++)
            CompareText(differences, $"{path}[{i}]", left[i], right[i], false);
    }

    private static void Add(
        List<PlanAnalysisDifference> differences,
        string path,
        string baseline,
        string candidate,
        string rule)
        => differences.Add(
            new PlanAnalysisDifference(
                path,
                baseline,
                candidate,
                rule));

    private static string Format(double value)
        => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Format(double? value)
        => value.HasValue ? Format(value.Value) : "null";
}
