using System.Text.Json;

namespace BioRT.Core.Radiobiology;

public sealed class NtcpModelLibrary
{
    private readonly List<NtcpModelDefinition> _models;

    private NtcpModelLibrary(List<NtcpModelDefinition> models)
    {
        _models = models;
    }

    public IReadOnlyList<NtcpModelDefinition> Models => _models;

    public static NtcpModelLibrary Load(string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
            throw new ArgumentException("NTCP library path must not be empty.", nameof(jsonPath));

        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"NTCP library not found: {Path.GetFullPath(jsonPath)}", jsonPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;

        if (!root.TryGetProperty("xerostomia_models", out var modelsNode) ||
            modelsNode.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "NTCP library must contain a 'xerostomia_models' array.");
        }

        var models = new List<NtcpModelDefinition>();

        foreach (var node in modelsNode.EnumerateArray())
        {
            models.Add(ParseModel(node));
        }

        if (models.Count == 0)
            throw new InvalidOperationException("NTCP library contains no computable model definitions.");

        return new NtcpModelLibrary(models);
    }

    private static NtcpModelDefinition ParseModel(JsonElement node)
    {
        var endpointNode = node.GetProperty("endpoint");
        var sourceNode = node.GetProperty("source");

        NtcpImplementationDefinition? implementation = null;

        if (node.TryGetProperty("implementation", out var implNode) &&
            implNode.ValueKind == JsonValueKind.Object)
        {
            implementation = new NtcpImplementationDefinition
            {
                CanonicalStructure = GetOptionalString(implNode, "canonical_structure"),
                InputMode = GetOptionalString(implNode, "input_mode"),
                RuntimeEnabled = GetOptionalBoolean(implNode, "runtime_enabled"),
                RuntimeReason = GetOptionalString(implNode, "runtime_reason"),
                RuntimeNote = GetOptionalString(implNode, "runtime_note"),
                ReferenceFractionSizeGy = GetOptionalDouble(implNode, "reference_fraction_size_gy"),
                RequiresEqd2WhenFractionSizeDiffers =
                    GetOptionalBoolean(implNode, "requires_eqd2_when_fraction_size_differs"),
                FractionationNote = GetOptionalString(implNode, "fractionation_note"),
                RequiredPredictors = GetStringArray(implNode, "required_predictors"),
                RequiredCategoricalPredictors =
                    GetStringArray(implNode, "required_categorical_predictors")
            };
        }

        return new NtcpModelDefinition
        {
            Id = node.GetProperty("id").GetString()
                 ?? throw new InvalidOperationException("NTCP model has no id."),
            ModelFamily = node.GetProperty("model_family").GetString() ?? "",
            EquationId = node.GetProperty("equation_id").GetString() ?? "",
            StructureDefinition = node.GetProperty("structure_definition").GetString() ?? "",
            Endpoint = new NtcpEndpointDefinition
            {
                Name = endpointNode.GetProperty("name").GetString() ?? "",
                Measurement = GetOptionalString(endpointNode, "measurement"),
                Definition = GetOptionalString(endpointNode, "definition"),
                TimePoint = GetOptionalString(endpointNode, "time_point")
            },
            Source = new NtcpSourceDefinition
            {
                Citation = GetOptionalString(sourceNode, "citation"),
                Pmid = GetOptionalString(sourceNode, "pmid"),
                Doi = GetOptionalString(sourceNode, "doi"),
                ModelName = GetOptionalString(sourceNode, "model_name")
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
    {
        return node.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
    }

    private static bool GetOptionalBoolean(JsonElement node, string name)
    {
        return node.TryGetProperty(name, out var p) &&
               p.ValueKind is JsonValueKind.True or JsonValueKind.False &&
               p.GetBoolean();
    }

    private static double? GetOptionalDouble(JsonElement node, string name)
    {
        return node.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
            ? p.GetDouble()
            : null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return p.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToArray();
    }
}
