using BioRT.Core.Models;
using BioRT.Core.Radiobiology;
using Xunit;

namespace BioRT.Tests;

public class NtcpModelEngineTests
{
    private static string LibraryPath => Path.Combine(
        AppContext.BaseDirectory,
        "data",
        "ntcp_parameters_v2.json");

    [Fact]
    public void Selector_ReturnsRuntimeEnabledParotidModels()
    {
        var library = NtcpModelLibrary.Load(LibraryPath);
        var selector = new NtcpModelSelector(library);

        var models = selector.Select(new NtcpModelQuery
        {
            CanonicalStructure = "parotid_gland"
        });

        Assert.Contains(models, m => m.Id == "semenenko_li_2008_xerostomia_lkb_6m");
        Assert.Contains(models, m => m.Id == "dijkema_2010_parotid_flow_lkb_12m");
        Assert.Contains(models, m => m.Id == "beetz_2012_patient_rated_xerostomia_logistic_6m");
        Assert.DoesNotContain(models, m => m.Id == "eisbruch_1999_parotid_lkb");
    }

    [Fact]
    public void DijkemaAtTd50_ReturnsFiftyPercent()
    {
        var model = LoadModel("dijkema_2010_parotid_flow_lkb_12m");
        var engine = new NtcpModelEngine();

        var result = engine.Evaluate(
            model,
            UniformCumulativeDvh(39.9),
            new NtcpEvaluationContext
            {
                DosePerFractionGy = 2.0
            });

        Assert.Equal(NtcpEvaluationStatus.Calculated, result.Status);
        Assert.NotNull(result.Probability);
        Assert.Equal(0.5, result.Probability!.Value, precision: 12);
    }

    [Fact]
    public void SemenenkoRejectsNonTwoGyPlanUntilEqd2IsImplemented()
    {
        var model = LoadModel("semenenko_li_2008_xerostomia_lkb_6m");
        var engine = new NtcpModelEngine();

        var result = engine.Evaluate(
            model,
            UniformCumulativeDvh(31.4),
            new NtcpEvaluationContext
            {
                DosePerFractionGy = 5.0
            });

        Assert.Equal(NtcpEvaluationStatus.NotApplicable, result.Status);
        Assert.Null(result.Probability);
        Assert.Contains(
            result.Warnings,
            w => w.Contains("EQD2", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BeetzLogisticModel_MatchesClosedFormCalculation()
    {
        var model = LoadModel("beetz_2012_patient_rated_xerostomia_logistic_6m");
        var engine = new NtcpModelEngine();

        var context = new NtcpEvaluationContext
        {
            NumericPredictors = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["contralateral_parotid_mean_dose_gy"] = 20.0,
                ["baseline_xerostomia_a_bit"] = 1.0
            }
        };

        var result = engine.Evaluate(model, dvh: null, context);

        double expectedLinearPredictor =
            -1.443 +
            0.047 * 20.0 +
            0.720 * 1.0;

        double expected = 1.0 / (1.0 + Math.Exp(-expectedLinearPredictor));

        Assert.Equal(NtcpEvaluationStatus.Calculated, result.Status);
        Assert.NotNull(result.Probability);
        Assert.Equal(expected, result.Probability!.Value, precision: 12);
    }

    [Fact]
    public void LippModel_ReportsMissingBaselineCategory()
    {
        var model = LoadModel("lipp_v2_2_xerostomia_logistic_6m");
        var engine = new NtcpModelEngine();

        var context = new NtcpEvaluationContext
        {
            NumericPredictors = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["sqrt_left_parotid_mean_dose_plus_sqrt_right_parotid_mean_dose"] =
                    Math.Sqrt(20.0) + Math.Sqrt(22.0),
                ["combined_submandibular_mean_dose_gy"] = 30.0
            }
        };

        var result = engine.Evaluate(model, dvh: null, context);

        Assert.Equal(NtcpEvaluationStatus.MissingInputs, result.Status);
        Assert.Contains("baseline_xerostomia", result.MissingInputs);
        Assert.Null(result.Probability);
    }

    [Fact]
    public void LippModel_CalculatesWhenAllPredictorsAreProvided()
    {
        var model = LoadModel("lipp_v2_2_xerostomia_logistic_6m");
        var engine = new NtcpModelEngine();

        double bilateralParotidTerm = Math.Sqrt(20.0) + Math.Sqrt(22.0);

        var context = new NtcpEvaluationContext
        {
            NumericPredictors = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["sqrt_left_parotid_mean_dose_plus_sqrt_right_parotid_mean_dose"] =
                    bilateralParotidTerm,
                ["combined_submandibular_mean_dose_gy"] = 30.0
            },
            CategoricalPredictors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["baseline_xerostomia"] = "mild"
            }
        };

        var result = engine.Evaluate(model, dvh: null, context);

        double s =
            -2.295 +
            0.0996 * bilateralParotidTerm +
            0.0182 * 30.0 +
            0.459;

        double expected = 1.0 / (1.0 + Math.Exp(-s));

        Assert.Equal(NtcpEvaluationStatus.Calculated, result.Status);
        Assert.NotNull(result.Probability);
        Assert.Equal(expected, result.Probability!.Value, precision: 12);
    }

    private static NtcpModelDefinition LoadModel(string id)
    {
        var library = NtcpModelLibrary.Load(LibraryPath);

        return library.Models.Single(m =>
            string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private static StructureDVH UniformCumulativeDvh(double doseGy)
    {
        return new StructureDVH
        {
            Name = "Synthetic",
            Dose = [0.0, doseGy, doseGy + 0.1],
            Volume = [100.0, 100.0, 0.0],
            MeanDose = doseGy,
            MaxDose = doseGy
        };
    }
}
