using BioRT.Core.Models;

namespace BioRT.IO.Dicom;

public sealed class DicomImportResult
{
    public required PlanData Plan { get; init; }

    public required IReadOnlyDictionary<int, string> StructureNames { get; init; }

    public required IReadOnlyDictionary<int, List<double[]>> Contours { get; init; }

    // Typed ROI topology for XOR-aware Web/Console analysis.
    public IReadOnlyDictionary<int, RoiContours> RoiContours { get; init; } =
        new Dictionary<int, RoiContours>();

    public required IReadOnlyList<string> SourceFiles { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}
