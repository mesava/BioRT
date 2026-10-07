using BioRT.Core.Models;
using FellowOakDicom;

namespace BioRT.IO.Dicom;

/// <summary>
/// Platform-neutral DICOM RT importer.
///
/// Unlike DicomImporter, this class does not require a physical directory or
/// Windows file-system access. Every input is supplied as a stream factory,
/// which makes the same importer usable by local files, ZIP entries and a
/// future browser/Blazor file picker.
/// </summary>
public sealed class DicomBundleImporter
{
    private readonly RtPlanReader _planReader = new();
    private readonly RtDoseReader _doseReader = new();
    private readonly RtStructReader _structReader = new();

    public async Task<DicomImportResult> LoadAsync(
        IEnumerable<DicomInputFile> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var candidates = inputs.ToArray();

        if (candidates.Length == 0)
            throw new InvalidOperationException("No input files were supplied.");

        var rtPlans = new List<(string Name, DicomFile File)>();
        var rtDoses = new List<(string Name, DicomFile File)>();
        var rtStructs = new List<(string Name, DicomFile File)>();
        var warnings = new List<string>();

        foreach (var input in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using Stream stream =
                    await input.OpenReadAsync(cancellationToken);

                DicomFile dicom =
                    await DicomFile.OpenAsync(
                        stream,
                        FileReadOption.ReadAll);

                string sop =
                    dicom.Dataset.GetSingleValueOrDefault(
                        DicomTag.SOPClassUID,
                        "");

                if (sop == DicomUID.RTPlanStorage.UID)
                {
                    rtPlans.Add((input.Name, dicom));
                }
                else if (sop == DicomUID.RTDoseStorage.UID)
                {
                    rtDoses.Add((input.Name, dicom));
                }
                else if (sop == DicomUID.RTStructureSetStorage.UID)
                {
                    rtStructs.Add((input.Name, dicom));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (
                ex is DicomFileException ||
                ex is IOException ||
                ex is InvalidDataException)
            {
                warnings.Add(
                    $"Skipped non-DICOM or unreadable file '{input.Name}': {ex.Message}");
            }
        }

        EnsureExactlyOne(rtPlans, "RTPLAN");
        EnsureExactlyOne(rtDoses, "RTDOSE");
        EnsureExactlyOne(rtStructs, "RTSTRUCT");

        var plan = new PlanData();

        _planReader.Read(
            rtPlans[0].File,
            plan);

        plan.Dose =
            _doseReader.Read(
                rtDoses[0].File);

        var structureNames =
            _structReader.ReadStructureNames(
                rtStructs[0].File);

        var contours =
            _structReader.ReadContours(
                rtStructs[0].File);

        return new DicomImportResult
        {
            Plan = plan,
            StructureNames = structureNames,
            Contours = contours,
            SourceFiles = new[]
            {
                rtPlans[0].Name,
                rtDoses[0].Name,
                rtStructs[0].Name
            },
            Warnings = warnings
        };
    }

    private static void EnsureExactlyOne(
        IReadOnlyCollection<(string Name, DicomFile File)> files,
        string dicomKind)
    {
        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                $"Required {dicomKind} was not found.");
        }

        if (files.Count > 1)
        {
            string names =
                string.Join(
                    ", ",
                    files.Select(x => x.Name));

            throw new InvalidOperationException(
                $"Ambiguous input: found {files.Count} {dicomKind} files ({names}). " +
                "Select one coherent RTPLAN / RTDOSE / RTSTRUCT set.");
        }
    }
}
