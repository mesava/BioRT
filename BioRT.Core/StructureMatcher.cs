using System.Text.Json;

namespace BioRT.Core.Matching;

/// <summary>
/// Maps RT structure names to canonical anatomy names using an explicit alias library.
/// Matching is exact after normalization; substring matching is intentionally avoided.
/// </summary>
public sealed class StructureMatcher
{
    private readonly Dictionary<string, string> _aliasMap =
        new(StringComparer.OrdinalIgnoreCase);

    public StructureMatcher(string aliasesJsonPath)
    {
        if (string.IsNullOrWhiteSpace(aliasesJsonPath))
            throw new ArgumentException(
                "Alias library path must not be empty.",
                nameof(aliasesJsonPath));

        if (!File.Exists(aliasesJsonPath))
            throw new FileNotFoundException(
                $"Structure alias file not found: {Path.GetFullPath(aliasesJsonPath)}",
                aliasesJsonPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(aliasesJsonPath));

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException(
                "aliases.json root must be a JSON object.");

        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            if (entry.NameEquals("metadata"))
                continue;

            if (entry.Value.ValueKind != JsonValueKind.Object ||
                !entry.Value.TryGetProperty("aliases", out var aliases) ||
                aliases.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    $"Invalid aliases.json entry for '{entry.Name}'. " +
                    "Expected an object containing an 'aliases' string array.");
            }

            AddAlias(entry.Name, entry.Name);

            foreach (var aliasElement in aliases.EnumerateArray())
            {
                if (aliasElement.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException(
                        $"Invalid alias in '{entry.Name}'. All aliases must be strings.");
                }

                string? alias = aliasElement.GetString();

                if (string.IsNullOrWhiteSpace(alias))
                    continue;

                AddAlias(alias, entry.Name);
            }
        }

        if (_aliasMap.Count == 0)
            throw new InvalidOperationException(
                "aliases.json contains no valid alias mappings.");
    }

    /// <summary>
    /// Returns the canonical anatomy name, or null when no explicit alias matches.
    /// </summary>
    public string? Match(string structureName)
    {
        if (string.IsNullOrWhiteSpace(structureName))
            return null;

        string normalized = Normalize(structureName);

        return _aliasMap.TryGetValue(normalized, out var canonical)
            ? canonical
            : null;
    }

    private void AddAlias(string alias, string canonical)
    {
        string normalized = Normalize(alias);

        if (normalized.Length == 0)
            return;

        if (_aliasMap.TryGetValue(normalized, out var existingCanonical) &&
            !existingCanonical.Equals(canonical, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Ambiguous structure alias '{alias}': " +
                $"maps to both '{existingCanonical}' and '{canonical}'.");
        }

        _aliasMap[normalized] = canonical;
    }

    private static string Normalize(string value)
    {
        return string.Concat(
            value
                .Trim()
                .ToLowerInvariant()
                .Where(char.IsLetterOrDigit));
    }
}
