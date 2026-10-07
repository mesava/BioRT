using BioRT.Core.Analysis;
using BioRT.Core.Models;
using Xunit;

namespace BioRT.Tests;

public class PlanAnalysisComparatorTests
{
    [Fact]
    public void Comparator_AcceptsIdenticalScienceWithDifferentPatientIds()
    {
        PlanData baselinePlan = CreatePlan("PATIENT_A");
        PlanData candidatePlan = CreatePlan("PATIENT_B");

        PlanAnalysisComparisonResult comparison =
            PlanAnalysisComparator.Compare(
                baselinePlan,
                CreateResult(meanDose: 10.0),
                candidatePlan,
                CreateResult(meanDose: 10.0));

        Assert.True(comparison.IsMatch);
        Assert.Empty(comparison.Differences);
    }

    [Fact]
    public void Comparator_AcceptsTinyFloatingPointNoiseWithinConfiguredTolerance()
    {
        var options = new PlanAnalysisToleranceOptions
        {
            DoseAbsoluteGy = 1e-4,
            DoseRelative = 0.0
        };

        PlanAnalysisComparisonResult comparison =
            PlanAnalysisComparator.Compare(
                CreatePlan("SYNTHETIC"),
                CreateResult(meanDose: 10.0),
                CreatePlan("SYNTHETIC"),
                CreateResult(meanDose: 10.00005),
                options);

        Assert.True(comparison.IsMatch);
    }

    [Fact]
    public void Comparator_RejectsScientificChangeOutsideTolerance()
    {
        PlanAnalysisComparisonResult comparison =
            PlanAnalysisComparator.Compare(
                CreatePlan("SYNTHETIC"),
                CreateResult(meanDose: 10.0),
                CreatePlan("SYNTHETIC"),
                CreateResult(meanDose: 10.01));

        Assert.False(comparison.IsMatch);

        Assert.Contains(
            comparison.Differences,
            difference =>
                difference.Path.EndsWith(
                    ".mean_dose_gy",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void Comparator_RejectsDoseGridGeometryChange()
    {
        PlanData baselinePlan = CreatePlan("SYNTHETIC");
        PlanData candidatePlan = CreatePlan("SYNTHETIC");
        candidatePlan.Dose.OriginX = 0.01;

        PlanAnalysisComparisonResult comparison =
            PlanAnalysisComparator.Compare(
                baselinePlan,
                CreateResult(meanDose: 10.0),
                candidatePlan,
                CreateResult(meanDose: 10.0));

        Assert.False(comparison.IsMatch);

        Assert.Contains(
            comparison.Differences,
            difference =>
                difference.Path ==
                "dose_grid.origin_x_mm");
    }

    [Fact]
    public void Comparator_UsesDvhPercentToleranceForVxxPercentCriteria()
    {
        var options = new PlanAnalysisToleranceOptions
        {
            DoseAbsoluteGy = 1e-12,
            DoseRelative = 0.0,
            DvhVolumeAbsolutePercent = 1e-3
        };

        PlanAnalysisComparisonResult comparison =
            PlanAnalysisComparator.Compare(
                CreatePlan("SYNTHETIC"),
                CreateResultWithVxxCriterion(30.0),
                CreatePlan("SYNTHETIC"),
                CreateResultWithVxxCriterion(30.0005),
                options);

        Assert.True(comparison.IsMatch);
    }

    [Fact]
    public void Comparator_DefaultToleranceIsNumericalNotClinical()
    {
        var options = new PlanAnalysisToleranceOptions();

        Assert.True(options.DoseAbsoluteGy < 0.001);
        Assert.True(options.VolumeAbsoluteCc < 0.001);
        Assert.True(options.ProbabilityAbsolute < 1e-6);
    }

    private static PlanData CreatePlan(string patientId)
    {
        return new PlanData
        {
            PatientId = patientId,
            Fractions = 5,
            DosePerFraction = 6.0,
            Dose = new DoseVolume
            {
                Dose = new double[1, 1, 1]
                {
                    { { 10.0 } }
                },
                SpacingX = 2.0,
                SpacingY = 2.0,
                SpacingZ = 2.0,
                OriginX = 0.0,
                OriginY = 0.0,
                OriginZ = 0.0,
                ZPositions = [0.0]
            }
        };
    }

    private static PlanAnalysisResult CreateResultWithVxxCriterion(double value)
    {
        return new PlanAnalysisResult
        {
            Structures =
            [
                new StructureAnalysisResult
                {
                    Name = "Lungs-GTV",
                    VolumeCc = 1000.0,
                    Dvh = new StructureDVH
                    {
                        Name = "Lungs-GTV",
                        MeanDose = 10.0,
                        MaxDose = 20.0,
                        Dose = [0.0, 20.0],
                        Volume = [100.0, 0.0]
                    }
                }
            ],
            PtvMetrics = Array.Empty<PtvAnalysisResult>(),
            ClinicalCriteria =
            [
                new ClinicalCriterionEvaluation
                {
                    CriterionStructureName = "Lungs-GTV",
                    MatchedStructureName = "Lungs-GTV",
                    Criterion = new DoseCriterion
                    {
                        StructureName = "Lungs-GTV",
                        Type = CriterionType.VxxGyPercent,
                        DoseGy = 20.0,
                        Operator = "<=",
                        Limit = 35.0,
                        Raw = "V20Gy <= 35 %"
                    },
                    Value = value,
                    Pass = true
                }
            ],
            Ntcp = Array.Empty<NtcpAnalysisResult>(),
            Tcp = null,
            Warnings = Array.Empty<string>()
        };
    }

    private static PlanAnalysisResult CreateResult(double meanDose)
    {
        return new PlanAnalysisResult
        {
            Structures =
            [
                new StructureAnalysisResult
                {
                    Name = "Synthetic",
                    VolumeCc = 1.25,
                    Dvh = new StructureDVH
                    {
                        Name = "Synthetic",
                        MeanDose = meanDose,
                        MaxDose = 12.0,
                        Dose = [0.0, 10.0, 12.0],
                        Volume = [100.0, 50.0, 0.0]
                    }
                }
            ],
            PtvMetrics = Array.Empty<PtvAnalysisResult>(),
            ClinicalCriteria = Array.Empty<ClinicalCriterionEvaluation>(),
            Ntcp = Array.Empty<NtcpAnalysisResult>(),
            Tcp = null,
            Warnings = Array.Empty<string>()
        };
    }
}
