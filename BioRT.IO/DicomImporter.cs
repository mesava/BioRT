using BioRT.Core.Models;
using FellowOakDicom;

namespace BioRT.IO.Dicom;

/// <summary>
/// Legacy folder-based importer used by BioRT.App.
///
/// Browser and stream callers should prefer DicomBundleImporter. Both paths
/// share the same RTPLAN / RTDOSE readers so the scientific inputs remain
/// consistent.
/// </summary>
public sealed class DicomImporter
{
    private readonly RtPlanReader _planReader = new();
    private readonly RtDoseReader _doseReader = new();

    public PlanData Load(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("Folder path must not be empty.", nameof(folder));

        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException(folder);

        var plan = new PlanData();
        DicomFile? rtDoseFile = null;
        DicomFile? rtStructFile = null;

        string[] files = Directory.GetFiles(
            folder,
            "*",
            SearchOption.AllDirectories);

        Console.WriteLine($"Found {files.Length} files.");

        foreach (string path in files)
        {
            try
            {
                var dicom = DicomFile.Open(
                    path,
                    FileReadOption.ReadAll);

                string sop =
                    dicom.Dataset.GetSingleValueOrDefault(
                        DicomTag.SOPClassUID,
                        "");

                if (sop == DicomUID.RTPlanStorage.UID)
                {
                    Console.WriteLine(
                        $"RTPLAN   : {Path.GetFileName(path)}");

                    _planReader.Read(
                        dicom,
                        plan);
                }
                else if (sop == DicomUID.RTDoseStorage.UID)
                {
                    Console.WriteLine(
                        $"RTDOSE   : {Path.GetFileName(path)}");

                    plan.Dose =
                        _doseReader.Read(dicom);

                    rtDoseFile = dicom;
                }
                else if (sop == DicomUID.RTStructureSetStorage.UID)
                {
                    Console.WriteLine(
                        $"RTSTRUCT : {Path.GetFileName(path)}");

                    rtStructFile = dicom;
                }
            }
            catch
            {
                // Not DICOM (criteria JSON, logs, etc.) -> skip.
                continue;
            }
        }

        if (rtDoseFile != null && rtStructFile != null)
        {
            // Do this OUTSIDE the permissive file-discovery catch above:
            // geometry safety errors must not be skipped as non-DICOM.
            DicomFrameOfReferenceGuard.Validate(
                rtDoseFile,
                rtStructFile);
        }

        return plan;
    }
}
