namespace BioRT.Core.Radiobiology;

public sealed class NtcpEvaluationContext
{
    public double? DosePerFractionGy { get; init; }

    public IReadOnlyDictionary<string, double> NumericPredictors { get; init; } =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> CategoricalPredictors { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
