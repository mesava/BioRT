using BioRT.Core.Models;
using BioRT.Core.Radiobiology;

namespace BioRT.Core.Analysis;

public sealed class PlanAnalysisRequest
{
    public required PlanData Plan { get; init; }

    public required IReadOnlyDictionary<int, string> StructureNames { get; init; }

    public required IReadOnlyDictionary<int, List<double[]>> Contours { get; init; }

    public IReadOnlyList<DoseCriterion> Criteria { get; init; } =
        Array.Empty<DoseCriterion>();

    public ClinicalContext? ClinicalContext { get; init; }

    public BioRT.Core.Matching.StructureMatcher? StructureMatcher { get; init; }

    public NtcpModelLibrary? NtcpLibrary { get; init; }

    public TcpModelLibrary? TcpLibrary { get; init; }
}

public sealed class PlanAnalysisResult
{
    public required IReadOnlyList<StructureAnalysisResult> Structures { get; init; }

    public required IReadOnlyList<PtvAnalysisResult> PtvMetrics { get; init; }

    public required IReadOnlyList<ClinicalCriterionEvaluation> ClinicalCriteria { get; init; }

    public required IReadOnlyList<NtcpAnalysisResult> Ntcp { get; init; }

    public TcpAnalysisResult? Tcp { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed class StructureAnalysisResult
{
    public required string Name { get; init; }
    public required double VolumeCc { get; init; }
    public required StructureDVH Dvh { get; init; }
}

public sealed class PtvAnalysisResult
{
    public required string CriterionStructureName { get; init; }
    public required string MatchedStructureName { get; init; }
    public required double PrescriptionDoseGy { get; init; }
    public required double VolumeCc { get; init; }
    public required double D2Gy { get; init; }
    public required double D98Gy { get; init; }
    public required double D95Gy { get; init; }
    public required double D50Gy { get; init; }
    public required double Hi { get; init; }
    public required double Ci { get; init; }
    public required double Gi { get; init; }
}

public sealed class ClinicalCriterionEvaluation
{
    public required string CriterionStructureName { get; init; }
    public required string MatchedStructureName { get; init; }
    public required DoseCriterion Criterion { get; init; }
    public required double Value { get; init; }
    public required bool Pass { get; init; }
}

public sealed class NtcpAnalysisResult
{
    public string? StructureName { get; init; }
    public string? CanonicalStructure { get; init; }
    public required NtcpEvaluationResult Result { get; init; }
    public required bool ReferenceOnly { get; init; }
}

public sealed class TcpAnalysisResult
{
    public string? TargetStructureName { get; init; }
    public double? TargetVolumeCc { get; init; }
    public required TcpEvaluationResult Result { get; init; }
}
