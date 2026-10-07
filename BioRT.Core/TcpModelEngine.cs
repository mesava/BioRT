using System.Text.Json;
using BioRT.Core.Models;

namespace BioRT.Core.Radiobiology;

public sealed class TcpModelEngine
{
    public TcpEvaluationResult Evaluate(
        TcpModelDefinition model,
        StructureDVH? targetDvh,
        TcpEvaluationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        context ??= new TcpEvaluationContext();

        var warnings = BuildWarnings(model);

        if (model.Implementation?.RuntimeEnabled != true)
        {
            if (!string.IsNullOrWhiteSpace(model.Implementation?.RuntimeReason))
                warnings.Add(model.Implementation.RuntimeReason!);

            return Build(
                model,
                TcpEvaluationStatus.RuntimeDisabled,
                warnings: warnings);
        }

        TcpEvaluationResult? contextFailure =
            CheckContext(model, context, warnings);

        if (contextFailure != null)
            return contextFailure;

        return model.EquationId switch
        {
            "linear_poisson_eqd2_dvh" =>
                EvaluateLinearPoissonDvh(model, targetDvh, context, warnings),

            "poisson_power2_eqd2_prescription" =>
                EvaluatePoissonPower2Prescription(model, context, warnings),

            "logistic_equivalent_fraction_count_dose" =>
                EvaluateEquivalentFractionLogistic(model, context, warnings),

            _ => Build(
                model,
                TcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    $"Equation '{model.EquationId}' is not implemented.")
                    .ToArray())
        };
    }

    private static TcpEvaluationResult EvaluateLinearPoissonDvh(
        TcpModelDefinition model,
        StructureDVH? targetDvh,
        TcpEvaluationContext context,
        List<string> warnings)
    {
        if (!string.Equals(
                model.Implementation?.InputMode,
                "target_dvh",
                StringComparison.OrdinalIgnoreCase))
        {
            return Build(
                model,
                TcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    $"Input mode '{model.Implementation?.InputMode}' is incompatible with a target-DVH TCP model.")
                    .ToArray());
        }

        if (targetDvh == null)
        {
            return Build(
                model,
                TcpEvaluationStatus.MissingInputs,
                warnings: warnings,
                missingInputs: new[] { "target_dvh" });
        }

        if (context.Fractions is not int fractions || fractions <= 0)
        {
            return Build(
                model,
                TcpEvaluationStatus.MissingInputs,
                warnings: warnings,
                missingInputs: new[] { "fractions" });
        }

        if (!TryGetCoreParameters(
                model.Parameters,
                requireAlphaBeta: true,
                out double d50,
                out double gamma,
                out double alphaBeta))
        {
            return Build(
                model,
                TcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    "Model must contain d50_gy, gamma and alpha_beta_gy.")
                    .ToArray());
        }

        double probability =
            TcpDoseResponseModel.CalculateLinearPoissonEqd2(
                targetDvh,
                fractions,
                d50,
                gamma,
                alphaBeta);

        warnings.Add(
            $"Per-bin target dose was converted to EQD2 using alpha/beta={alphaBeta:F2} Gy before TCP aggregation.");

        return Build(
            model,
            TcpEvaluationStatus.Calculated,
            probability,
            effectiveDoseGy: null,
            appliedDoseBasis: "target_dvh_per_bin_eqd2",
            warnings: warnings);
    }

    private static TcpEvaluationResult EvaluatePoissonPower2Prescription(
        TcpModelDefinition model,
        TcpEvaluationContext context,
        List<string> warnings)
    {
        if (!string.Equals(
                model.Implementation?.InputMode,
                "prescription_dose",
                StringComparison.OrdinalIgnoreCase))
        {
            return Build(
                model,
                TcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    $"Input mode '{model.Implementation?.InputMode}' is incompatible with a prescription-dose TCP model.")
                    .ToArray());
        }

        if (!TryGetPrescriptionCourse(
                context,
                out double totalDose,
                out int fractions,
                out var missing))
        {
            return Build(
                model,
                TcpEvaluationStatus.MissingInputs,
                warnings: warnings,
                missingInputs: missing);
        }

        if (!TryGetCoreParameters(
                model.Parameters,
                requireAlphaBeta: true,
                out double d50,
                out double gamma,
                out double alphaBeta))
        {
            return Build(
                model,
                TcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    "Model must contain d50_gy, gamma and alpha_beta_gy.")
                    .ToArray());
        }

        double eqd2 =
            FractionationCorrector.CalculateEquivalentDose(
                totalDose,
                fractions,
                alphaBeta,
                referenceFractionGy: 2.0);

        double probability =
            TcpDoseResponseModel.CalculatePoissonPowerOfTwoFromEqd2(
                eqd2,
                d50,
                gamma);

        warnings.Add(
            "This model was fitted to prescription dose rather than patient-level target DVHs; target heterogeneity is not represented.");

        return Build(
            model,
            TcpEvaluationStatus.Calculated,
            probability,
            effectiveDoseGy: eqd2,
            appliedDoseBasis: "prescription_eqd2",
            warnings: warnings);
    }

    private static TcpEvaluationResult EvaluateEquivalentFractionLogistic(
        TcpModelDefinition model,
        TcpEvaluationContext context,
        List<string> warnings)
    {
        if (!string.Equals(
                model.Implementation?.InputMode,
                "prescription_dose",
                StringComparison.OrdinalIgnoreCase))
        {
            return Build(
                model,
                TcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    $"Input mode '{model.Implementation?.InputMode}' is incompatible with a prescription-dose TCP model.")
                    .ToArray());
        }

        if (!TryGetPrescriptionCourse(
                context,
                out double totalDose,
                out int fractions,
                out var missing))
        {
            return Build(
                model,
                TcpEvaluationStatus.MissingInputs,
                warnings: warnings,
                missingInputs: missing);
        }

        if (!TryGetCoreParameters(
                model.Parameters,
                requireAlphaBeta: true,
                out double d50,
                out double gamma,
                out double alphaBeta))
        {
            return Build(
                model,
                TcpEvaluationStatus.Unsupported,
                warnings: warnings.Append(
                    "Model must contain d50_gy, gamma and alpha_beta_gy.")
                    .ToArray());
        }

        int referenceFractions =
            model.Implementation?.ReferenceFractionCount ??
            throw new InvalidOperationException(
                $"TCP model '{model.Id}' has no reference_fraction_count.");

        double equivalentDose =
            FractionationCorrector.CalculateEquivalentTotalDoseForFractions(
                totalDose,
                fractions,
                referenceFractions,
                alphaBeta);

        double probability =
            TcpDoseResponseModel.CalculateLogisticFromDose(
                equivalentDose,
                d50,
                gamma);

        warnings.Add(
            $"Prescription dose was converted to an isoeffective {referenceFractions}-fraction total dose using alpha/beta={alphaBeta:F2} Gy.");

        warnings.Add(
            "This pooled prescription-dose model does not represent target DVH heterogeneity, target volume, histology-specific effects, or prior-dose spatial overlap unless explicitly included by the source model.");

        return Build(
            model,
            TcpEvaluationStatus.Calculated,
            probability,
            equivalentDose,
            appliedDoseBasis: $"{referenceFractions}_fraction_equivalent_prescription_dose",
            warnings: warnings);
    }

    private static TcpEvaluationResult? CheckContext(
        TcpModelDefinition model,
        TcpEvaluationContext context,
        List<string> warnings)
    {
        var missing = new List<string>();

        foreach (string field in model.Implementation?.RequiredContextFields
                     ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(GetContextField(context, field)))
                missing.Add(field);
        }

        if (missing.Count > 0)
        {
            return Build(
                model,
                TcpEvaluationStatus.MissingInputs,
                warnings: warnings,
                missingInputs: missing);
        }

        if (!MatchesOptional(
                model.Disease.Diagnosis,
                context.Diagnosis))
        {
            return NotApplicable(
                model,
                warnings,
                "diagnosis",
                context.Diagnosis,
                model.Disease.Diagnosis);
        }

        if (!MatchesOptional(
                model.Disease.Histology,
                context.Histology))
        {
            return NotApplicable(
                model,
                warnings,
                "histology",
                context.Histology,
                model.Disease.Histology);
        }

        if (!MatchesOptional(
                model.Disease.RiskGroup,
                context.RiskGroup))
        {
            return NotApplicable(
                model,
                warnings,
                "risk_group",
                context.RiskGroup,
                model.Disease.RiskGroup);
        }

        if (!MatchesOptional(
                model.Disease.Setting,
                context.Setting))
        {
            return NotApplicable(
                model,
                warnings,
                "setting",
                context.Setting,
                model.Disease.Setting);
        }

        if (context.Fractions is int fractions)
        {
            if (model.Implementation?.MinimumFractions is int minFractions &&
                fractions < minFractions)
            {
                warnings.Add(
                    $"Fraction count {fractions} is below the configured source domain minimum ({minFractions}).");

                return Build(
                    model,
                    TcpEvaluationStatus.NotApplicable,
                    warnings: warnings);
            }

            if (model.Implementation?.MaximumFractions is int maxFractions &&
                fractions > maxFractions)
            {
                warnings.Add(
                    $"Fraction count {fractions} is above the configured source domain maximum ({maxFractions}).");

                return Build(
                    model,
                    TcpEvaluationStatus.NotApplicable,
                    warnings: warnings);
            }
        }

        if (context.DosePerFractionGy is double dosePerFraction)
        {
            if (model.Implementation?.MinimumPrescriptionFractionSizeGy is double minFx &&
                dosePerFraction < minFx)
            {
                warnings.Add(
                    $"Dose per fraction {dosePerFraction:F2} Gy is below the configured source domain minimum ({minFx:F2} Gy).");

                return Build(
                    model,
                    TcpEvaluationStatus.NotApplicable,
                    warnings: warnings);
            }

            if (model.Implementation?.MaximumPrescriptionFractionSizeGy is double maxFx &&
                dosePerFraction > maxFx)
            {
                warnings.Add(
                    $"Dose per fraction {dosePerFraction:F2} Gy is above the configured source domain maximum ({maxFx:F2} Gy).");

                return Build(
                    model,
                    TcpEvaluationStatus.NotApplicable,
                    warnings: warnings);
            }
        }

        return null;
    }

    private static bool TryGetPrescriptionCourse(
        TcpEvaluationContext context,
        out double totalDose,
        out int fractions,
        out IReadOnlyList<string> missing)
    {
        var fields = new List<string>();

        if (context.Fractions is not int f || f <= 0)
        {
            fields.Add("fractions");
            fractions = 0;
        }
        else
        {
            fractions = f;
        }

        if (context.TotalPrescriptionDoseGy is double total &&
            total > 0)
        {
            totalDose = total;
        }
        else if (context.DosePerFractionGy is double perFraction &&
                 perFraction > 0 &&
                 fractions > 0)
        {
            totalDose = perFraction * fractions;
        }
        else
        {
            fields.Add("total_prescription_dose_gy");
            totalDose = 0.0;
        }

        missing = fields.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        return missing.Count == 0;
    }

    private static bool TryGetCoreParameters(
        JsonElement parameters,
        bool requireAlphaBeta,
        out double d50,
        out double gamma,
        out double alphaBeta)
    {
        d50 = default;
        gamma = default;
        alphaBeta = default;

        bool hasD50 =
            TryGetDouble(parameters, "d50_gy", out d50);

        bool hasGamma =
            TryGetDouble(parameters, "gamma", out gamma);

        bool hasAlphaBeta =
            !requireAlphaBeta ||
            TryGetDouble(parameters, "alpha_beta_gy", out alphaBeta);

        return hasD50 && hasGamma && hasAlphaBeta;
    }

    private static string? GetContextField(
        TcpEvaluationContext context,
        string field)
    {
        return field.ToLowerInvariant() switch
        {
            "diagnosis" => context.Diagnosis,
            "histology" => context.Histology,
            "risk_group" => context.RiskGroup,
            "setting" => context.Setting,
            _ => null
        };
    }

    private static bool MatchesOptional(
        string? required,
        string? actual)
    {
        if (string.IsNullOrWhiteSpace(required))
            return true;

        if (string.IsNullOrWhiteSpace(actual))
            return true;

        return string.Equals(
            required,
            actual,
            StringComparison.OrdinalIgnoreCase);
    }

    private static TcpEvaluationResult NotApplicable(
        TcpModelDefinition model,
        List<string> warnings,
        string field,
        string? actual,
        string? required)
    {
        warnings.Add(
            $"Clinical context {field}='{actual}' does not match model domain '{required}'.");

        return Build(
            model,
            TcpEvaluationStatus.NotApplicable,
            warnings: warnings);
    }

    private static List<string> BuildWarnings(TcpModelDefinition model)
    {
        var warnings = new List<string>();

        if (!string.Equals(
                model.Status,
                "reference_candidate",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                model.Status,
                "validated_reference_candidate",
                StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(
                $"Parameter-set status: {model.Status}.");
        }

        if (!string.IsNullOrWhiteSpace(model.ImplementationNotes))
            warnings.Add(model.ImplementationNotes!);

        if (!string.IsNullOrWhiteSpace(model.Implementation?.RuntimeNote))
            warnings.Add(model.Implementation.RuntimeNote!);

        return warnings;
    }

    private static TcpEvaluationResult Build(
        TcpModelDefinition model,
        TcpEvaluationStatus status,
        double? probability = null,
        double? effectiveDoseGy = null,
        string? appliedDoseBasis = null,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? missingInputs = null)
    {
        return new TcpEvaluationResult
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
