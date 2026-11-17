using BioRT.IO.Dicom;
using FellowOakDicom;
using Xunit;

namespace BioRT.Tests;

/// <summary>
/// Prevents silent RTSTRUCT/RTDOSE coordinate mixing without a DICOM
/// spatial registration. No real patient identifiers are used.
/// </summary>
public sealed class DicomFrameOfReferenceGuardTests
{
    private const string DoseFrameUid =
        "1.2.826.0.1.3680043.10.543.77";

    private const string OtherFrameUid =
        "1.2.826.0.1.3680043.10.543.88";

    [Fact]
    public void MatchingDoseStructAndRoiFrames_AreAccepted()
    {
        DicomFrameOfReferenceGuard.Validate(
            Dose(DoseFrameUid),
            Struct(
                topLevelUid: DoseFrameUid,
                roiUid: DoseFrameUid,
                referenceUid: DoseFrameUid));
    }

    [Fact]
    public void DifferentTopLevelStructFrame_IsRejected()
    {
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() =>
                DicomFrameOfReferenceGuard.Validate(
                    Dose(DoseFrameUid),
                    Struct(
                        topLevelUid: OtherFrameUid,
                        roiUid: DoseFrameUid,
                        referenceUid: DoseFrameUid)));

        Assert.Contains("mismatch", exception.Message);
    }

    [Fact]
    public void DifferentIndividualRoiFrame_IsRejectedEvenWhenTopMatches()
    {
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() =>
                DicomFrameOfReferenceGuard.Validate(
                    Dose(DoseFrameUid),
                    Struct(
                        topLevelUid: DoseFrameUid,
                        roiUid: OtherFrameUid,
                        referenceUid: DoseFrameUid)));

        Assert.Contains("ROI 7", exception.Message);
    }

    [Fact]
    public void MissingDoseFrame_IsRejected()
    {
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() =>
                DicomFrameOfReferenceGuard.Validate(
                    Dose(null),
                    Struct(
                        topLevelUid: DoseFrameUid,
                        roiUid: DoseFrameUid,
                        referenceUid: DoseFrameUid)));

        Assert.Contains("RTDOSE", exception.Message);
    }

    [Fact]
    public void MissingRoiFrame_IsRejected()
    {
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() =>
                DicomFrameOfReferenceGuard.Validate(
                    Dose(DoseFrameUid),
                    Struct(
                        topLevelUid: DoseFrameUid,
                        roiUid: null,
                        referenceUid: DoseFrameUid)));

        Assert.Contains("ROI 7", exception.Message);
    }

    [Fact]
    public void DifferentReferencedSequenceFrame_IsRejected()
    {
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() =>
                DicomFrameOfReferenceGuard.Validate(
                    Dose(DoseFrameUid),
                    Struct(
                        topLevelUid: DoseFrameUid,
                        roiUid: DoseFrameUid,
                        referenceUid: OtherFrameUid)));

        Assert.Contains("ReferencedFrameOfReferenceSequence", exception.Message);
    }

    [Fact]
    public void MissingOptionalTopLevelStructUid_WithRoiUid_IsAccepted()
    {
        DicomFrameOfReferenceGuard.Validate(
            Dose(DoseFrameUid),
            Struct(
                topLevelUid: null,
                roiUid: DoseFrameUid,
                referenceUid: DoseFrameUid));
    }

    private static DicomFile Dose(string? frameUid)
    {
        var ds = new DicomDataset(
            DicomTransferSyntax.ExplicitVRLittleEndian);
        ds.Add(DicomTag.Modality, "RTDOSE");
        ds.Add(DicomTag.SOPClassUID, DicomUID.RTDoseStorage);
        ds.Add(DicomTag.SOPInstanceUID, DicomUID.Generate());

        if (frameUid is not null)
            ds.Add(DicomTag.FrameOfReferenceUID, frameUid);

        return new DicomFile(ds);
    }

    private static DicomFile Struct(
        string? topLevelUid,
        string? roiUid,
        string? referenceUid)
    {
        var ds = new DicomDataset(
            DicomTransferSyntax.ExplicitVRLittleEndian);

        ds.Add(DicomTag.Modality, "RTSTRUCT");
        ds.Add(DicomTag.SOPClassUID, DicomUID.RTStructureSetStorage);
        ds.Add(DicomTag.SOPInstanceUID, DicomUID.Generate());

        if (topLevelUid is not null)
            ds.Add(DicomTag.FrameOfReferenceUID, topLevelUid);

        var roi = new DicomDataset();
        roi.Add(DicomTag.ROINumber, 7);
        roi.Add(DicomTag.ROIName, "SYNTHETIC_ROI");

        if (roiUid is not null)
            roi.Add(DicomTag.ReferencedFrameOfReferenceUID, roiUid);

        ds.Add(
            new DicomSequence(
                DicomTag.StructureSetROISequence,
                roi));

        if (referenceUid is not null)
        {
            var reference = new DicomDataset();
            reference.Add(DicomTag.FrameOfReferenceUID, referenceUid);
            ds.Add(
                new DicomSequence(
                    DicomTag.ReferencedFrameOfReferenceSequence,
                    reference));
        }

        return new DicomFile(ds);
    }
}
