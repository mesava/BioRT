using System.Text.Json;

namespace BioRT.Core.Matching;

public sealed class StructureMatcher
{
    private readonly Dictionary<string, string> _aliasMap = new();

    public StructureMatcher(string aliasesJsonPath)
    {
        Console.WriteLine("[StructureMatcher] Initializing");
        Console.WriteLine($"  Path: {Path.GetFullPath(aliasesJsonPath)}");

        if (!File.Exists(aliasesJsonPath))
            throw new FileNotFoundException(
                $"aliases.json not found: {aliasesJsonPath}");

        string jsonText = File.ReadAllText(aliasesJsonPath);
        Console.WriteLine($"  File size: {jsonText.Length} chars");

        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;

        Console.WriteLine($"  Root kind: {root.ValueKind}");

        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException(
                "aliases.json root must be a JSON object");

        foreach (var organ in root.EnumerateObject())
        {
            Console.WriteLine($"  Organ key: {organ.Name}");

            if (!organ.Value.TryGetProperty("aliases", out var aliases))
            {
                Console.WriteLine("    ⚠ no 'aliases' property");
                continue;
            }

            Console.WriteLine($"    aliases kind: {aliases.ValueKind}");

            if (aliases.ValueKind != JsonValueKind.Array)
            {
                Console.WriteLine("    ⚠ 'aliases' is not array");
                continue;
            }

            string canonical = organ.Name;
            string canonicalNorm = Normalize(canonical);

            _aliasMap[canonicalNorm] = canonical;
            Console.WriteLine($"    + canonical: {canonicalNorm}");

            foreach (var a in aliases.EnumerateArray())
            {
                if (a.ValueKind != JsonValueKind.String)
                    continue;

                string alias = a.GetString()!;
                string norm = Normalize(alias);

                _aliasMap[norm] = canonical;
                Console.WriteLine($"    + alias: {norm} -> {canonical}");
            }
        }

        Console.WriteLine($"[StructureMatcher] Loaded {_aliasMap.Count} aliases");

        if (_aliasMap.Count == 0)
            throw new InvalidOperationException(
                "aliases.json contains no valid alias mappings");
    }

    public string? Match(string structureName)
    {
        string key = Normalize(structureName);
        return _aliasMap.TryGetValue(key, out var canonical)
            ? canonical
            : null;
    }

    private static string Normalize(string s)
        => s
            .ToLowerInvariant()
            .Replace("_", "")
            .Replace("-", "")
            .Replace(" ", "");
}
