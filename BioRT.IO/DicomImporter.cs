using FellowOakDicom;
using BioRT.Core.Models;

namespace BioRT.IO.Dicom;

public class DicomImporter
{
    private readonly RtDoseReader _doseReader = new();

    public PlanData Load(string folder)
    {
        var plan = new PlanData();

        // Берём ВСЕ файлы (любые расширения)
        var files = Directory.GetFiles(
            folder, "*", SearchOption.AllDirectories);

        Console.WriteLine($"Found {files.Length} files.");

        foreach (var f in files)
        {
            try
            {
                var dicom = DicomFile.Open(f);

                var ds = dicom.Dataset;

                var sop = ds.GetSingleValueOrDefault(
                    DicomTag.SOPClassUID, "");

                // ================= RTSTRUCT =================
                else if (sop == DicomUID.RTStructureSetStorage.UID)
                {
                    Console.WriteLine($"RTSTRUCT : {Path.GetFileName(f)}");
                    ReadStruct(dicom, plan);
                }
            }
            catch
            {
                // Не DICOM → пропускаем
                continue;
            }
        }

        return plan;
    }


    // ================= RTPLAN =================

    private void ReadPlan(DicomFile f, PlanData plan)
    {
        var ds = f.Dataset;

        plan.PatientId =
            ds.GetSingleValueOrDefault(
                DicomTag.PatientID, "Unknown");

        int fractions = 0;
        double totalDose = 0;


        // ================= FractionGroupSequence =================
        if (ds.Contains(DicomTag.FractionGroupSequence))
        {
            var fgSeq =
                ds.GetSequence(DicomTag.FractionGroupSequence);

            if (fgSeq.Items.Count > 0)
            {
                var fg = fgSeq.Items[0];

                fractions =
                    fg.GetSingleValueOrDefault(
                        DicomTag.NumberOfFractionsPlanned, 0);
            }
        }


        // ================= DoseReferenceSequence =================
        if (ds.Contains(DicomTag.DoseReferenceSequence))
        {
            var drSeq =
                ds.GetSequence(DicomTag.DoseReferenceSequence);

            foreach (var item in drSeq.Items)
            {
                string type =
                    item.GetSingleValueOrDefault(
                        DicomTag.DoseReferenceType, "");

                // Берём TARGET
                if (type == "TARGET")
                {
                    totalDose =
                        item.GetSingleValueOrDefault(
                            DicomTag.TargetPrescriptionDose, 0.0);

                    if (totalDose > 0)
                        break;
                }
            }
        }


        // ================= Fallback =================
        if (fractions == 0)
        {
            fractions =
                ds.GetSingleValueOrDefault(
                    DicomTag.NumberOfFractionsPlanned, 0);
        }

        if (totalDose == 0)
        {
            totalDose =
                ds.GetSingleValueOrDefault(
                    DicomTag.TargetPrescriptionDose, 0.0);
        }


        // ================= Save =================
        plan.Fractions = fractions;

        if (fractions > 0 && totalDose > 0)
        {
            plan.DosePerFraction = totalDose / fractions;
        }
    }


    // ================= RTSTRUCT =================

    private void ReadStruct(DicomFile f, PlanData plan)
    {
        // Реализуем позже
    }
}
