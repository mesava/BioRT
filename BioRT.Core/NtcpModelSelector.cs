namespace BioRT.Core.Radiobiology;

public sealed class NtcpModelSelector
{
    private readonly NtcpModelLibrary _library;

    public NtcpModelSelector(NtcpModelLibrary library)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
    }

    public IReadOnlyList<NtcpModelDefinition> Select(NtcpModelQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        IEnumerable<NtcpModelDefinition> q = _library.Models;

        if (!string.IsNullOrWhiteSpace(query.CanonicalStructure))
        {
            q = q.Where(m =>
                string.Equals(
                    m.Implementation?.CanonicalStructure,
                    query.CanonicalStructure,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.EquationId))
        {
            q = q.Where(m =>
                string.Equals(m.EquationId, query.EquationId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.ModelFamily))
        {
            q = q.Where(m =>
                string.Equals(m.ModelFamily, query.ModelFamily, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.TimePoint))
        {
            q = q.Where(m =>
                string.Equals(m.Endpoint.TimePoint, query.TimePoint, StringComparison.OrdinalIgnoreCase));
        }

        if (!query.IncludeRuntimeDisabled)
            q = q.Where(m => m.Implementation?.RuntimeEnabled == true);

        return q
            .OrderBy(m => m.Endpoint.TimePoint)
            .ThenBy(m => m.Id)
            .ToArray();
    }
}

public sealed class NtcpModelQuery
{
    public string? CanonicalStructure { get; init; }
    public string? EquationId { get; init; }
    public string? ModelFamily { get; init; }
    public string? TimePoint { get; init; }
    public bool IncludeRuntimeDisabled { get; init; }
}
