using System.Text.Json.Serialization;

namespace BioRT.Core.Models;

public sealed class ClinicalContext
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = "0.2.0";

    [JsonPropertyName("patient")]
    public ClinicalPatientContext Patient { get; init; } = new();

    [JsonPropertyName("tumor")]
    public ClinicalTumorContext Tumor { get; init; } = new();

    [JsonPropertyName("treatment")]
    public ClinicalTreatmentContext Treatment { get; init; } = new();

    [JsonPropertyName("baseline")]
    public ClinicalBaselineContext Baseline { get; init; } = new();

    [JsonPropertyName("tcp")]
    public ClinicalTcpContext Tcp { get; init; } = new();

    [JsonPropertyName("model_overrides")]
    public ClinicalModelOverrides ModelOverrides { get; init; } = new();
}

public sealed class ClinicalPatientContext
{
    [JsonPropertyName("age_years")]
    public double? AgeYears { get; init; }

    /// <summary>
    /// Allowed values: never, former, current.
    /// </summary>
    [JsonPropertyName("smoking_status")]
    public string? SmokingStatus { get; init; }

    [JsonPropertyName("months_since_smoking_cessation")]
    public double? MonthsSinceSmokingCessation { get; init; }

    [JsonPropertyName("pulmonary_comorbidity")]
    public bool? PulmonaryComorbidity { get; init; }
}

public sealed class ClinicalTumorContext
{
    [JsonPropertyName("diagnosis")]
    public string? Diagnosis { get; init; }

    [JsonPropertyName("histology")]
    public string? Histology { get; init; }

    [JsonPropertyName("stage")]
    public string? Stage { get; init; }

    /// <summary>
    /// Canonical disease-model risk group, for example:
    /// low_intermediate, high.
    /// </summary>
    [JsonPropertyName("risk_group")]
    public string? RiskGroup { get; init; }

    /// <summary>
    /// Allowed values for current NTCP mappings: superior, middle, inferior.
    /// </summary>
    [JsonPropertyName("location")]
    public string? Location { get; init; }

    /// <summary>
    /// Allowed values: left, right, midline, bilateral.
    /// </summary>
    [JsonPropertyName("laterality")]
    public string? Laterality { get; init; }
}

public sealed class ClinicalTreatmentContext
{
    /// <summary>
    /// Canonical treatment setting used for model applicability, for example:
    /// definitive_ebrt, definitive_sbrt, reirradiation_sbrt.
    /// </summary>
    [JsonPropertyName("setting")]
    public string? Setting { get; init; }

    /// <summary>
    /// Allowed values: none, concurrent, sequential.
    /// </summary>
    [JsonPropertyName("chemotherapy_sequence")]
    public string? ChemotherapySequence { get; init; }

    [JsonPropertyName("immunotherapy")]
    public bool? Immunotherapy { get; init; }
}

public sealed class ClinicalBaselineContext
{
    /// <summary>
    /// Optional generic baseline xerostomia category.
    /// Supported values: none, a_bit, mild, moderate_to_severe.
    /// Model-specific mappings are only made when they are unambiguous.
    /// </summary>
    [JsonPropertyName("xerostomia")]
    public string? Xerostomia { get; init; }
}

public sealed class ClinicalTcpContext
{
    /// <summary>
    /// Exact provenance-library model ID to evaluate.
    /// TCP is not evaluated automatically when this is omitted.
    /// </summary>
    [JsonPropertyName("model_id")]
    public string? ModelId { get; init; }

    /// <summary>
    /// Exact RTSTRUCT ROI name used when the selected model requires
    /// a target DVH.
    /// </summary>
    [JsonPropertyName("target_structure_name")]
    public string? TargetStructureName { get; init; }

    /// <summary>
    /// Explicit semantic role of the target, for example prostate_gland.
    /// For target-DVH models this must match the model record.
    /// </summary>
    [JsonPropertyName("target_role")]
    public string? TargetRole { get; init; }
}

public sealed class ClinicalModelOverrides
{
    [JsonPropertyName("numeric")]
    public Dictionary<string, double> Numeric { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("categorical")]
    public Dictionary<string, string> Categorical { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}
