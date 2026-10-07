using System.Reflection;
using System.Text.Json;

namespace BioRT.Web.Services;

public sealed class ModelCatalogService
{
    private const string NtcpResource =
        "BioRT.Web.Data.ntcp_parameters_v2.json";

    private const string TcpResource =
        "BioRT.Web.Data.tcp_parameters_v2.json";

    private IReadOnlyList<CatalogModel>? _ntcp;
    private IReadOnlyList<CatalogModel>? _tcp;

    public IReadOnlyList<CatalogModel> LoadNtcpModels()
        => _ntcp ??= LoadModels(NtcpResource, "NTCP");

    public IReadOnlyList<CatalogModel> LoadTcpModels()
        => _tcp ??= LoadModels(TcpResource, "TCP");

    private static IReadOnlyList<CatalogModel> LoadModels(
        string resourceName,
        string domain)
    {
        using Stream stream =
            Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded model library not found: {resourceName}");

        using var document = JsonDocument.Parse(stream);
        var models = new List<CatalogModel>();

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array ||
                !property.Name.EndsWith(
                    "_models",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var node in property.Value.EnumerateArray())
            {
                string? id = GetString(node, "id");

                if (string.IsNullOrWhiteSpace(id))
                    continue;

                string modelFamily =
                    GetString(node, "model_family") ?? "—";

                string equationId =
                    GetString(node, "equation_id") ?? "—";

                string status =
                    GetString(node, "status") ?? "—";

                string endpoint = "—";
                string? timePoint = null;

                if (node.TryGetProperty("endpoint", out var endpointNode) &&
                    endpointNode.ValueKind == JsonValueKind.Object)
                {
                    endpoint =
                        GetString(endpointNode, "name") ?? "—";

                    timePoint =
                        GetString(endpointNode, "time_point");
                }

                string? canonicalStructure = null;
                bool runtimeEnabled = false;

                if (node.TryGetProperty("implementation", out var implNode) &&
                    implNode.ValueKind == JsonValueKind.Object)
                {
                    canonicalStructure =
                        GetString(implNode, "canonical_structure");

                    runtimeEnabled =
                        implNode.TryGetProperty(
                            "runtime_enabled",
                            out var runtimeNode) &&
                        runtimeNode.ValueKind is
                            JsonValueKind.True or JsonValueKind.False &&
                        runtimeNode.GetBoolean();
                }

                string? diagnosis = null;
                string? setting = null;
                string? riskGroup = null;

                if (node.TryGetProperty("disease", out var diseaseNode) &&
                    diseaseNode.ValueKind == JsonValueKind.Object)
                {
                    diagnosis =
                        GetString(diseaseNode, "diagnosis");

                    setting =
                        GetString(diseaseNode, "setting");

                    riskGroup =
                        GetString(diseaseNode, "risk_group");
                }

                string? target = null;

                if (node.TryGetProperty("target", out var targetNode) &&
                    targetNode.ValueKind == JsonValueKind.Object)
                {
                    target =
                        GetString(targetNode, "canonical_structure");
                }

                string? pmid = null;
                string? doi = null;

                if (node.TryGetProperty("source", out var sourceNode) &&
                    sourceNode.ValueKind == JsonValueKind.Object)
                {
                    pmid = GetString(sourceNode, "pmid");
                    doi = GetString(sourceNode, "doi");
                }

                models.Add(
                    new CatalogModel(
                        Domain: domain,
                        Group: property.Name,
                        Id: id,
                        ModelFamily: modelFamily,
                        EquationId: equationId,
                        Status: status,
                        RuntimeEnabled: runtimeEnabled,
                        Endpoint: endpoint,
                        TimePoint: timePoint,
                        CanonicalStructure: canonicalStructure,
                        Diagnosis: diagnosis,
                        Setting: setting,
                        RiskGroup: riskGroup,
                        Target: target,
                        Pmid: pmid,
                        Doi: doi));
            }
        }

        return models
            .OrderByDescending(m => m.RuntimeEnabled)
            .ThenBy(m => m.Group)
            .ThenBy(m => m.Id)
            .ToArray();
    }

    private static string? GetString(
        JsonElement node,
        string propertyName)
    {
        return node.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}

public sealed record CatalogModel(
    string Domain,
    string Group,
    string Id,
    string ModelFamily,
    string EquationId,
    string Status,
    bool RuntimeEnabled,
    string Endpoint,
    string? TimePoint,
    string? CanonicalStructure,
    string? Diagnosis,
    string? Setting,
    string? RiskGroup,
    string? Target,
    string? Pmid,
    string? Doi)
{
    public string PrimaryContext =>
        string.Join(
            " · ",
            new[]
            {
                CanonicalStructure,
                Diagnosis,
                Setting,
                RiskGroup,
                Target
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase));

    public string EndpointDisplay =>
        string.IsNullOrWhiteSpace(TimePoint)
            ? Endpoint
            : $"{Endpoint} · {TimePoint}";
}
