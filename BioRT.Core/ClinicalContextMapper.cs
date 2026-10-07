using BioRT.Core.Models;

namespace BioRT.Core.Radiobiology;

public static class ClinicalContextMapper
{
    public static NtcpEvaluationContext ToNtcpEvaluationContext(
        ClinicalContext? clinicalContext,
        int? fractions,
        double? dosePerFractionGy)
    {
        var numeric = new Dictionary<string, double>(
            StringComparer.OrdinalIgnoreCase);

        var categorical = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        if (clinicalContext != null)
        {
            MapPatient(clinicalContext, numeric, categorical);
            MapTumor(clinicalContext, categorical);
            MapTreatment(clinicalContext, categorical);
            MapBaseline(clinicalContext, numeric, categorical);

            // Explicit model overrides are applied last. They exist for
            // model-specific variables that cannot be safely inferred from
            // generic clinical fields.
            foreach (var kv in clinicalContext.ModelOverrides.Numeric)
                numeric[kv.Key] = kv.Value;

            foreach (var kv in clinicalContext.ModelOverrides.Categorical)
                categorical[kv.Key] = kv.Value;
        }

        return new NtcpEvaluationContext
        {
            Fractions = fractions,
            DosePerFractionGy = dosePerFractionGy,
            NumericPredictors = numeric,
            CategoricalPredictors = categorical
        };
    }

    public static TcpEvaluationContext ToTcpEvaluationContext(
        ClinicalContext? clinicalContext,
        int? fractions,
        double? dosePerFractionGy,
        double? totalPrescriptionDoseGy)
    {
        return new TcpEvaluationContext
        {
            Fractions = fractions,
            DosePerFractionGy = dosePerFractionGy,
            TotalPrescriptionDoseGy = totalPrescriptionDoseGy,
            Diagnosis = clinicalContext?.Tumor.Diagnosis,
            Histology = clinicalContext?.Tumor.Histology,
            RiskGroup = clinicalContext?.Tumor.RiskGroup,
            Setting = clinicalContext?.Treatment.Setting,
            TargetRole = clinicalContext?.Tcp.TargetRole
        };
    }

    private static void MapPatient(
        ClinicalContext context,
        IDictionary<string, double> numeric,
        IDictionary<string, string> categorical)
    {
        if (context.Patient.AgeYears is double age)
        {
            numeric["age_years"] = age;
            categorical["age_over_63"] =
                age > 63.0 ? "yes" : "no";
        }

        if (!string.IsNullOrWhiteSpace(context.Patient.SmokingStatus))
        {
            string smoking = context.Patient.SmokingStatus!.ToLowerInvariant();

            categorical["smoking_status"] = smoking;

            if (smoking == "current")
            {
                categorical["smoking_current_or_quit_lt3m"] = "yes";
            }
            else if (smoking == "never")
            {
                categorical["smoking_current_or_quit_lt3m"] = "no";
            }
            else if (smoking == "former" &&
                     context.Patient.MonthsSinceSmokingCessation is double months)
            {
                categorical["smoking_current_or_quit_lt3m"] =
                    months < 3.0 ? "yes" : "no";
            }
            // Former smoking without cessation duration is deliberately not
            // converted to the Niezink binary predictor.
        }

        if (context.Patient.PulmonaryComorbidity is bool pulmonary)
        {
            categorical["pulmonary_comorbidity"] =
                pulmonary ? "yes" : "no";
        }
    }

    private static void MapTumor(
        ClinicalContext context,
        IDictionary<string, string> categorical)
    {
        if (!string.IsNullOrWhiteSpace(context.Tumor.Location))
        {
            string location = context.Tumor.Location!.ToLowerInvariant();

            categorical["tumor_location_mid_or_inferior"] =
                location is "middle" or "inferior"
                    ? "yes"
                    : "no";
        }

        if (!string.IsNullOrWhiteSpace(context.Tumor.Laterality))
        {
            categorical["tumor_laterality"] =
                context.Tumor.Laterality!.ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(context.Tumor.Diagnosis))
            categorical["diagnosis"] = context.Tumor.Diagnosis!;

        if (!string.IsNullOrWhiteSpace(context.Tumor.Histology))
            categorical["histology"] = context.Tumor.Histology!;

        if (!string.IsNullOrWhiteSpace(context.Tumor.Stage))
            categorical["stage"] = context.Tumor.Stage!;
    }

    private static void MapTreatment(
        ClinicalContext context,
        IDictionary<string, string> categorical)
    {
        if (!string.IsNullOrWhiteSpace(context.Treatment.ChemotherapySequence))
        {
            string chemo =
                context.Treatment.ChemotherapySequence!.ToLowerInvariant();

            if (chemo == "sequential")
            {
                categorical["sequential_chemotherapy"] = "yes";
            }
            else if (chemo == "concurrent")
            {
                categorical["sequential_chemotherapy"] = "no";
            }
            // "none" is intentionally not mapped to the Appelt binary
            // variable because its handling differs between publications.
        }

        if (context.Treatment.Immunotherapy is bool immunotherapy)
        {
            categorical["immunotherapy"] =
                immunotherapy ? "yes" : "no";
        }
    }

    private static void MapBaseline(
        ClinicalContext context,
        IDictionary<string, double> numeric,
        IDictionary<string, string> categorical)
    {
        if (string.IsNullOrWhiteSpace(context.Baseline.Xerostomia))
            return;

        string xerostomia =
            context.Baseline.Xerostomia!.ToLowerInvariant();

        if (xerostomia is "none" or "mild" or "moderate_to_severe")
            categorical["baseline_xerostomia"] = xerostomia;

        if (xerostomia == "none")
            numeric["baseline_xerostomia_a_bit"] = 0.0;
        else if (xerostomia == "a_bit")
            numeric["baseline_xerostomia_a_bit"] = 1.0;

        // "mild" is not silently equated to the exact Beetz "a bit"
        // questionnaire category, and "a_bit" is not silently reclassified
        // into the LIPP three-level baseline variable.
    }
}
