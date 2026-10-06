namespace BioRT.Core.Radiobiology;

public sealed class NtcpEvaluationContext
{
    /// <summary>
    /// Prescription/target dose per fraction reported by RTPLAN.
    /// This is used to determine whether a published parameter set derived
    /// around a reference daily fraction size requires fractionation correction.
    /// </summary>
    public double? DosePerFractionGy { get; init; }

    /// <summary>
    /// Number of planned fractions for the dose distribution being evaluated.
    /// Required for LQ BED/EQD conversions.
    /// </summary>
    public int? Fractions { get; init; }

    public IReadOnlyDictionary<string, double> NumericPredictors { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> CategoricalPredictors { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
