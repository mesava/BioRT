using BioRT.Core.Models;
using BioRT.Core.Radiobiology;
using Xunit;

namespace BioRT.Tests;

public class ClinicalContextTests
{
    [Fact]
    public void ExampleClinicalContext_LoadsAndValidates()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "data",
            "clinical_context.example.json");

        Assert.True(File.Exists(path));

        ClinicalContext context = ClinicalContextLoader.Load(path);

        Assert.Equal("0.1.0", context.SchemaVersion);
        Assert.Equal(68.0, context.Patient.AgeYears!.Value, precision: 12);
        Assert.Equal("former", context.Patient.SmokingStatus);
        Assert.Equal("NSCLC", context.Tumor.Diagnosis);
        Assert.Equal("concurrent", context.Treatment.ChemotherapySequence);
    }

    [Fact]
    public void Mapper_DerivesAgeAndAppeltAgeCategory()
    {
        var context = new ClinicalContext
        {
            Patient = new ClinicalPatientContext
            {
                AgeYears = 68
            }
        };

        var mapped = ClinicalContextMapper.ToNtcpEvaluationContext(
            context,
            fractions: 30,
            dosePerFractionGy: 2.0);

        Assert.Equal(68.0, mapped.NumericPredictors["age_years"], precision: 12);
        Assert.Equal("yes", mapped.CategoricalPredictors["age_over_63"]);
        Assert.Equal(30, mapped.Fractions);
        Assert.Equal(2.0, mapped.DosePerFractionGy!.Value, precision: 12);
    }

    [Theory]
    [InlineData(2.0, "yes")]
    [InlineData(12.0, "no")]
    public void Mapper_FormerSmokerUsesCessationIntervalForNiezink(
        double months,
        string expected)
    {
        var context = new ClinicalContext
        {
            Patient = new ClinicalPatientContext
            {
                SmokingStatus = "former",
                MonthsSinceSmokingCessation = months
            }
        };

        var mapped = ClinicalContextMapper.ToNtcpEvaluationContext(
            context,
            fractions: 30,
            dosePerFractionGy: 2.0);

        Assert.Equal(
            expected,
            mapped.CategoricalPredictors["smoking_current_or_quit_lt3m"]);
    }

    [Fact]
    public void Mapper_DoesNotGuessRecentSmokingForFormerSmokerWithoutCessationInterval()
    {
        var context = new ClinicalContext
        {
            Patient = new ClinicalPatientContext
            {
                SmokingStatus = "former"
            }
        };

        var mapped = ClinicalContextMapper.ToNtcpEvaluationContext(
            context,
            fractions: 30,
            dosePerFractionGy: 2.0);

        Assert.Equal("former", mapped.CategoricalPredictors["smoking_status"]);
        Assert.False(
            mapped.CategoricalPredictors.ContainsKey(
                "smoking_current_or_quit_lt3m"));
    }

    [Theory]
    [InlineData("superior", "no")]
    [InlineData("middle", "yes")]
    [InlineData("inferior", "yes")]
    public void Mapper_DerivesAppeltTumorLocation(
        string location,
        string expected)
    {
        var context = new ClinicalContext
        {
            Tumor = new ClinicalTumorContext
            {
                Location = location
            }
        };

        var mapped = ClinicalContextMapper.ToNtcpEvaluationContext(
            context,
            fractions: null,
            dosePerFractionGy: null);

        Assert.Equal(
            expected,
            mapped.CategoricalPredictors[
                "tumor_location_mid_or_inferior"]);
    }

    [Fact]
    public void Mapper_MapsSequentialAndConcurrentChemotherapyConservatively()
    {
        var sequential = ClinicalContextMapper.ToNtcpEvaluationContext(
            new ClinicalContext
            {
                Treatment = new ClinicalTreatmentContext
                {
                    ChemotherapySequence = "sequential"
                }
            },
            fractions: null,
            dosePerFractionGy: null);

        var concurrent = ClinicalContextMapper.ToNtcpEvaluationContext(
            new ClinicalContext
            {
                Treatment = new ClinicalTreatmentContext
                {
                    ChemotherapySequence = "concurrent"
                }
            },
            fractions: null,
            dosePerFractionGy: null);

        var none = ClinicalContextMapper.ToNtcpEvaluationContext(
            new ClinicalContext
            {
                Treatment = new ClinicalTreatmentContext
                {
                    ChemotherapySequence = "none"
                }
            },
            fractions: null,
            dosePerFractionGy: null);

        Assert.Equal(
            "yes",
            sequential.CategoricalPredictors["sequential_chemotherapy"]);

        Assert.Equal(
            "no",
            concurrent.CategoricalPredictors["sequential_chemotherapy"]);

        Assert.False(
            none.CategoricalPredictors.ContainsKey(
                "sequential_chemotherapy"));
    }

    [Fact]
    public void Mapper_DoesNotConflateBeetzAndLippBaselineCategories()
    {
        var mild = ClinicalContextMapper.ToNtcpEvaluationContext(
            new ClinicalContext
            {
                Baseline = new ClinicalBaselineContext
                {
                    Xerostomia = "mild"
                }
            },
            fractions: null,
            dosePerFractionGy: null);

        var aBit = ClinicalContextMapper.ToNtcpEvaluationContext(
            new ClinicalContext
            {
                Baseline = new ClinicalBaselineContext
                {
                    Xerostomia = "a_bit"
                }
            },
            fractions: null,
            dosePerFractionGy: null);

        Assert.Equal(
            "mild",
            mild.CategoricalPredictors["baseline_xerostomia"]);

        Assert.False(
            mild.NumericPredictors.ContainsKey(
                "baseline_xerostomia_a_bit"));

        Assert.Equal(
            1.0,
            aBit.NumericPredictors["baseline_xerostomia_a_bit"],
            precision: 12);

        Assert.False(
            aBit.CategoricalPredictors.ContainsKey(
                "baseline_xerostomia"));
    }

    [Fact]
    public void Mapper_ExplicitOverrideWinsOverDerivedValue()
    {
        var context = new ClinicalContext
        {
            Patient = new ClinicalPatientContext
            {
                AgeYears = 68
            },
            ModelOverrides = new ClinicalModelOverrides
            {
                Numeric = new Dictionary<string, double>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["age_years"] = 70.0
                },
                Categorical = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["age_over_63"] = "no"
                }
            }
        };

        var mapped = ClinicalContextMapper.ToNtcpEvaluationContext(
            context,
            fractions: null,
            dosePerFractionGy: null);

        Assert.Equal(
            70.0,
            mapped.NumericPredictors["age_years"],
            precision: 12);

        Assert.Equal(
            "no",
            mapped.CategoricalPredictors["age_over_63"]);
    }

    [Fact]
    public void Loader_RejectsInvalidAge()
    {
        var context = new ClinicalContext
        {
            Patient = new ClinicalPatientContext
            {
                AgeYears = 200
            }
        };

        Assert.Throws<InvalidOperationException>(
            () => ClinicalContextLoader.Validate(context));
    }

    [Fact]
    public void Loader_RejectsUnknownSmokingStatus()
    {
        var context = new ClinicalContext
        {
            Patient = new ClinicalPatientContext
            {
                SmokingStatus = "occasional"
            }
        };

        Assert.Throws<InvalidOperationException>(
            () => ClinicalContextLoader.Validate(context));
    }

    [Fact]
    public void Loader_RejectsCessationIntervalForCurrentSmoker()
    {
        var context = new ClinicalContext
        {
            Patient = new ClinicalPatientContext
            {
                SmokingStatus = "current",
                MonthsSinceSmokingCessation = 1
            }
        };

        Assert.Throws<InvalidOperationException>(
            () => ClinicalContextLoader.Validate(context));
    }
}
