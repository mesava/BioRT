using BioRT.Core.Models;

namespace BioRT.IO.Dicom;

public sealed class DicomImportResult
{
    public required PlanData Plan { get; init; }

    public required IReadOnlyDictionary<int, string> StructureNames { get; init; }

    public required IReadOnlyDictionary<int, List<double[]>> Contours { get; init; }

    public required IReadOnlyList<string> SourceFiles { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}
