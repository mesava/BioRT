namespace BioRT.Core.Radiobiology;

public sealed class TcpEvaluationContext
{
    public int? Fractions { get; init; }
    public double? DosePerFractionGy { get; init; }
    public double? TotalPrescriptionDoseGy { get; init; }

    public string? Diagnosis { get; init; }
    public string? Histology { get; init; }
    public string? RiskGroup { get; init; }
    public string? Setting { get; init; }

    /// <summary>
    /// Explicit semantic role of the evaluated target DVH, for example
    /// prostate_gland. This is not inferred from the ROI name.
    /// </summary>
    public string? TargetRole { get; init; }
}
