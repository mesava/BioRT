using FellowOakDicom;

namespace BioRT.IO.Dicom;

/// <summary>
/// Prevents RTDOSE voxels from being assigned RTSTRUCT contours belonging
/// to another patient coordinate system. BioRT does not perform spatial
/// registration between different DICOM Frames of Reference.
/// </summary>
public static class DicomFrameOfReferenceGuard
{
    public static void Validate(DicomFile rtDose, DicomFile rtStruct)
    {
        ArgumentNullException.ThrowIfNull(rtDose);
        ArgumentNullException.ThrowIfNull(rtStruct);

        string doseUid =
            rtDose.Dataset.GetSingleValueOrDefault(
                DicomTag.FrameOfReferenceUID,
                "");

        if (string.IsNullOrWhiteSpace(doseUid))
        {
            throw new InvalidOperationException(
                "RTDOSE is missing FrameOfReferenceUID; " +
                "dose-to-contour alignment cannot be verified.");
        }

        // Some RTSTRUCT exporters omit the optional top-level reference;
        // individual ROI references in StructureSetROISequence are required.
        string structureUid =
            rtStruct.Dataset.GetSingleValueOrDefault(
                DicomTag.FrameOfReferenceUID,
                "");

        if (!string.IsNullOrWhiteSpace(structureUid) &&
            !string.Equals(
                structureUid,
                doseUid,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "RTSTRUCT and RTDOSE FrameOfReferenceUID mismatch. " +
                "Spatial registration has not been performed.");
        }

        DicomSequence roiSequence =
            rtStruct.Dataset.GetSequence(
                DicomTag.StructureSetROISequence);

        if (roiSequence.Items.Count == 0)
        {
            throw new InvalidOperationException(
                "RTSTRUCT has no StructureSetROISequence items.");
        }

        foreach (DicomDataset roi in roiSequence.Items)
        {
            int number =
                roi.GetSingleValue<int>(DicomTag.ROINumber);

            string roiUid =
                roi.GetSingleValueOrDefault(
                    DicomTag.ReferencedFrameOfReferenceUID,
                    "");

            if (string.IsNullOrWhiteSpace(roiUid))
            {
                throw new InvalidOperationException(
                    $"RTSTRUCT ROI {number} lacks " +
                    "ReferencedFrameOfReferenceUID.");
            }

            if (!string.Equals(
                    roiUid,
                    doseUid,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"RTSTRUCT ROI {number} references a FrameOfReferenceUID " +
                    "different from RTDOSE. Unregistered geometry is unsafe.");
            }
        }

        if (rtStruct.Dataset.Contains(
                DicomTag.ReferencedFrameOfReferenceSequence))
        {
            DicomSequence references =
                rtStruct.Dataset.GetSequence(
                    DicomTag.ReferencedFrameOfReferenceSequence);

            foreach (DicomDataset referenced in references.Items)
            {
                string uid =
                    referenced.GetSingleValueOrDefault(
                        DicomTag.FrameOfReferenceUID,
                        "");

                if (string.IsNullOrWhiteSpace(uid) ||
                    !string.Equals(uid, doseUid, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "RTSTRUCT ReferencedFrameOfReferenceSequence " +
                        "contains a FrameOfReferenceUID that is missing " +
                        "or different from RTDOSE. BioRT has no " +
                        "registration transform for this data.");
                }
            }
        }
    }
}
