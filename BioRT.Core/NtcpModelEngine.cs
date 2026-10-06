using System.Text.Json;
using BioRT.Core.Models;

namespace BioRT.Core.Radiobiology;

public sealed class NtcpModelEngine
{
    private const double FractionSizeToleranceGy = 0.05;

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

        return model.EquationId switch
        {
            "lkb_probit" => EvaluateLkb(model, dvh, context, warnings),
            "logistic" => EvaluateLogistic(model, context, warnings),
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

        if (model.Implementation?.RequiresEqd2WhenFractionSizeDiffers == true &&
            model.Implementation.ReferenceFractionSizeGy is double referenceFraction)
        {
            if (context.DosePerFractionGy is not double actualFraction)
            {
                warnings.Add(
                    "Dose per fraction is required to verify compatibility with this parameter set.");

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
                    $"but the current plan is {actualFraction:F2} Gy/fraction. " +
                    "Model-specific EQD2 conversion is required and is not yet applied by the NTCP engine.");

                return Build(
                    model,
                    NtcpEvaluationStatus.NotApplicable,
                    warnings: warnings);
            }
        }

        if (model.Implementation?.ReferenceFractionSizeGy is double refFx &&
            context.DosePerFractionGy is double actualFx &&
            Math.Abs(actualFx - refFx) > 0.25)
        {
            warnings.Add(
                $"Current fraction size ({actualFx:F2} Gy) differs from the model's reference context " +
                $"({refFx:F2} Gy). Result is an extrapolation unless the source explicitly supports this fractionation.");
        }

        if (!string.IsNullOrWhiteSpace(model.Implementation?.FractionationNote))
            warnings.Add(model.Implementation.FractionationNote!);

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

        double probability = LkbModel.CalculateNTCP(dvh, td50, m, n);

        return Build(
            model,
            NtcpEvaluationStatus.Calculated,
            probability,
            warnings);
    }

    private static NtcpEvaluationResult EvaluateLogistic(
        NtcpModelDefinition model,
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
                if (!context.NumericPredictors.TryGetValue(name, out double value))
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
            warnings);
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
            ParameterStatus = model.Status,
            Pmid = model.Source.Pmid,
            Doi = model.Source.Doi,
            Warnings = warnings ?? Array.Empty<string>(),
            MissingInputs = missingInputs ?? Array.Empty<string>()
        };
    }

    private static bool TryGetDouble(JsonElement node, string name, out double value)
    {
        if (node.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number)
        {
            value = p.GetDouble();
            return true;
        }

        value = default;
        return false;
    }
}
