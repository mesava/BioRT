using System.Text.Json;

namespace BioRT.Core.Models;

public static class MonacoCriteriaParser
{
    public static IReadOnlyList<DoseCriterion> ParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<DoseCriterion>();

        using var document = JsonDocument.Parse(json);
        return Parse(document.RootElement);
    }

    public static IReadOnlyList<DoseCriterion> Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("prescriptions", out var prescriptions) ||
            prescriptions.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "Criteria JSON must contain a root 'prescriptions' array.");
        }

        var criteria = new List<DoseCriterion>();

        foreach (var item in prescriptions.EnumerateArray())
        {
            if (!item.TryGetProperty("prescription", out var prescription) ||
                prescription.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? structureName =
                prescription.TryGetProperty("structureName", out var structureNode) &&
                structureNode.ValueKind == JsonValueKind.String
                    ? structureNode.GetString()
                    : null;

            if (string.IsNullOrWhiteSpace(structureName))
                continue;

            if (!prescription.TryGetProperty("doseGoals", out var doseGoals) ||
                doseGoals.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var goal in doseGoals.EnumerateArray())
            {
                if (!goal.TryGetProperty("doseGoal", out var goalNode) ||
                    goalNode.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string? raw = goalNode.GetString();

                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                DoseCriterion? parsed =
                    DoseCriterionParser.Parse(structureName, raw);

                if (parsed != null)
                    criteria.Add(parsed);
            }
        }

        return criteria;
    }
}
