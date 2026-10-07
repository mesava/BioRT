using System.Text.Json;
using BioRT.Core.Models;

namespace BioRT.Core.Radiobiology;

public static class ClinicalContextLoader
{
    private static readonly HashSet<string> SmokingStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "never",
            "former",
            "current"
        };

    private static readonly HashSet<string> TumorLocations =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "superior",
            "middle",
            "inferior"
        };

    private static readonly HashSet<string> TumorLateralities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "left",
            "right",
            "midline",
            "bilateral"
        };

    private static readonly HashSet<string> ChemotherapySequences =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "none",
            "concurrent",
            "sequential"
        };

    private static readonly HashSet<string> XerostomiaCategories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "none",
            "a_bit",
            "mild",
            "moderate_to_severe"
        };

    public static ClinicalContext Load(string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
            throw new ArgumentException(
                "Clinical context path must not be empty.",
                nameof(jsonPath));

        if (!File.Exists(jsonPath))
            throw new FileNotFoundException(
                $"Clinical context file not found: {Path.GetFullPath(jsonPath)}",
                jsonPath);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        ClinicalContext? context = JsonSerializer.Deserialize<ClinicalContext>(
            File.ReadAllText(jsonPath),
            options);

        if (context == null)
            throw new InvalidOperationException(
                "Clinical context JSON could not be deserialized.");

        Validate(context);

        return context;
    }

    public static void Validate(ClinicalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!string.Equals(
                context.SchemaVersion,
                "0.1.0",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported clinical_context schema_version '{context.SchemaVersion}'. Expected 0.1.0.");
        }

        if (context.Patient.AgeYears is double age &&
            (double.IsNaN(age) || double.IsInfinity(age) || age < 0 || age > 130))
        {
            throw new InvalidOperationException(
                "patient.age_years must be between 0 and 130.");
        }

        if (context.Patient.MonthsSinceSmokingCessation is double months &&
            (double.IsNaN(months) || double.IsInfinity(months) || months < 0))
        {
            throw new InvalidOperationException(
                "patient.months_since_smoking_cessation must be >= 0.");
        }

        ValidateChoice(
            context.Patient.SmokingStatus,
            SmokingStatuses,
            "patient.smoking_status");

        ValidateChoice(
            context.Tumor.Location,
            TumorLocations,
            "tumor.location");

        ValidateChoice(
            context.Tumor.Laterality,
            TumorLateralities,
            "tumor.laterality");

        ValidateChoice(
            context.Treatment.ChemotherapySequence,
            ChemotherapySequences,
            "treatment.chemotherapy_sequence");

        ValidateChoice(
            context.Baseline.Xerostomia,
            XerostomiaCategories,
            "baseline.xerostomia");

        if (string.Equals(
                context.Patient.SmokingStatus,
                "never",
                StringComparison.OrdinalIgnoreCase) &&
            context.Patient.MonthsSinceSmokingCessation is not null)
        {
            throw new InvalidOperationException(
                "months_since_smoking_cessation must be omitted for a never-smoker.");
        }

        if (string.Equals(
                context.Patient.SmokingStatus,
                "current",
                StringComparison.OrdinalIgnoreCase) &&
            context.Patient.MonthsSinceSmokingCessation is not null)
        {
            throw new InvalidOperationException(
                "months_since_smoking_cessation must be omitted for a current smoker.");
        }
    }

    private static void ValidateChoice(
        string? value,
        HashSet<string> allowed,
        string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!allowed.Contains(value))
        {
            throw new InvalidOperationException(
                $"{field} has unsupported value '{value}'. Allowed: {string.Join(", ", allowed.OrderBy(x => x))}.");
        }
    }
}
