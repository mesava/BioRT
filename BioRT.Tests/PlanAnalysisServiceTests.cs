using BioRT.Core.Analysis;
using BioRT.Core.Models;
using Xunit;

namespace BioRT.Tests;

public class PlanAnalysisServiceTests
{
    [Fact]
    public void MonacoCriteriaParser_ParsesSupportedGoals()
    {
        const string json = """
        {
          "prescriptions": [
            {
              "prescription": {
                "structureName": "Parotid_L",
                "doseGoals": [
                  { "doseGoal": "Dmean <= 26 Gy" },
                  { "doseGoal": "V20Gy <= 50 %" }
                ]
              }
            }
          ]
        }
        """;

        IReadOnlyList<DoseCriterion> criteria =
            MonacoCriteriaParser.ParseJson(json);

        Assert.Equal(2, criteria.Count);

        Assert.Contains(
            criteria,
            c => c.Type == CriterionType.Dmean &&
                 c.Limit == 26.0);

        Assert.Contains(
            criteria,
            c => c.Type == CriterionType.VxxGyPercent &&
                 c.DoseGy == 20.0 &&
                 c.Limit == 50.0);
    }

    [Fact]
    public void MonacoCriteriaParser_HandlesRealisticMonacoAnnotationsAndCmQuestionMark()
    {
        const string json = """
        {
          "prescriptions": [
            {
              "prescription": {
                "structureName": "PTV",
                "doseGoals": [
                  { "doseGoal": "D2% <= 64.2 Gy (+1.8 Gy)" },
                  { "doseGoal": "V58.8Gy >= 98 % (-3 %)" }
                ]
              }
            },
            {
              "prescription": {
                "structureName": "Lens_L",
                "doseGoals": [
                  { "doseGoal": "V20Gy <= 65 cm?" }
                ]
              }
            }
          ]
        }
        """;

        IReadOnlyList<DoseCriterion> criteria =
            MonacoCriteriaParser.ParseJson(json);

        Assert.Equal(3, criteria.Count);

        Assert.Contains(
            criteria,
            c => c.Type == CriterionType.DxxPercent &&
                 c.DxPercent == 2.0 &&
                 c.Operator == "<=" &&
                 c.Limit == 64.2);

        Assert.Contains(
            criteria,
            c => c.Type == CriterionType.VxxGyPercent &&
                 c.DoseGy == 58.8 &&
                 c.Operator == ">=" &&
                 c.Limit == 98.0);

        Assert.Contains(
            criteria,
            c => c.Type == CriterionType.VxxGyCc &&
                 c.DoseGy == 20.0 &&
                 c.Operator == "<=" &&
                 c.Limit == 65.0);
    }

