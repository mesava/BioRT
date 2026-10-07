using BioRT.Core.DVH;
using BioRT.Core.Models;
using BioRT.Core.Radiobiology;

namespace BioRT.Core.Analysis;

/// <summary>
/// Reusable plan-analysis orchestration shared by console and web clients.
///
/// This class intentionally contains no Console, file-system or UI code.
/// It consumes already imported RT data plus optional criteria/model context
/// and returns structured results.
/// </summary>
public sealed class PlanAnalysisService
{
    public PlanAnalysisResult Analyze(PlanAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Plan);
        ArgumentNullException.ThrowIfNull(request.StructureNames);
        ArgumentNullException.ThrowIfNull(request.Contours);

        if (request.Plan.Dose == null)
        {
            throw new InvalidOperationException(
                "Plan analysis requires a loaded RTDOSE.");
        }

        var warnings = new List<string>();

        var ptvRx = IdentifyPtvPrescriptions(request.Criteria);

        request.Plan.DVHs.Clear();

        var volumes =
            new Dictionary<string, double>(
                StringComparer.OrdinalIgnoreCase);

        var masks =
            new Dictionary<string, StructureMask>(
                StringComparer.OrdinalIgnoreCase);

        var criterionMatches =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        double voxelVolumeCc =
            request.Plan.Dose.SpacingX *
            request.Plan.Dose.SpacingY *
            request.Plan.Dose.SpacingZ /
            1000.0;

        // Preserve the current console workflow: calculate structures that are
        // referenced by clinical criteria, skipping PTV-like entries that have
        // no D50% >= prescription criterion.
        foreach (var group in request.Criteria.GroupBy(
                     c => c.StructureName,
                     StringComparer.OrdinalIgnoreCase))
        {
            string requestedName = group.Key;

            if (requestedName.StartsWith(
                    "PTV",
                    StringComparison.OrdinalIgnoreCase) &&
                !ptvRx.ContainsKey(requestedName))
            {
                continue;
            }

            StructureDVH? matched =
                EnsureCriterionStructure(
                    requestedName,
                    request,
                    volumes,
                    masks,
                    voxelVolumeCc,
                    warnings);

            if (matched != null)
                criterionMatches[requestedName] = matched.Name;
        }

        TcpAnalysisResult? tcp =
            EvaluateTcp(
                request,
                volumes,
                masks,
                voxelVolumeCc,
                warnings);

        var ptvResults =
            EvaluatePtvMetrics(
                request,
                ptvRx,
                criterionMatches,
                volumes,
                masks,
                warnings);

        var criterionResults =
            EvaluateClinicalCriteria(
                request,
                criterionMatches,
                volumes,
                warnings);

        var ntcpResults =
            EvaluateNtcp(
                request,
                ptvRx,
                criterionMatches,
                warnings);

