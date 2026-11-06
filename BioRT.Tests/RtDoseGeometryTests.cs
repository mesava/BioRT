using BioRT.IO.Dicom;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using Xunit;

namespace BioRT.Tests;

public sealed class RtDoseGeometryTests
{
    [Fact]
    public void Reader_UsesColumnThenRowSpacingAndRelativeZ()
    {
        DicomFile file = MakeDose(
            imageZ: 40.0,
            offsets: [0.0, 2.0],
            pixelSpacing: [2.0, 3.0]);

        var dose = new RtDoseReader().Read(file);

        Assert.Equal(3.0, dose.SpacingX, precision: 12);
        Assert.Equal(2.0, dose.SpacingY, precision: 12);
        Assert.Equal(2.0, dose.SpacingZ, precision: 12);
        Assert.Equal(new double[] { 40.0, 42.0 }, dose.ZPositions);
        Assert.Equal(20.0, dose.Dose[0, 0, 0], precision: 12);
        Assert.Equal(20.0, dose.Dose[0, 0, 1], precision: 12);
    }

    [Fact]
    public void Reader_RecognizesAbsoluteAxialZWithoutAddingOriginTwice()
    {
        DicomFile file = MakeDose(
            imageZ: 40.0,
            offsets: [40.0, 42.0]);

        var dose = new RtDoseReader().Read(file);

        Assert.Equal(new double[] { 40.0, 42.0 }, dose.ZPositions);
    }

    [Fact]
    public void Reader_RejectsObliqueOrRotatedDoseGrid()
    {
        DicomFile file = MakeDose(
            imageZ: 40.0,
            offsets: [0.0, 2.0],
            orientation: [0.0, 1.0, 0.0, -1.0, 0.0, 0.0]);

        NotSupportedException error =
            Assert.Throws<NotSupportedException>(
                () => new RtDoseReader().Read(file));

        Assert.Contains(
            "ImageOrientationPatient",
            error.Message);
    }

    [Fact]
    public void Reader_RejectsUnknownGridFrameOffsetConvention()
    {
        DicomFile file = MakeDose(
            imageZ: 40.0,
            offsets: [5.0, 7.0]);

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(
                () => new RtDoseReader().Read(file));

        Assert.Contains(
            "GridFrameOffsetVector",
            error.Message);
    }

    [Fact]
    public void Reader_RejectsNonuniformFrameSpacingUntilVolumeAlgorithmSupportsIt()
    {
        DicomFile file = MakeDose(
            imageZ: 40.0,
            offsets: [0.0, 2.0, 5.0]);

        NotSupportedException error =
            Assert.Throws<NotSupportedException>(
                () => new RtDoseReader().Read(file));

        Assert.Contains(
            "Non-uniform",
            error.Message);
    }

    private static DicomFile MakeDose(
        double imageZ,
        double[] offsets,
        double[]? pixelSpacing = null,
        double[]? orientation = null)
    {
        var ds =
            new DicomDataset(
                DicomTransferSyntax.ExplicitVRLittleEndian);

        ds.Add(DicomTag.Modality, "RTDOSE");
        ds.Add(DicomTag.SOPClassUID, DicomUID.RTDoseStorage);
        ds.Add(DicomTag.SOPInstanceUID, DicomUID.Generate());
        ds.Add(DicomTag.DoseUnits, "GY");
        ds.Add(DicomTag.DoseType, "PHYSICAL");
        ds.Add(DicomTag.DoseSummationType, "PLAN");
        ds.Add(DicomTag.DoseGridScaling, 0.01);

        ds.Add(DicomTag.Rows, (ushort)2);
        ds.Add(DicomTag.Columns, (ushort)2);
        ds.Add(
            DicomTag.PixelSpacing,
            pixelSpacing ?? [1.0, 1.0]);
        ds.Add(DicomTag.ImagePositionPatient, 10.0, 20.0, imageZ);
        ds.Add(
            DicomTag.ImageOrientationPatient,
            orientation ?? [1.0, 0.0, 0.0, 0.0, 1.0, 0.0]);
        ds.Add(DicomTag.GridFrameOffsetVector, offsets);

        ds.Add(DicomTag.SamplesPerPixel, (ushort)1);
        ds.Add(DicomTag.PhotometricInterpretation, "MONOCHROME2");
        ds.Add(DicomTag.BitsAllocated, (ushort)16);
        ds.Add(DicomTag.BitsStored, (ushort)16);
        ds.Add(DicomTag.HighBit, (ushort)15);
        ds.Add(DicomTag.PixelRepresentation, (ushort)0);

        DicomPixelData pixelData =
            DicomPixelData.Create(ds, true);

        // 2000 * 0.01 = 20 Gy at every voxel.
        byte[] frame =
        [
            0xD0, 0x07, 0xD0, 0x07,
            0xD0, 0x07, 0xD0, 0x07
        ];

        for (int i = 0; i < offsets.Length; i++)
            pixelData.AddFrame(
                new MemoryByteBuffer((byte[])frame.Clone()));

        return new DicomFile(ds);
    }
}