    [Fact]
    public void AnalysisService_PreservesPtvAndClinicalCriteriaCalculations()
    {
        var dose = new DoseVolume
        {
            Dose = new double[2, 2, 1]
            {
                { { 60.0 }, { 60.0 } },
                { { 60.0 }, { 60.0 } }
            },
            SpacingX = 1.0,
            SpacingY = 1.0,
            SpacingZ = 1.0,
            OriginX = 0.0,
            OriginY = 0.0,
            OriginZ = 0.0,
            ZPositions = [0.0]
        };

        var plan = new PlanData
        {
            PatientId = "SYNTHETIC",
            Fractions = 30,
            DosePerFraction = 2.0,
            Dose = dose
        };

        double[] square =
        [
            -0.1, -0.1, 0.0,
             2.1, -0.1, 0.0,
             2.1,  2.1, 0.0,
            -0.1,  2.1, 0.0
        ];

        var request = new PlanAnalysisRequest
        {
            Plan = plan,
            StructureNames = new Dictionary<int, string>
            {
                [1] = "PTV_60",
                [2] = "OAR"
            },
            Contours = new Dictionary<int, List<double[]>>
            {
                [1] = [square],
                [2] = [square]
            },
            Criteria =
            [
                new DoseCriterion
                {
                    StructureName = "PTV_60",
                    Type = CriterionType.DxxPercent,
                    DxPercent = 50.0,
                    Operator = ">=",
                    Limit = 60.0,
                    Raw = "D50% >= 60 Gy"
                },
                new DoseCriterion
                {
                    StructureName = "PTV_60",
                    Type = CriterionType.DxxPercent,
                    DxPercent = 95.0,
                    Operator = ">=",
                    Limit = 57.0,
                    Raw = "D95% >= 57 Gy"
                },
                new DoseCriterion
                {
                    StructureName = "OAR",
                    Type = CriterionType.Dmean,
                    Operator = "<=",
                    Limit = 65.0,
                    Raw = "Dmean <= 65 Gy"
                }
            ]
        };

        PlanAnalysisResult result =
            new PlanAnalysisService().Analyze(request);

        Assert.Equal(2, result.Structures.Count);

        PtvAnalysisResult ptv =
            Assert.Single(result.PtvMetrics);

        Assert.Equal("PTV_60", ptv.MatchedStructureName);
        Assert.Equal(0.004, ptv.VolumeCc, precision: 12);
        Assert.Equal(60.0, ptv.D2Gy, precision: 12);
        Assert.Equal(60.0, ptv.D98Gy, precision: 12);
        Assert.Equal(60.0, ptv.D95Gy, precision: 12);
        Assert.Equal(60.0, ptv.D50Gy, precision: 12);
        Assert.Equal(1.0, ptv.Hi, precision: 12);
        Assert.Equal(1.0, ptv.Ci, precision: 12);
        Assert.Equal(1.0, ptv.Gi, precision: 12);

        Assert.Equal(3, result.ClinicalCriteria.Count);

        ClinicalCriterionEvaluation ptvD50 =
            Assert.Single(
                result.ClinicalCriteria.Where(c =>
                    c.Criterion.Raw == "D50% >= 60 Gy"));

        Assert.Equal("PTV_60", ptvD50.MatchedStructureName);
        Assert.Equal(60.0, ptvD50.Value, precision: 12);
        Assert.True(ptvD50.Pass);

        ClinicalCriterionEvaluation ptvD95 =
            Assert.Single(
                result.ClinicalCriteria.Where(c =>
                    c.Criterion.Raw == "D95% >= 57 Gy"));

        Assert.Equal("PTV_60", ptvD95.MatchedStructureName);
        Assert.Equal(60.0, ptvD95.Value, precision: 12);
        Assert.True(ptvD95.Pass);

        ClinicalCriterionEvaluation criterion =
            Assert.Single(
                result.ClinicalCriteria.Where(c =>
                    c.Criterion.Raw == "Dmean <= 65 Gy"));

        Assert.Equal("OAR", criterion.MatchedStructureName);
        Assert.Equal(60.0, criterion.Value, precision: 12);
        Assert.True(criterion.Pass);

        Assert.Empty(result.Ntcp);
        Assert.Null(result.Tcp);
    }

    [Fact]
    public void AnalysisService_ExactStructureNameWinsOverContainingName()
    {
        var dose = new DoseVolume
        {
            Dose = new double[2, 2, 1]
            {
                { { 60.0 }, { 60.0 } },
                { { 5.0 }, { 5.0 } }
            },
            SpacingX = 1.0,
            SpacingY = 1.0,
            SpacingZ = 1.0,
            OriginX = 0.0,
            OriginY = 0.0,
            OriginZ = 0.0,
            ZPositions = [0.0]
        };

        double[] brain =
        [
            -0.1, -0.1, 0.0,
             1.0, -0.1, 0.0,
             1.0,  2.1, 0.0,
            -0.1,  2.1, 0.0
        ];

        double[] brainstem =
        [
             1.0, -0.1, 0.0,
             2.1, -0.1, 0.0,
             2.1,  2.1, 0.0,
             1.0,  2.1, 0.0
        ];

        var request = new PlanAnalysisRequest
        {
            Plan = new PlanData
            {
                PatientId = "SYNTHETIC",
                Fractions = 30,
                DosePerFraction = 2.0,
                Dose = dose
            },
            StructureNames = new Dictionary<int, string>
            {
                [2] = "Brain",
                [3] = "Brainstem"
            },
            Contours = new Dictionary<int, List<double[]>>
            {
                [2] = [brain],
                [3] = [brainstem]
            },
            Criteria =
            [
                new DoseCriterion
                {
                    StructureName = "Brainstem",
                    Type = CriterionType.Dmax,
                    Operator = "<=",
                    Limit = 54.0,
                    Raw = "Dmax <= 54 Gy"
                }
            ]
        };

        PlanAnalysisResult result =
            new PlanAnalysisService().Analyze(request);

        StructureAnalysisResult structure =
            Assert.Single(result.Structures);

        Assert.Equal("Brainstem", structure.Name);

        ClinicalCriterionEvaluation criterion =
            Assert.Single(result.ClinicalCriteria);

        Assert.Equal("Brainstem", criterion.MatchedStructureName);
        Assert.Equal(5.0, criterion.Value, precision: 12);
        Assert.True(criterion.Pass);
    }

