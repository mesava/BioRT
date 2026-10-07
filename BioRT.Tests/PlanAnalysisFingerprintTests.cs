using BioRT.Core.Analysis;
using BioRT.Core.Models;
using Xunit;

namespace BioRT.Tests;

public class PlanAnalysisFingerprintTests
{
    [Fact]
    public void Fingerprint_IsDeterministicAndDoesNotDependOnPatientId()
    {
        var planA = CreatePlan("PATIENT_A");
        var planB = CreatePlan("PATIENT_B");

        PlanAnalysisResult result = CreateResult(meanDose: 10.0);

        string first =
            PlanAnalysisFingerprint.Compute(
                planA,
                result);

        string second =
            PlanAnalysisFingerprint.Compute(
                planB,
                result);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void Fingerprint_ChangesWhenScientificResultChanges()
    {
        var plan = CreatePlan("SYNTHETIC");

        string first =
            PlanAnalysisFingerprint.Compute(
                plan,
                CreateResult(meanDose: 10.0));

        string second =
            PlanAnalysisFingerprint.Compute(
                plan,
                CreateResult(meanDose: 10.1));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ShortFingerprint_IsPrefixOfFullFingerprint()
    {
        var plan = CreatePlan("SYNTHETIC");
        PlanAnalysisResult result = CreateResult(meanDose: 10.0);

        string full =
            PlanAnalysisFingerprint.Compute(
                plan,
                result);

        string shortValue =
            PlanAnalysisFingerprint.ComputeShort(
                plan,
                result,
                characters: 16);

        Assert.Equal(
            full[..16],
            shortValue);
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
