namespace BioRT.Core.Radiobiology;

public enum TcpEvaluationStatus
{
    Calculated,
    MissingInputs,
    NotApplicable,
    RuntimeDisabled,
    Unsupported
}

public sealed class TcpEvaluationResult
{
    public required string ModelId { get; init; }
    public required string ModelFamily { get; init; }
    public required string EndpointName { get; init; }
    public string? TimePoint { get; init; }
    public required TcpEvaluationStatus Status { get; init; }
    public double? Probability { get; init; }
    public double? EffectiveDoseGy { get; init; }
    public string? AppliedDoseBasis { get; init; }
    public required string ParameterStatus { get; init; }
    public string? Pmid { get; init; }
    public string? Doi { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MissingInputs { get; init; } = Array.Empty<string>();
}
