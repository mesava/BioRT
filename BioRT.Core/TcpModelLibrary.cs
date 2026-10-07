using System.Text.Json;

namespace BioRT.Core.Radiobiology;

public sealed class TcpModelLibrary
{
    private readonly List<TcpModelDefinition> _models;

    private TcpModelLibrary(List<TcpModelDefinition> models)
    {
        _models = models;
    }

    public IReadOnlyList<TcpModelDefinition> Models => _models;

    public static TcpModelLibrary Load(string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
            throw new ArgumentException("TCP library path must not be empty.", nameof(jsonPath));

        if (!File.Exists(jsonPath))
            throw new FileNotFoundException(
                $"TCP library not found: {Path.GetFullPath(jsonPath)}",
                jsonPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;

        var models = new List<TcpModelDefinition>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in root.EnumerateObject())
        {
            bool isModelArray =
                property.Value.ValueKind == JsonValueKind.Array &&
                (property.Name.Equals("models", StringComparison.OrdinalIgnoreCase) ||
                 property.Name.EndsWith("_models", StringComparison.OrdinalIgnoreCase));

            if (!isModelArray)
                continue;

            foreach (var node in property.Value.EnumerateArray())
            {
                TcpModelDefinition model = ParseModel(node);

                if (!ids.Add(model.Id))
                {
                    throw new InvalidOperationException(
                        $"Duplicate TCP model id '{model.Id}' across model collections.");
                }

                models.Add(model);
            }
        }

        if (models.Count == 0)
        {
            throw new InvalidOperationException(
                "TCP library contains no model arrays ('models' or '*_models').");
        }

        return new TcpModelLibrary(models);
    }

    private static TcpModelDefinition ParseModel(JsonElement node)
    {
        var diseaseNode = node.GetProperty("disease");
        var targetNode = node.GetProperty("target");
        var endpointNode = node.GetProperty("endpoint");
        var sourceNode = node.GetProperty("source");

        TcpImplementationDefinition? implementation = null;

        if (node.TryGetProperty("implementation", out var implNode) &&
            implNode.ValueKind == JsonValueKind.Object)
        {
            implementation = new TcpImplementationDefinition
            {
                InputMode = GetOptionalString(implNode, "input_mode"),
                RuntimeEnabled = GetOptionalBoolean(implNode, "runtime_enabled"),
                RuntimeReason = GetOptionalString(implNode, "runtime_reason"),
                RuntimeNote = GetOptionalString(implNode, "runtime_note"),
                ReferenceFractionCount = GetOptionalInt(implNode, "reference_fraction_count"),
                MinimumPrescriptionFractionSizeGy =
                    GetOptionalDouble(implNode, "minimum_prescription_fraction_size_gy"),
                MaximumPrescriptionFractionSizeGy =
                    GetOptionalDouble(implNode, "maximum_prescription_fraction_size_gy"),
                MinimumFractions =
                    GetOptionalInt(implNode, "minimum_fractions"),
                MaximumFractions =
                    GetOptionalInt(implNode, "maximum_fractions"),
                RequiredContextFields =
                    GetStringArray(implNode, "required_context_fields")
            };
        }

        return new TcpModelDefinition
        {
            Id = node.GetProperty("id").GetString()
                 ?? throw new InvalidOperationException("TCP model has no id."),
            ModelFamily = node.GetProperty("model_family").GetString() ?? "",
            EquationId = node.GetProperty("equation_id").GetString() ?? "",
            Disease = new TcpDiseaseDefinition
            {
                Diagnosis = GetOptionalString(diseaseNode, "diagnosis"),
                Histology = GetOptionalString(diseaseNode, "histology"),
                RiskGroup = GetOptionalString(diseaseNode, "risk_group"),
                Setting = GetOptionalString(diseaseNode, "setting")
            },
            Target = new TcpTargetDefinition
            {
                CanonicalStructure = GetOptionalString(targetNode, "canonical_structure"),
                Description = GetOptionalString(targetNode, "description"),
                SourceDoseDescriptor = GetOptionalString(targetNode, "source_dose_descriptor")
            },
            Endpoint = new TcpEndpointDefinition
            {
                Name = endpointNode.GetProperty("name").GetString() ?? "",
                Definition = GetOptionalString(endpointNode, "definition"),
                TimePoint = GetOptionalString(endpointNode, "time_point")
            },
            Source = new TcpSourceDefinition
            {
                Citation = GetOptionalString(sourceNode, "citation"),
                Pmid = GetOptionalString(sourceNode, "pmid"),
                Doi = GetOptionalString(sourceNode, "doi")
            },
            Status = node.GetProperty("status").GetString() ?? "",
            ImplementationNotes = GetOptionalString(node, "implementation_notes"),
            Parameters = node.GetProperty("parameters").Clone(),
            DoseBasis = node.TryGetProperty("dose_basis", out var doseBasis)
                ? doseBasis.Clone()
                : default,
            Implementation = implementation
        };
    }

    private static string? GetOptionalString(JsonElement node, string name)
        => node.TryGetProperty(name, out var p) &&
           p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static bool GetOptionalBoolean(JsonElement node, string name)
        => node.TryGetProperty(name, out var p) &&
           p.ValueKind is JsonValueKind.True or JsonValueKind.False &&
           p.GetBoolean();

    private static double? GetOptionalDouble(JsonElement node, string name)
        => node.TryGetProperty(name, out var p) &&
           p.ValueKind == JsonValueKind.Number
            ? p.GetDouble()
            : null;

    private static int? GetOptionalInt(JsonElement node, string name)
        => node.TryGetProperty(name, out var p) &&
           p.ValueKind == JsonValueKind.Number &&
           p.TryGetInt32(out int value)
            ? value
            : null;

    private static IReadOnlyList<string> GetStringArray(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var p) ||
            p.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return p.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToArray();
    }
}