    [Fact]
    public void AnalysisService_UniqueDecoratedCriterionNameMapsToActualRoi()
    {
        var dose = new DoseVolume
        {
            Dose = new double[1, 1, 1]
            {
                { { 10.0 } }
            },
            SpacingX = 1.0,
            SpacingY = 1.0,
            SpacingZ = 1.0,
            OriginX = 0.0,
            OriginY = 0.0,
            OriginZ = 0.0,
            ZPositions = [0.0]
        };

        double[] square =
        [
            -0.1, -0.1, 0.0,
             1.1, -0.1, 0.0,
             1.1,  1.1, 0.0,
            -0.1,  1.1, 0.0
        ];

        var request = new PlanAnalysisRequest
        {
            Plan = new PlanData
            {
                PatientId = "SYNTHETIC",
                Fractions = 30,
                DosePerFraction = 2.0,
                Dose = dose
            },
            StructureNames = new Dictionary<int, string>
            {
                [14] = "patient"
            },
            Contours = new Dictionary<int, List<double[]>>
            {
                [14] = [square]
            },
            Criteria =
            [
                new DoseCriterion
                {
                    StructureName = "patient(Unsp.Tiss.)",
                    Type = CriterionType.DxxPercent,
                    DxPercent = 2.0,
                    Operator = "<=",
                    Limit = 64.2,
                    Raw = "D2% <= 64.2 Gy (+1.8 Gy)"
                }
            ]
        };

        PlanAnalysisResult result =
            new PlanAnalysisService().Analyze(request);

        ClinicalCriterionEvaluation criterion =
            Assert.Single(result.ClinicalCriteria);

        Assert.Equal("patient", criterion.MatchedStructureName);
        Assert.Equal(10.0, criterion.Value, precision: 12);
        Assert.True(criterion.Pass);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void AnalysisService_AmbiguousPartialCriterionMatchFailsSafely()
    {
        var dose = new DoseVolume
        {
            Dose = new double[1, 1, 1]
            {
                { { 10.0 } }
            },
            SpacingX = 1.0,
            SpacingY = 1.0,
            SpacingZ = 1.0,
            OriginX = 0.0,
            OriginY = 0.0,
            OriginZ = 0.0,
            ZPositions = [0.0]
        };

        double[] square =
        [
            -0.1, -0.1, 0.0,
             1.1, -0.1, 0.0,
             1.1,  1.1, 0.0,
            -0.1,  1.1, 0.0
        ];

        var request = new PlanAnalysisRequest
        {
            Plan = new PlanData
            {
                PatientId = "SYNTHETIC",
                Fractions = 30,
                DosePerFraction = 2.0,
                Dose = dose
            },
            StructureNames = new Dictionary<int, string>
            {
                [1] = "Parotid_L",
                [2] = "Parotid_R"
            },
            Contours = new Dictionary<int, List<double[]>>
            {
                [1] = [square],
                [2] = [square]
            },
            Criteria =
            [
                new DoseCriterion
                {
                    StructureName = "Parotid",
                    Type = CriterionType.Dmean,
                    Operator = "<=",
                    Limit = 26.0,
                    Raw = "Dmean <= 26 Gy"
                }
            ]
        };

        PlanAnalysisResult result =
            new PlanAnalysisService().Analyze(request);

        Assert.Empty(result.Structures);
        Assert.Empty(result.ClinicalCriteria);

        Assert.Contains(
            result.Warnings,
            warning =>
                warning.Contains(
                    "Ambiguous RTSTRUCT match",
                    StringComparison.Ordinal));
    }

}
