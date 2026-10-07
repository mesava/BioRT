using System.Text.Json;
using BioRT.Core.Models;

namespace BioRT.Core.Radiobiology;

public sealed class NtcpModelEngine
{
    private const double FractionSizeToleranceGy = 0.05;
    private const string MeanDoseEqd2IfFractionDiffers =
        "mean_dose_eqd2_if_prescription_fraction_differs";
    private const string DvhBinEqd2 =
        "dvh_bin_eqd2";

    public NtcpEvaluationResult Evaluate(
        NtcpModelDefinition model,
        StructureDVH? dvh,
        NtcpEvaluationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        context ??= new NtcpEvaluationContext();

        var warnings = BuildCommonWarnings(model);

        if (model.Implementation?.RuntimeEnabled != true)
        {
            if (!string.IsNullOrWhiteSpace(model.Implementation?.RuntimeReason))
                warnings.Add(model.Implementation.RuntimeReason!);

            return Build(
                model,
                NtcpEvaluationStatus.RuntimeDisabled,
                warnings: warnings);
        }

        NtcpEvaluationResult? applicabilityFailure =
            CheckApplicability(model, context, warnings);

        if (applicabilityFailure != null)
            return applicabilityFailure;

        return model.EquationId switch
        {
            "lkb_probit" => EvaluateLkb(model, dvh, context, warnings),
            "logistic" => EvaluateLogistic(model, dvh, context, warnings),
            _ => Build(
                model,
                NtcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    $"Equation '{model.EquationId}' is not implemented.").ToArray())
        };
    }

    private static NtcpEvaluationResult EvaluateLkb(
        NtcpModelDefinition model,
        StructureDVH? dvh,
        NtcpEvaluationContext context,
        List<string> warnings)
    {
        if (!string.Equals(
                model.Implementation?.InputMode,
                "single_structure_dvh",
                StringComparison.OrdinalIgnoreCase))
        {
            return Build(
                model,
                NtcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    $"Input mode '{model.Implementation?.InputMode}' is not yet supported for automatic LKB evaluation.")
                    .ToArray());
        }

        if (dvh == null)
        {
            return Build(
                model,
                NtcpEvaluationStatus.MissingInputs,
                warnings: warnings,
                missingInputs: new[] { "structure_dvh" });
        }

        var p = model.Parameters;

        if (!TryGetDouble(p, "td50_gy", out double td50) ||
            !TryGetDouble(p, "m", out double m) ||
            !TryGetDouble(p, "n", out double n))
        {
            return Build(
                model,
                NtcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    "LKB parameter set must contain td50_gy, m and n.").ToArray());
        }

        string? transform = model.Implementation?.FractionationTransform;

        if (string.Equals(
                transform,
                MeanDoseEqd2IfFractionDiffers,
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateMeanDoseEqd2Model(
                model,
                dvh,
                context,
                td50,
                m,
                n,
                warnings);
        }

        if (string.Equals(
                transform,
                DvhBinEqd2,
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateDvhBinEqd2Model(
                model,
                dvh,
                context,
                td50,
                m,
                n,
                warnings);
        }

        // Backward-compatible safety gate for parameter sets that are known to
        // require EQD2 but do not yet declare a machine-readable transform.
        if (model.Implementation?.RequiresEqd2WhenFractionSizeDiffers == true &&
            model.Implementation.ReferenceFractionSizeGy is double referenceFraction)
        {
            if (context.DosePerFractionGy is not double actualFraction)
            {
                warnings.Add(
                    "Prescription dose per fraction is required to verify compatibility with this parameter set.");

                return Build(
                    model,
                    NtcpEvaluationStatus.MissingInputs,
                    warnings: warnings,
                    missingInputs: new[] { "dose_per_fraction_gy" });
            }

            if (Math.Abs(actualFraction - referenceFraction) > FractionSizeToleranceGy)
            {
                warnings.Add(
                    $"Model was normalized to {referenceFraction:F2} Gy/fraction, " +
                    $"but the current prescription context is {actualFraction:F2} Gy/fraction. " +
                    "A model-specific fractionation transform is required but is not defined.");

                return Build(
                    model,
                    NtcpEvaluationStatus.NotApplicable,
                    warnings: warnings);
            }
        }

        AddFractionationContextWarning(model, context, warnings);

        double effectiveDose = LkbModel.CalculateGEUD(dvh, n);
        double probability = LkbModel.CalculateNTCPFromEffectiveDose(
            effectiveDose,
            td50,
            m);

        return Build(
            model,
            NtcpEvaluationStatus.Calculated,
            probability,
            effectiveDose,
            appliedDoseBasis: "physical_dose",
            warnings: warnings);
    }

    private static NtcpEvaluationResult EvaluateMeanDoseEqd2Model(
        NtcpModelDefinition model,
        StructureDVH dvh,
        NtcpEvaluationContext context,
        double td50,
        double m,
        double n,
        List<string> warnings)
    {
        if (Math.Abs(n - 1.0) > 1e-12)
        {
            return Build(
                model,
                NtcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    "The mean-dose EQD2 transform is only valid in this implementation for n=1 mean-dose LKB models.")
                    .ToArray());
        }

        double referenceFraction =
            model.Implementation?.ReferenceFractionSizeGy ?? 2.0;

        if (context.DosePerFractionGy is not double prescriptionFraction)
        {
            warnings.Add(
                "Prescription dose per fraction is required to determine whether this source-specific EQD2 correction applies.");

            return Build(
                model,
                NtcpEvaluationStatus.MissingInputs,
                warnings: warnings,
                missingInputs: new[] { "dose_per_fraction_gy" });
        }

        // Semenenko & Li 2008 corrected published mean-organ doses only when
        // the daily treatment fraction differed from 2 Gy. We preserve that
        // source-specific convention rather than silently applying per-bin EQD2.
        if (Math.Abs(prescriptionFraction - referenceFraction) <=
            FractionSizeToleranceGy)
        {
            double physicalMean = dvh.MeanDose;
            double probability = LkbModel.CalculateNTCPFromEffectiveDose(
                physicalMean,
                td50,
                m);

            warnings.Add(
                $"No EQD{referenceFraction:F0} correction applied because the prescription fraction size " +
                $"({prescriptionFraction:F2} Gy) matches the model reference ({referenceFraction:F2} Gy) " +
                "within tolerance.");

            return Build(
                model,
                NtcpEvaluationStatus.Calculated,
                probability,
                physicalMean,
                appliedDoseBasis: "physical_mean_dose_source_convention",
                warnings: warnings);
        }

        if (context.Fractions is not int fractions || fractions <= 0)
        {
            warnings.Add(
                "Number of fractions is required for LQ conversion of the mean organ dose.");

            return Build(
                model,
                NtcpEvaluationStatus.MissingInputs,
                warnings: warnings,
                missingInputs: new[] { "fractions" });
        }

        if (!TryGetDouble(
                model.DoseBasis,
                "alpha_beta_gy",
                out double alphaBeta))
        {
            return Build(
                model,
                NtcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    "Fractionation-corrected model does not define alpha_beta_gy in dose_basis.")
                    .ToArray());
        }

        double correctedMean =
            FractionationCorrector.CalculateEquivalentDose(
                dvh.MeanDose,
                fractions,
                alphaBeta,
                referenceFraction);

        double correctedProbability =
            LkbModel.CalculateNTCPFromEffectiveDose(
                correctedMean,
                td50,
                m);

        warnings.Add(
            $"Applied source-specific mean-dose EQD{referenceFraction:F0} correction: " +
            $"N={fractions}, alpha/beta={alphaBeta:F2} Gy, " +
            $"physical Dmean={dvh.MeanDose:F2} Gy -> EQD{referenceFraction:F0}={correctedMean:F2} Gy.");

        warnings.Add(
            "LQ conversion assumes the evaluated total dose belongs to one equal-fraction course. " +
            "Composite doses from phases with different fractionation schedules require phase-specific correction.");

        return Build(
            model,
            NtcpEvaluationStatus.Calculated,
            correctedProbability,
            correctedMean,
            appliedDoseBasis: $"mean_dose_eqd{referenceFraction:F0}",
            warnings: warnings);
    }

    private static NtcpEvaluationResult EvaluateDvhBinEqd2Model(
        NtcpModelDefinition model,
        StructureDVH dvh,
        NtcpEvaluationContext context,
        double td50,
        double m,
        double n,
        List<string> warnings)
    {
        if (context.Fractions is not int fractions || fractions <= 0)
        {
            return Build(
                model,
                NtcpEvaluationStatus.MissingInputs,
                warnings: warnings.Append(
                    "Number of fractions is required for per-bin LQ DVH correction.")
                    .ToArray(),
                missingInputs: new[] { "fractions" });
        }

        if (!TryGetDouble(
                model.DoseBasis,
                "alpha_beta_gy",
                out double alphaBeta))
        {
            return Build(
                model,
                NtcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    "Per-bin fractionation-corrected model does not define alpha_beta_gy in dose_basis.")
                    .ToArray());
        }

        double referenceFraction =
            model.Implementation?.ReferenceFractionSizeGy ?? 2.0;

        StructureDVH corrected =
            FractionationCorrector.ConvertCumulativeDvhToEquivalentDose(
                dvh,
                fractions,
                alphaBeta,
                referenceFraction);

        double effectiveDose = LkbModel.CalculateGEUD(corrected, n);
        double probability = LkbModel.CalculateNTCPFromEffectiveDose(
            effectiveDose,
            td50,
            m);

        warnings.Add(
            $"Applied per-bin LQ EQD{referenceFraction:F0} conversion: " +
            $"N={fractions}, alpha/beta={alphaBeta:F2} Gy.");

        warnings.Add(
            "Per-bin LQ conversion assumes the same spatial dose pattern is delivered in every fraction. " +
            "Composite doses from different fractionation phases require phase-specific biological summation.");

        return Build(
            model,
            NtcpEvaluationStatus.Calculated,
            probability,
            effectiveDose,
            appliedDoseBasis: $"dvh_bin_eqd{referenceFraction:F0}",
            warnings: warnings);
    }

    private static NtcpEvaluationResult EvaluateLogistic(
        NtcpModelDefinition model,
        StructureDVH? dvh,
        NtcpEvaluationContext context,
        List<string> warnings)
    {
        var p = model.Parameters;

        if (!TryGetDouble(p, "intercept", out double linearPredictor))
        {
            return Build(
                model,
                NtcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    "Logistic parameter set must contain an intercept.").ToArray());
        }

        if (!p.TryGetProperty("predictors", out var predictors) ||
            predictors.ValueKind != JsonValueKind.Array)
        {
            return Build(
                model,
                NtcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    "Logistic parameter set must contain a predictors array.").ToArray());
        }

        var missing = new List<string>();

        foreach (var predictor in predictors.EnumerateArray())
        {
            string? name = predictor.TryGetProperty("name", out var nameNode)
                ? nameNode.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (predictor.TryGetProperty("coefficient", out var coefficientNode) &&
                coefficientNode.ValueKind == JsonValueKind.Number)
            {
                double value;

                if (TryResolveAutomaticPredictor(
                        predictor,
                        dvh,
                        out value))
                {
                    linearPredictor += coefficientNode.GetDouble() * value;
                    continue;
                }

                if (!context.NumericPredictors.TryGetValue(name, out value))
                {
                    missing.Add(name);
                    continue;
                }

                linearPredictor += coefficientNode.GetDouble() * value;
                continue;
            }

            if (predictor.TryGetProperty("coding", out var codingNode) &&
                codingNode.ValueKind == JsonValueKind.Object)
            {
                if (!context.CategoricalPredictors.TryGetValue(name, out string? category))
                {
                    missing.Add(name);
                    continue;
                }

                if (!codingNode.TryGetProperty(category, out var codedValue) ||
                    codedValue.ValueKind != JsonValueKind.Number)
                {
                    missing.Add($"{name} (unknown category '{category}')");
                    continue;
                }

                linearPredictor += codedValue.GetDouble();
                continue;
            }

            return Build(
                model,
                NtcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    $"Predictor '{name}' has an unsupported definition.").ToArray());
        }

        if (missing.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(model.Implementation?.RuntimeNote))
                warnings.Add(model.Implementation.RuntimeNote!);

            return Build(
                model,
                NtcpEvaluationStatus.MissingInputs,
                warnings: warnings,
                missingInputs: missing);
        }

        if (!string.IsNullOrWhiteSpace(model.Implementation?.RuntimeNote))
            warnings.Add(model.Implementation.RuntimeNote!);

        double probability = LogisticNtcpModel.Calculate(linearPredictor);

        return Build(
            model,
            NtcpEvaluationStatus.Calculated,
            probability,
            warnings: warnings);
    }

    private static NtcpEvaluationResult? CheckApplicability(
        NtcpModelDefinition model,
        NtcpEvaluationContext context,
        List<string> warnings)
    {
        if (model.Implementation?.MinimumPrescriptionFractionSizeGy is not double &&
            model.Implementation?.MaximumPrescriptionFractionSizeGy is not double)
        {
            return null;
        }

        if (context.DosePerFractionGy is not double actualFx)
        {
            warnings.Add(
                "Prescription dose per fraction is required to verify this model's applicability domain.");

            return Build(
                model,
                NtcpEvaluationStatus.MissingInputs,
                warnings: warnings,
                missingInputs: new[] { "dose_per_fraction_gy" });
        }

        if ((model.Implementation.MinimumPrescriptionFractionSizeGy is double minimum &&
             actualFx < minimum - FractionSizeToleranceGy) ||
            (model.Implementation.MaximumPrescriptionFractionSizeGy is double maximum &&
             actualFx > maximum + FractionSizeToleranceGy))
        {
            string range =
                $"{model.Implementation.MinimumPrescriptionFractionSizeGy?.ToString("F2") ?? "-inf"}" +
                " to " +
                $"{model.Implementation.MaximumPrescriptionFractionSizeGy?.ToString("F2") ?? "+inf"} Gy/fx";

            warnings.Add(
                $"Prescription fraction size {actualFx:F2} Gy/fx is outside the validated/configured model range ({range}).");

            return Build(
                model,
                NtcpEvaluationStatus.NotApplicable,
                warnings: warnings);
        }

        return null;
    }

    private static bool TryResolveAutomaticPredictor(
        JsonElement predictor,
        StructureDVH? dvh,
        out double value)
    {
        value = default;

        if (!predictor.TryGetProperty("auto_source", out var sourceNode) ||
            sourceNode.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? source = sourceNode.GetString();

        if (string.Equals(
                source,
                "dvh_mean_dose_gy",
                StringComparison.OrdinalIgnoreCase))
        {
            if (dvh == null)
                return false;

            value = dvh.MeanDose;
            return true;
        }

        if (string.Equals(
                source,
                "dvh_max_dose_gy",
                StringComparison.OrdinalIgnoreCase))
        {
            if (dvh == null)
                return false;

            value = dvh.MaxDose;
            return true;
        }

        return false;
    }

    private static void AddFractionationContextWarning(
        NtcpModelDefinition model,
        NtcpEvaluationContext context,
        List<string> warnings)
    {
        if (model.Implementation?.ReferenceFractionSizeGy is double refFx &&
            context.DosePerFractionGy is double actualFx &&
            Math.Abs(actualFx - refFx) > 0.25)
        {
            warnings.Add(
                $"Prescription fraction size ({actualFx:F2} Gy) differs from the model reference context " +
                $"({refFx:F2} Gy). No source-specific fractionation transform is defined for this model; " +
                "the result should be treated as an extrapolation.");
        }

        if (!string.IsNullOrWhiteSpace(model.Implementation?.FractionationNote))
            warnings.Add(model.Implementation.FractionationNote!);
    }

    private static List<string> BuildCommonWarnings(NtcpModelDefinition model)
    {
        var warnings = new List<string>();

        if (!string.Equals(
                model.Status,
                "reference_candidate",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                model.Status,
                "externally_validated_reference_candidate",
                StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Parameter-set status: {model.Status}.");
        }

        if (!string.IsNullOrWhiteSpace(model.ImplementationNotes))
            warnings.Add(model.ImplementationNotes!);

        return warnings;
    }

    private static NtcpEvaluationResult Build(
        NtcpModelDefinition model,
        NtcpEvaluationStatus status,
        double? probability = null,
        double? effectiveDoseGy = null,
        string? appliedDoseBasis = null,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? missingInputs = null)
    {
        return new NtcpEvaluationResult
        {
            ModelId = model.Id,
            ModelFamily = model.ModelFamily,
            EndpointName = model.Endpoint.Name,
            TimePoint = model.Endpoint.TimePoint,
            Status = status,
            Probability = probability,
            EffectiveDoseGy = effectiveDoseGy,
            AppliedDoseBasis = appliedDoseBasis,
            ParameterStatus = model.Status,
            Pmid = model.Source.Pmid,
            Doi = model.Source.Doi,
            Warnings = warnings ?? Array.Empty<string>(),
            MissingInputs = missingInputs ?? Array.Empty<string>()
        };
    }

    private static bool TryGetDouble(
        JsonElement node,
        string name,
        out double value)
    {
        if (node.ValueKind == JsonValueKind.Object &&
            node.TryGetProperty(name, out var p) &&
            p.ValueKind == JsonValueKind.Number)
        {
            value = p.GetDouble();
            return true;
        }

        value = default;
        return false;
    }
}
