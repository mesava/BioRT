using System.Text.Json;

namespace BioRT.Core.Matching;

public sealed class StructureMatcher
{
    private readonly Dictionary<string, string> _aliasMap =
        new(StringComparer.OrdinalIgnoreCase);

    public StructureMatcher(string aliasesJsonPath)
        : this(ReadJsonFile(aliasesJsonPath))
    {
    }

    private StructureMatcher(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "aliases.json root must be a JSON object");
        }

        foreach (var organ in root.EnumerateObject())
        {
            if (!organ.Value.TryGetProperty("aliases", out var aliases) ||
                aliases.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            string canonical = organ.Name;

            _aliasMap[Normalize(canonical)] = canonical;

            foreach (var aliasNode in aliases.EnumerateArray())
            {
                if (aliasNode.ValueKind != JsonValueKind.String)
                    continue;

                string? alias = aliasNode.GetString();

                if (string.IsNullOrWhiteSpace(alias))
                    continue;

                _aliasMap[Normalize(alias)] = canonical;
            }
        }

        if (_aliasMap.Count == 0)
        {
            throw new InvalidOperationException(
                "aliases.json contains no valid alias mappings");
        }
    }

    public static StructureMatcher FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException(
                "Alias JSON must not be empty.",
                nameof(json));
        }

        using var document = JsonDocument.Parse(json);
        return new StructureMatcher(
            document.RootElement.Clone());
    }

    public string? Match(string structureName)
    {
        if (string.IsNullOrWhiteSpace(structureName))
            return null;

        string key = Normalize(structureName);

        return _aliasMap.TryGetValue(
            key,
            out string? canonical)
            ? canonical
            : null;
    }

    private static JsonElement ReadJsonFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "Alias JSON path must not be empty.",
                nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"aliases.json not found: {path}",
                path);
        }

        using var document =
            JsonDocument.Parse(
                File.ReadAllText(path));

        return document.RootElement.Clone();
    }

    private static string Normalize(string value)
        => value
            .ToLowerInvariant()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "");
}
