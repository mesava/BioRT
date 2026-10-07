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

        ClinicalCriterionEvaluation criterion =
            Assert.Single(result.ClinicalCriteria);

        Assert.Equal("OAR", criterion.MatchedStructureName);
        Assert.Equal(60.0, criterion.Value, precision: 12);
        Assert.True(criterion.Pass);

        Assert.Empty(result.Ntcp);
        Assert.Null(result.Tcp);
    }
}
