namespace BioRT.Core.Radiobiology;

public sealed class TcpModelSelector
{
    private readonly TcpModelLibrary _library;

    public TcpModelSelector(TcpModelLibrary library)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
    }

    public IReadOnlyList<TcpModelDefinition> Select(TcpModelQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        IEnumerable<TcpModelDefinition> q = _library.Models;

        if (!string.IsNullOrWhiteSpace(query.Diagnosis))
        {
            q = q.Where(m =>
                string.Equals(
                    m.Disease.Diagnosis,
                    query.Diagnosis,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Histology))
        {
            q = q.Where(m =>
                string.IsNullOrWhiteSpace(m.Disease.Histology) ||
                string.Equals(
                    m.Disease.Histology,
                    query.Histology,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.RiskGroup))
        {
            q = q.Where(m =>
                string.IsNullOrWhiteSpace(m.Disease.RiskGroup) ||
                string.Equals(
                    m.Disease.RiskGroup,
                    query.RiskGroup,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.Setting))
        {
            q = q.Where(m =>
                string.IsNullOrWhiteSpace(m.Disease.Setting) ||
                string.Equals(
                    m.Disease.Setting,
                    query.Setting,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.CanonicalTarget))
        {
            q = q.Where(m =>
                string.Equals(
                    m.Target.CanonicalStructure,
                    query.CanonicalTarget,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.EquationId))
        {
            q = q.Where(m =>
                string.Equals(
                    m.EquationId,
                    query.EquationId,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.TimePoint))
        {
            q = q.Where(m =>
                string.Equals(
                    m.Endpoint.TimePoint,
                    query.TimePoint,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!query.IncludeRuntimeDisabled)
            q = q.Where(m => m.Implementation?.RuntimeEnabled == true);

        return q
            .OrderBy(m => m.Disease.Diagnosis)
            .ThenBy(m => m.Disease.RiskGroup)
            .ThenBy(m => m.Endpoint.TimePoint)
            .ThenBy(m => m.Id)
            .ToArray();
    }
}

public sealed class TcpModelQuery
{
    public string? Diagnosis { get; init; }
    public string? Histology { get; init; }
    public string? RiskGroup { get; init; }
    public string? Setting { get; init; }
    public string? CanonicalTarget { get; init; }
    public string? EquationId { get; init; }
    public string? TimePoint { get; init; }
    public bool IncludeRuntimeDisabled { get; init; }
}
