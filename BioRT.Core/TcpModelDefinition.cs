using System.Text.Json;

namespace BioRT.Core.Radiobiology;

public sealed class TcpModelDefinition
{
    public required string Id { get; init; }
    public required string ModelFamily { get; init; }
    public required string EquationId { get; init; }
    public required TcpDiseaseDefinition Disease { get; init; }
    public required TcpTargetDefinition Target { get; init; }
    public required TcpEndpointDefinition Endpoint { get; init; }
    public required TcpSourceDefinition Source { get; init; }
    public required string Status { get; init; }
    public string? ImplementationNotes { get; init; }
    public JsonElement Parameters { get; init; }
    public JsonElement DoseBasis { get; init; }
    public TcpImplementationDefinition? Implementation { get; init; }
}

public sealed class TcpDiseaseDefinition
{
    public string? Diagnosis { get; init; }
    public string? Histology { get; init; }
    public string? RiskGroup { get; init; }
    public string? Setting { get; init; }
}

public sealed class TcpTargetDefinition
{
    public string? CanonicalStructure { get; init; }
    public string? Description { get; init; }
    public string? SourceDoseDescriptor { get; init; }
}

public sealed class TcpEndpointDefinition
{
    public required string Name { get; init; }
    public string? Definition { get; init; }
    public string? TimePoint { get; init; }
}

public sealed class TcpSourceDefinition
{
    public string? Citation { get; init; }
    public string? Pmid { get; init; }
    public string? Doi { get; init; }
}

public sealed class TcpImplementationDefinition
{
    public string? InputMode { get; init; }
    public bool RuntimeEnabled { get; init; }
    public string? RuntimeReason { get; init; }
    public string? RuntimeNote { get; init; }
    public int? ReferenceFractionCount { get; init; }
    public double? MinimumPrescriptionFractionSizeGy { get; init; }
    public double? MaximumPrescriptionFractionSizeGy { get; init; }
    public int? MinimumFractions { get; init; }
    public int? MaximumFractions { get; init; }
    public IReadOnlyList<string> RequiredContextFields { get; init; } =
        Array.Empty<string>();
}
