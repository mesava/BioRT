using BioRT.Core.Models;
using FellowOakDicom;

namespace BioRT.IO.Dicom;

public sealed class RtPlanReader
{
    public void Read(DicomFile file, PlanData plan)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(plan);

        var ds = file.Dataset;

        plan.PatientId =
            ds.GetSingleValueOrDefault(
                DicomTag.PatientID,
                "Unknown");

        int fractions = 0;
        double totalDose = 0.0;

        if (ds.Contains(DicomTag.FractionGroupSequence))
        {
            var fgSeq =
                ds.GetSequence(DicomTag.FractionGroupSequence);

            if (fgSeq.Items.Count > 0)
            {
                var fg = fgSeq.Items[0];

                fractions =
                    fg.GetSingleValueOrDefault(
                        DicomTag.NumberOfFractionsPlanned,
                        0);
            }
        }

        if (ds.Contains(DicomTag.DoseReferenceSequence))
        {
            var drSeq =
                ds.GetSequence(DicomTag.DoseReferenceSequence);

            foreach (var item in drSeq.Items)
            {
                string type =
                    item.GetSingleValueOrDefault(
                        DicomTag.DoseReferenceType,
                        "");

                if (!string.Equals(
                        type,
                        "TARGET",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                totalDose =
                    item.GetSingleValueOrDefault(
                        DicomTag.TargetPrescriptionDose,
                        0.0);

                if (totalDose > 0)
                    break;
            }
        }

        if (fractions == 0)
        {
            fractions =
                ds.GetSingleValueOrDefault(
                    DicomTag.NumberOfFractionsPlanned,
                    0);
        }

        if (totalDose == 0)
        {
            totalDose =
                ds.GetSingleValueOrDefault(
                    DicomTag.TargetPrescriptionDose,
                    0.0);
        }

        plan.Fractions = fractions;

        if (fractions > 0 && totalDose > 0)
            plan.DosePerFraction = totalDose / fractions;
    }
}
