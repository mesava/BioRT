using BioRT.Core.Models;
using BioRT.Core.Radiobiology;
using Xunit;

namespace BioRT.Tests;

public class LungPneumonitisNtcpTests
{
    private static string LibraryPath => Path.Combine(
        AppContext.BaseDirectory,
        "data",
        "ntcp_parameters_v2.json");

    [Fact]
    public void Library_LoadsLungModelHierarchy()
    {
        var library = NtcpModelLibrary.Load(LibraryPath);

        Assert.Contains(
            library.Models,
            m => m.Id == "semenenko_li_2008_lung_rp_lkb_total_lung");

        Assert.Contains(
            library.Models,
            m => m.Id == "quantec_2010_lung_rp_logistic_mld");

        Assert.Contains(
            library.Models,
            m => m.Id == "niezink_2023_new_rp_logistic");

        Assert.Contains(
            library.Models,
            m => m.Id == "estro_2025_lung_rp_conventional_guideline_reference");

        Assert.Contains(
            library.Models,
            m => m.Id == "chen_2026_lung_rp_model_d_evidence");
    }

    [Fact]
    public void SemenenkoTotalLung_AtTd50_ReturnsFiftyPercent()
    {
        var model = LoadModel("semenenko_li_2008_lung_rp_lkb_total_lung");
        var engine = new NtcpModelEngine();

        var result = engine.Evaluate(
            model,
            UniformCumulativeDvh("Lungs", 29.9),
            new NtcpEvaluationContext
            {
                DosePerFractionGy = 2.0,
                Fractions = 30
            });

        Assert.Equal(NtcpEvaluationStatus.Calculated, result.Status);
        Assert.Equal(0.5, result.Probability!.Value, precision: 12);
        Assert.Equal(29.9, result.EffectiveDoseGy!.Value, precision: 12);
        Assert.Equal(
            "physical_mean_dose_source_convention",
            result.AppliedDoseBasis);
    }

    [Fact]
    public void QuantecLogistic_AutomaticallyUsesDvhMeanDose()
    {
        var model = LoadModel("quantec_2010_lung_rp_logistic_mld");
        var engine = new NtcpModelEngine();

        var result = engine.Evaluate(
            model,
            UniformCumulativeDvh("Lungs", 20.0),
            new NtcpEvaluationContext
            {
                DosePerFractionGy = 2.0,
                Fractions = 30
            });

        double linearPredictor = -3.87 + 0.126 * 20.0;
        double expected = 1.0 / (1.0 + Math.Exp(-linearPredictor));

        Assert.Equal(NtcpEvaluationStatus.Calculated, result.Status);
        Assert.Equal(expected, result.Probability!.Value, precision: 12);
    }

    [Fact]
    public void QuantecLogistic_IsBlockedForSbrtFractionSize()
    {
        var model = LoadModel("quantec_2010_lung_rp_logistic_mld");
        var engine = new NtcpModelEngine();

        var result = engine.Evaluate(
            model,
            UniformCumulativeDvh("Lungs", 6.0),
            new NtcpEvaluationContext
            {
                DosePerFractionGy = 10.0,
                Fractions = 5
            });

        Assert.Equal(NtcpEvaluationStatus.NotApplicable, result.Status);
        Assert.Null(result.Probability);
    }

    [Fact]
    public void OriginalAppelt_UsesDvhMeanDoseButRequiresClinicalFactors()
    {
        var model = LoadModel("appelt_2014_lung_rp_individualized_logistic");
        var engine = new NtcpModelEngine();

        var result = engine.Evaluate(
            model,
            UniformCumulativeDvh("Lungs", 15.0),
            new NtcpEvaluationContext
            {
                DosePerFractionGy = 2.0,
                Fractions = 30
            });

        Assert.Equal(NtcpEvaluationStatus.MissingInputs, result.Status);
        Assert.Contains("smoking_status", result.MissingInputs);
        Assert.Contains("pulmonary_comorbidity", result.MissingInputs);
        Assert.Contains("age_over_63", result.MissingInputs);
        Assert.Contains("sequential_chemotherapy", result.MissingInputs);
        Assert.Contains("tumor_location_mid_or_inferior", result.MissingInputs);
        Assert.DoesNotContain("mean_lung_dose_gy", result.MissingInputs);
    }

    [Fact]
    public void Niezink2023_MatchesPublishedThreePredictorEquation()
    {
        var model = LoadModel("niezink_2023_new_rp_logistic");
        var engine = new NtcpModelEngine();

        var context = new NtcpEvaluationContext
        {
            DosePerFractionGy = 2.0,
            Fractions = 30,
            NumericPredictors = new Dictionary<string, double>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["age_years"] = 68.0
            },
            CategoricalPredictors = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["smoking_current_or_quit_lt3m"] = "no"
            }
        };

        var result = engine.Evaluate(
            model,
            UniformCumulativeDvh("Lungs-GTV", 11.5),
            context);

        double s =
            -7.880 +
            0.250 * 11.5 +
            0.049 * 68.0;

        double expected = 1.0 / (1.0 + Math.Exp(-s));

        Assert.Equal(NtcpEvaluationStatus.Calculated, result.Status);
        Assert.Equal(expected, result.Probability!.Value, precision: 12);
    }

    [Fact]
    public void Niezink2023_CurrentOrRecentSmokerAppliesNegativeCoefficient()
    {
        var model = LoadModel("niezink_2023_new_rp_logistic");
        var engine = new NtcpModelEngine();

        var context = new NtcpEvaluationContext
        {
            DosePerFractionGy = 2.0,
            Fractions = 30,
            NumericPredictors = new Dictionary<string, double>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["age_years"] = 68.0
            },
            CategoricalPredictors = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["smoking_current_or_quit_lt3m"] = "yes"
            }
        };

        var result = engine.Evaluate(
            model,
            UniformCumulativeDvh("Lungs-GTV", 11.5),
            context);

        double s =
            -7.880 +
            0.250 * 11.5 +
            0.049 * 68.0 -
            0.902;

        double expected = 1.0 / (1.0 + Math.Exp(-s));

        Assert.Equal(NtcpEvaluationStatus.Calculated, result.Status);
        Assert.Equal(expected, result.Probability!.Value, precision: 12);
    }

    [Fact]
    public void Chen2026ModelD_RemainsEvidenceOnlyBecausePublishedSpecificationIsInconsistent()
    {
        var model = LoadModel("chen_2026_lung_rp_model_d_evidence");

        Assert.False(model.Implementation!.RuntimeEnabled);
        Assert.Equal("evidence_only", model.EquationId);
        Assert.Equal("experimental_external_validation", model.Status);
    }

    private static NtcpModelDefinition LoadModel(string id)
    {
        var library = NtcpModelLibrary.Load(LibraryPath);

        return library.Models.Single(m =>
            string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private static StructureDVH UniformCumulativeDvh(
        string name,
        double doseGy)
    {
        return new StructureDVH
        {
            Name = name,
            Dose = [0.0, doseGy, doseGy + 0.1],
            Volume = [100.0, 100.0, 0.0],
            MeanDose = doseGy,
            MaxDose = doseGy
        };
    }
}
