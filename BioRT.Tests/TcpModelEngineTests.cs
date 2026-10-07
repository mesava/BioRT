using BioRT.Core.Models;
using BioRT.Core.Radiobiology;
using Xunit;

namespace BioRT.Tests;

public class TcpModelEngineTests
{
    private static string LibraryPath => Path.Combine(
        AppContext.BaseDirectory,
        "data",
        "tcp_parameters_v2.json");

    [Fact]
    public void SachpazidisMixedFollowup_ReproducesPublishedLinearPoissonEquation()
    {
        var model = LoadModel(
            "sachpazidis_2020_prostate_gland_lq_poisson_mixed_followup");

        var engine = new TcpModelEngine();

        // 68 Gy / 34 fx is exactly 2 Gy/fx, so physical dose == EQD2.
        var result = engine.Evaluate(
            model,
            UniformCumulativeDvh("Prostate", 68.0),
            new TcpEvaluationContext
            {
                Fractions = 34,
                DosePerFractionGy = 2.0,
                TotalPrescriptionDoseGy = 68.0,
                Diagnosis = "prostate_cancer",
                Setting = "definitive_ebrt"
            });

        double d50 = 66.8;
        double gamma = 3.8;
        double expected = Math.Exp(
            -Math.Exp(
                Math.E * gamma -
                (68.0 / d50) *
                (Math.E * gamma - Math.Log(Math.Log(2.0)))));

        Assert.Equal(TcpEvaluationStatus.Calculated, result.Status);
        Assert.NotNull(result.Probability);
        Assert.Equal(expected, result.Probability!.Value, precision: 12);
        Assert.Equal("target_dvh_per_bin_eqd2", result.AppliedDoseBasis);
    }

    [Fact]
    public void SachpazidisModel_IsBlockedOutsideSourceFractionationEnvelope()
    {
        var model = LoadModel(
            "sachpazidis_2020_prostate_gland_lq_poisson_mixed_followup");

        var result = new TcpModelEngine().Evaluate(
            model,
            UniformCumulativeDvh("Prostate", 60.0),
            new TcpEvaluationContext
            {
                Fractions = 5,
                DosePerFractionGy = 7.25,
                TotalPrescriptionDoseGy = 36.25,
                Diagnosis = "prostate_cancer",
                Setting = "definitive_ebrt"
            });

        Assert.Equal(TcpEvaluationStatus.NotApplicable, result.Status);
        Assert.Null(result.Probability);
    }

    [Fact]
    public void VargoTwoYearModel_AtD50InFiveFractions_ReturnsFiftyPercent()
    {
        var model = LoadModel(
            "vargo_2018_recurrent_hn_sbrt_local_control_2y");

        var result = new TcpModelEngine().Evaluate(
            model,
            targetDvh: null,
            new TcpEvaluationContext
            {
                Fractions = 5,
                DosePerFractionGy = 45.1 / 5.0,
                TotalPrescriptionDoseGy = 45.1,
                Diagnosis = "head_neck_cancer",
                Setting = "reirradiation_sbrt"
            });

        Assert.Equal(TcpEvaluationStatus.Calculated, result.Status);
        Assert.Equal(0.5, result.Probability!.Value, precision: 12);
        Assert.Equal(45.1, result.EffectiveDoseGy!.Value, precision: 12);
        Assert.Equal(
            "5_fraction_equivalent_prescription_dose",
            result.AppliedDoseBasis);
    }

    [Fact]
    public void VargoThreeYearModel_At45GyInFiveFractions_ReproducesAboutFortyOnePercent()
    {
        var model = LoadModel(
            "vargo_2018_recurrent_hn_sbrt_local_control_3y");

        var result = new TcpModelEngine().Evaluate(
            model,
            targetDvh: null,
            new TcpEvaluationContext
            {
                Fractions = 5,
                DosePerFractionGy = 9.0,
                TotalPrescriptionDoseGy = 45.0,
                Diagnosis = "head_neck_cancer",
                Setting = "reirradiation_sbrt"
            });

        Assert.Equal(TcpEvaluationStatus.Calculated, result.Status);
        Assert.InRange(result.Probability!.Value, 0.40, 0.42);
    }

    [Fact]
    public void RoyceHighRiskModel_ReproducesPublishedNinetyPercentDosePoint()
    {
        var model = LoadModel(
            "royce_2021_prostate_sbrt_ffbr_5y_high");

        var result = new TcpModelEngine().Evaluate(
            model,
            targetDvh: null,
            new TcpEvaluationContext
            {
                Fractions = 5,
                DosePerFractionGy = 37.6 / 5.0,
                TotalPrescriptionDoseGy = 37.6,
                Diagnosis = "prostate_cancer",
                RiskGroup = "high",
                Setting = "definitive_sbrt"
            });

        Assert.Equal(TcpEvaluationStatus.Calculated, result.Status);
        Assert.InRange(result.Probability!.Value, 0.89, 0.91);
        Assert.InRange(result.EffectiveDoseGy!.Value, 96.8, 97.1);
    }

    [Fact]
    public void RoyceHighRiskModel_ReproducesPublishedNinetyFivePercentDosePoint()
    {
        var model = LoadModel(
            "royce_2021_prostate_sbrt_ffbr_5y_high");

        var result = new TcpModelEngine().Evaluate(
            model,
            targetDvh: null,
            new TcpEvaluationContext
            {
                Fractions = 5,
                DosePerFractionGy = 38.7 / 5.0,
                TotalPrescriptionDoseGy = 38.7,
                Diagnosis = "prostate_cancer",
                RiskGroup = "high",
                Setting = "definitive_sbrt"
            });

        Assert.Equal(TcpEvaluationStatus.Calculated, result.Status);
        Assert.InRange(result.Probability!.Value, 0.94, 0.96);
        Assert.InRange(result.EffectiveDoseGy!.Value, 101.8, 102.2);
    }

    [Fact]
    public void RoyceLowIntermediateModel_RemainsBlocked()
    {
        var model = LoadModel(
            "royce_2021_prostate_sbrt_ffbr_5y_low_intermediate");

        var result = new TcpModelEngine().Evaluate(
            model,
            targetDvh: null,
            new TcpEvaluationContext
            {
                Fractions = 5,
                DosePerFractionGy = 36.1 / 5.0,
                TotalPrescriptionDoseGy = 36.1,
                Diagnosis = "prostate_cancer",
                RiskGroup = "low_intermediate",
                Setting = "definitive_sbrt"
            });

        Assert.Equal(TcpEvaluationStatus.RuntimeDisabled, result.Status);
        Assert.Null(result.Probability);
    }

    [Fact]
    public void ModelContextMismatch_IsNotApplicable()
    {
        var model = LoadModel(
            "vargo_2018_recurrent_hn_sbrt_local_control_2y");

        var result = new TcpModelEngine().Evaluate(
            model,
            targetDvh: null,
            new TcpEvaluationContext
            {
                Fractions = 5,
                DosePerFractionGy = 9.0,
                TotalPrescriptionDoseGy = 45.0,
                Diagnosis = "prostate_cancer",
                Setting = "definitive_sbrt"
            });

        Assert.Equal(TcpEvaluationStatus.NotApplicable, result.Status);
        Assert.Null(result.Probability);
    }

    private static TcpModelDefinition LoadModel(string id)
    {
        return TcpModelLibrary
            .Load(LibraryPath)
            .Models
            .Single(m =>
                string.Equals(
                    m.Id,
                    id,
                    StringComparison.OrdinalIgnoreCase));
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