        var structureResults =
            request.Plan.DVHs.Values
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => new StructureAnalysisResult
                {
                    Name = d.Name,
                    VolumeCc = volumes.TryGetValue(d.Name, out double volume)
                        ? volume
                        : double.NaN,
                    Dvh = d
                })
                .ToArray();

        return new PlanAnalysisResult
        {
            Structures = structureResults,
            PtvMetrics = ptvResults,
            ClinicalCriteria = criterionResults,
            Ntcp = ntcpResults,
            Tcp = tcp,
            Warnings = warnings
        };
    }

    private static Dictionary<string, double> IdentifyPtvPrescriptions(
        IReadOnlyList<DoseCriterion> criteria)
    {
        var result =
            new Dictionary<string, double>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var group in criteria.GroupBy(
                     c => c.StructureName,
                     StringComparer.OrdinalIgnoreCase))
        {
            DoseCriterion? d50 = group.FirstOrDefault(c =>
                c.Type == CriterionType.DxxPercent &&
                c.DxPercent is double dx &&
                Math.Abs(dx - 50.0) < 0.5 &&
                c.Operator == ">=");

            if (d50 != null)
                result[group.Key] = d50.Limit;
        }

        return result;
    }

    private static StructureDVH? EnsureCriterionStructure(
        string requestedName,
        PlanAnalysisRequest request,
        IDictionary<string, double> volumes,
        IDictionary<string, StructureMask> masks,
        double voxelVolumeCc,
        IList<string> warnings)
    {
        KeyValuePair<int, string>? match =
            ResolveCriterionStructure(
                requestedName,
                request.StructureNames,
                out string? matchError);

        if (match == null)
        {
            warnings.Add(
                matchError ??
                $"Structure referenced by criteria was not found: '{requestedName}'.");

            return null;
        }

        if (request.Plan.DVHs.TryGetValue(
                match.Value.Value,
                out StructureDVH? existing))
        {
            return existing;
        }

        if (!request.Contours.TryGetValue(
                match.Value.Key,
                out List<double[]>? contours) ||
            contours.Count == 0)
        {
            warnings.Add(
                $"Structure referenced by criteria has no contours: '{requestedName}' -> '{match.Value.Value}'.");

            return null;
        }

        return BuildStructureDvh(
            match.Value.Value,
            contours,
            request,
            volumes,
            masks,
            voxelVolumeCc);
    }

    private static KeyValuePair<int, string>? ResolveCriterionStructure(
        string requestedName,
        IReadOnlyDictionary<int, string> structureNames,
        out string? error)
    {
        error = null;

        KeyValuePair<int, string>[] exact =
            structureNames
                .Where(r => string.Equals(
                    r.Value,
                    requestedName,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (exact.Length == 1)
            return exact[0];

        if (exact.Length > 1)
        {
            error =
                $"RTSTRUCT contains multiple exact matches for criterion structure '{requestedName}'.";
            return null;
        }

        KeyValuePair<int, string>[] partial =
            structureNames
                .Where(r =>
                    r.Value.Contains(
                        requestedName,
                        StringComparison.OrdinalIgnoreCase) ||
                    requestedName.Contains(
                        r.Value,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (partial.Length == 1)
            return partial[0];

        if (partial.Length > 1)
        {
            error =
                $"Ambiguous RTSTRUCT match for criterion structure '{requestedName}': " +
                string.Join(
                    ", ",
                    partial.Select(x => x.Value));
            return null;
        }

        error =
            $"Structure referenced by criteria was not found: '{requestedName}'.";

        return null;
    }

    private static StructureDVH? EnsureExactStructure(
        string requestedName,
        PlanAnalysisRequest request,
        IDictionary<string, double> volumes,
        IDictionary<string, StructureMask> masks,
        double voxelVolumeCc,
        out string? error)
    {
        error = null;

        StructureDVH[] existing =
            request.Plan.DVHs.Values
                .Where(d => string.Equals(
                    d.Name,
                    requestedName,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (existing.Length == 1)
            return existing[0];

        if (existing.Length > 1)
        {
            error =
                $"Multiple calculated DVHs match exact target name '{requestedName}'.";
            return null;
        }

        var matches =
            request.StructureNames
                .Where(r => string.Equals(
                    r.Value,
                    requestedName,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (matches.Length == 0)
        {
            error =
                $"Exact RTSTRUCT ROI '{requestedName}' was not found.";
            return null;
        }

        if (matches.Length > 1)
        {
            error =
                $"RTSTRUCT contains multiple exact matches for target '{requestedName}'.";
            return null;
        }

        var match = matches[0];

        if (!request.Contours.TryGetValue(
                match.Key,
                out List<double[]>? contours) ||
            contours.Count == 0)
        {
            error =
                $"Target '{match.Value}' has no usable contours.";
            return null;
        }

        return BuildStructureDvh(
            match.Value,
            contours,
            request,
            volumes,
            masks,
            voxelVolumeCc);
    }

    private static StructureDVH? BuildStructureDvh(
        string name,
        List<double[]> contours,
        PlanAnalysisRequest request,
        IDictionary<string, double> volumes,
        IDictionary<string, StructureMask> masks,
        double voxelVolumeCc)
    {
        StructureMask mask =
            MaskBuilder.BuildMask(
                name,
                contours,
                request.Plan.Dose);

        double volumeCc =
            mask.Mask.Cast<bool>().Count(v => v) *
            voxelVolumeCc;

        var calculated =
            DVHCalculator.Calculate(
                mask,
                request.Plan.Dose);

        if (calculated == null)
            return null;

        var dvh = new StructureDVH
        {
            Name = name,
            MeanDose = calculated.MeanDose,
            MaxDose = calculated.MaxDose,
            Dose = calculated.DoseBins,
            Volume = calculated.VolumeBins
        };

        volumes[name] = volumeCc;
        masks[name] = mask;
        request.Plan.DVHs[name] = dvh;

        return dvh;
    }

    private static IReadOnlyList<PtvAnalysisResult> EvaluatePtvMetrics(
        PlanAnalysisRequest request,
        IReadOnlyDictionary<string, double> ptvRx,
        IReadOnlyDictionary<string, string> criterionMatches,
        IReadOnlyDictionary<string, double> volumes,
        IReadOnlyDictionary<string, StructureMask> masks,
        IList<string> warnings)
    {
        var results = new List<PtvAnalysisResult>();

        foreach (var pair in ptvRx)
        {
            StructureDVH? dvh = null;

            if (criterionMatches.TryGetValue(
                    pair.Key,
                    out string? matchedName))
            {
                request.Plan.DVHs.TryGetValue(
                    matchedName,
                    out dvh);
            }

            if (dvh == null)
            {
                warnings.Add(
                    $"PTV DVH was not available for '{pair.Key}'.");
                continue;
            }

            if (!volumes.TryGetValue(
                    dvh.Name,
                    out double volumeCc))
            {
                warnings.Add(
                    $"PTV volume was not available for '{dvh.Name}'.");
                continue;
            }

            double d2 = PtvMetricCalculator.D2(dvh);
            double d98 = PtvMetricCalculator.D98(dvh);

            double ci = double.NaN;
            double gi = double.NaN;

            if (masks.TryGetValue(
                    dvh.Name,
                    out StructureMask? mask))
            {
                ci = PtvSpatialMetrics.ComputeCI(
                    mask,
                    request.Plan.Dose,
                    pair.Value);

                gi = PtvSpatialMetrics.ComputeGI(
                    request.Plan.Dose,
                    pair.Value);
            }

            results.Add(
                new PtvAnalysisResult
                {
                    CriterionStructureName = pair.Key,
                    MatchedStructureName = dvh.Name,
                    PrescriptionDoseGy = pair.Value,
                    VolumeCc = volumeCc,
                    D2Gy = d2,
                    D98Gy = d98,
                    D95Gy = PtvMetricCalculator.D95(dvh),
                    D50Gy = PtvMetricCalculator.D50(dvh),
                    Hi = PtvMetricCalculator.HI(d2, d98),
                    Ci = ci,
                    Gi = gi
                });
        }

        return results;
    }

    private static IReadOnlyList<ClinicalCriterionEvaluation>
        EvaluateClinicalCriteria(
            PlanAnalysisRequest request,
            IReadOnlyDictionary<string, string> criterionMatches,
            IReadOnlyDictionary<string, double> volumes,
            IList<string> warnings)
    {
        var results =
            new List<ClinicalCriterionEvaluation>();

        foreach (DoseCriterion criterion in request.Criteria)
        {
            if (!criterionMatches.TryGetValue(
                    criterion.StructureName,
                    out string? matchedName) ||
                !request.Plan.DVHs.TryGetValue(
                    matchedName,
                    out StructureDVH? dvh))
            {
                continue;
            }

            if (!volumes.TryGetValue(
                    dvh.Name,
                    out double volumeCc))
            {
                warnings.Add(
                    $"Structure volume was not available for criterion '{criterion.Raw}' on '{dvh.Name}'.");
                continue;
            }

            double value = criterion.Type switch
            {
                CriterionType.Dmean =>
                    DoseMetricCalculator.Dmean(dvh),

                CriterionType.Dmax =>
                    DoseMetricCalculator.Dmax(dvh),

                CriterionType.DxxPercent =>
                    DoseMetricCalculator.DxPercent(
                        dvh,
                        criterion.DxPercent!.Value),

                CriterionType.VxxGyPercent =>
                    DoseMetricCalculator.VxxGyPercent(
                        dvh,
                        criterion.DoseGy!.Value),

                CriterionType.VxxGyCc =>
                    DoseMetricCalculator.VxxGyCc(
                        dvh,
                        criterion.DoseGy!.Value,
                        volumeCc),

                CriterionType.Dcc =>
                    DoseMetricCalculator.Dcc(
                        dvh,
                        criterion.VolumeCc!.Value,
                        volumeCc),

                _ => double.NaN
            };

            bool pass =
                criterion.Operator == "<="
                    ? value <= criterion.Limit
                    : value >= criterion.Limit;

            results.Add(
                new ClinicalCriterionEvaluation
                {
                    CriterionStructureName = criterion.StructureName,
                    MatchedStructureName = dvh.Name,
                    Criterion = criterion,
                    Value = value,
                    Pass = pass
                });
        }

        return results;
    }

    private static IReadOnlyList<NtcpAnalysisResult> EvaluateNtcp(
        PlanAnalysisRequest request,
        IReadOnlyDictionary<string, double> ptvRx,
        IReadOnlyDictionary<string, string> criterionMatches,
        IList<string> warnings)
    {
        if (request.StructureMatcher == null ||
            request.NtcpLibrary == null)
        {
            return Array.Empty<NtcpAnalysisResult>();
        }

        var selector =
            new NtcpModelSelector(request.NtcpLibrary);

        var engine =
            new NtcpModelEngine();

        NtcpEvaluationContext context =
            ClinicalContextMapper.ToNtcpEvaluationContext(
                request.ClinicalContext,
                fractions: request.Plan.Fractions > 0
                    ? request.Plan.Fractions
                    : null,
                dosePerFractionGy:
                    request.Plan.DosePerFraction > 0
                        ? request.Plan.DosePerFraction
                        : null);

        var results =
            new List<NtcpAnalysisResult>();

        var ptvStructureNames =
            ptvRx.Keys
                .Select(key =>
                    criterionMatches.TryGetValue(
                        key,
                        out string? matched)
                            ? matched
                            : key)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        foreach (StructureDVH dvh in request.Plan.DVHs.Values.Where(
                     d => !ptvStructureNames.Contains(d.Name)))
        {
            string? canonical =
                request.StructureMatcher.Match(dvh.Name);

            if (canonical == null)
                continue;

            var models =
                selector.Select(
                    new NtcpModelQuery
                    {
                        CanonicalStructure = canonical,
                        IncludeRuntimeDisabled = true
                    });

            foreach (NtcpModelDefinition model in models)
            {
                string? inputMode =
                    model.Implementation?.InputMode;

                bool structureCompatible =
                    string.Equals(
                        inputMode,
                        "single_structure_dvh",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        inputMode,
                        "single_structure_predictor_vector",
                        StringComparison.OrdinalIgnoreCase) ||
                    model.Implementation?.RuntimeEnabled != true;

                if (!structureCompatible)
                    continue;

                NtcpEvaluationResult evaluated =
                    engine.Evaluate(
                        model,
                        dvh,
                        context);

                results.Add(
                    new NtcpAnalysisResult
                    {
                        StructureName = dvh.Name,
                        CanonicalStructure = canonical,
                        Result = evaluated,
                        ReferenceOnly =
                            model.Implementation?.RuntimeEnabled != true
                    });
            }
        }

        foreach (NtcpModelDefinition model in selector
                     .Select(new NtcpModelQuery
                     {
                         EquationId = "logistic"
                     })
                     .Where(m => string.Equals(
                         m.Implementation?.InputMode,
                         "predictor_vector",
                         StringComparison.OrdinalIgnoreCase)))
        {
            NtcpEvaluationResult evaluated =
                engine.Evaluate(
                    model,
                    dvh: null,
                    context);

            results.Add(
                new NtcpAnalysisResult
                {
                    StructureName = null,
                    CanonicalStructure =
                        model.Implementation?.CanonicalStructure,
                    Result = evaluated,
                    ReferenceOnly = false
                });
        }

        return results;
    }

    private static TcpAnalysisResult? EvaluateTcp(
        PlanAnalysisRequest request,
        IDictionary<string, double> volumes,
        IDictionary<string, StructureMask> masks,
        double voxelVolumeCc,
        IList<string> warnings)
    {
        ClinicalContext? clinical =
            request.ClinicalContext;

        if (clinical == null ||
            string.IsNullOrWhiteSpace(clinical.Tcp.ModelId))
        {
            return null;
        }

        if (request.TcpLibrary == null)
        {
            warnings.Add(
                "TCP was requested but no TCP model library was supplied.");
            return null;
        }

        TcpModelDefinition? model =
            request.TcpLibrary.FindById(
                clinical.Tcp.ModelId!);

        if (model == null)
        {
            warnings.Add(
                $"TCP model ID was not found: '{clinical.Tcp.ModelId}'.");
            return null;
        }

        StructureDVH? targetDvh = null;
        string? targetName = null;
        double? targetVolume = null;

        if (string.Equals(
                model.Implementation?.InputMode,
                "target_dvh",
                StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(
                    clinical.Tcp.TargetStructureName))
            {
                warnings.Add(
                    "TCP target-DVH model requires tcp.target_structure_name.");
                return null;
            }

            targetDvh =
                EnsureExactStructure(
                    clinical.Tcp.TargetStructureName!,
                    request,
                    volumes,
                    masks,
                    voxelVolumeCc,
                    out string? targetError);

            if (targetDvh == null)
            {
                warnings.Add(
                    targetError ??
                    "TCP target DVH could not be calculated.");
                return null;
            }

            targetName = targetDvh.Name;

            if (volumes.TryGetValue(
                    targetDvh.Name,
                    out double volumeCc))
            {
                targetVolume = volumeCc;
            }
        }

        TcpEvaluationContext context =
            ClinicalContextMapper.ToTcpEvaluationContext(
                clinical,
                fractions: request.Plan.Fractions > 0
                    ? request.Plan.Fractions
                    : null,
                dosePerFractionGy:
                    request.Plan.DosePerFraction > 0
                        ? request.Plan.DosePerFraction
                        : null,
                totalPrescriptionDoseGy:
                    request.Plan.TotalDose > 0
                        ? request.Plan.TotalDose
                        : null);

        TcpEvaluationResult evaluated =
            new TcpModelEngine().Evaluate(
                model,
                targetDvh,
                context);

        return new TcpAnalysisResult
        {
            TargetStructureName = targetName,
            TargetVolumeCc = targetVolume,
            Result = evaluated
        };
    }
}
