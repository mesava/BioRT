using System.Text.Json;

namespace BioRT.Core.Radiobiology;

public sealed class NtcpModelDefinition
{
    public required string Id { get; init; }
    public required string ModelFamily { get; init; }
    public required string EquationId { get; init; }
    public required string StructureDefinition { get; init; }
    public required NtcpEndpointDefinition Endpoint { get; init; }
    public required NtcpSourceDefinition Source { get; init; }
    public required string Status { get; init; }
    public string? ImplementationNotes { get; init; }

    public JsonElement Parameters { get; init; }
    public JsonElement DoseBasis { get; init; }
    public NtcpImplementationDefinition? Implementation { get; init; }
}

public sealed class NtcpEndpointDefinition
{
    public required string Name { get; init; }
    public string? Measurement { get; init; }
    public string? Definition { get; init; }
    public string? TimePoint { get; init; }
}

public sealed class NtcpSourceDefinition
{
    public string? Citation { get; init; }
    public string? Pmid { get; init; }
    public string? Doi { get; init; }
    public string? ModelName { get; init; }
}

public sealed class NtcpImplementationDefinition
{
    public string? CanonicalStructure { get; init; }
    public string? InputMode { get; init; }
    public bool RuntimeEnabled { get; init; }
    public string? RuntimeReason { get; init; }
    public string? RuntimeNote { get; init; }
    public double? ReferenceFractionSizeGy { get; init; }
    public bool RequiresEqd2WhenFractionSizeDiffers { get; init; }
    public string? FractionationTransform { get; init; }
    public string? FractionationNote { get; init; }
    public double? MinimumPrescriptionFractionSizeGy { get; init; }
    public double? MaximumPrescriptionFractionSizeGy { get; init; }

    public IReadOnlyList<string> RequiredPredictors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RequiredCategoricalPredictors { get; init; } = Array.Empty<string>();
}
