using System.Buffers.Binary;
using BioRT.IO.Dicom;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using Xunit;

namespace BioRT.Tests;

/// <summary>
/// RTDOSE pixels must be decoded according to DICOM BitsAllocated.
/// The Stanley SRS analytical dataset uses UINT32, 2E-05 Gy/scaled unit.
/// Tests are purely synthetic; no publisher DICOM binary is committed.
/// </summary>
public sealed class RtDosePixelFormatTests
{
    [Fact]
    public void Unsigned32BitStanleyLikeSamples_AreNotTruncatedToUInt16()
    {
        DicomFile file = MakeDose(
            bitsAllocated: 32,
            representation: 0,
            scaling: 2e-5,
            frames:
            [
                [250000, 1320243, 1000000, 275000],
                [1000000, 250000, 500000, 750000]
            ]);

        var volume = new RtDoseReader().Read(file);

        Assert.Equal(2, volume.SizeZ);
        Assert.Equal(5.0, volume.Dose[0, 0, 0], precision: 9);
        Assert.Equal(26.40486, volume.Dose[1, 0, 0], precision: 9);
        Assert.Equal(20.0, volume.Dose[0, 1, 0], precision: 9);
        Assert.Equal(5.5, volume.Dose[1, 1, 0], precision: 9);

        Assert.Equal(20.0, volume.Dose[0, 0, 1], precision: 9);
        Assert.Equal(5.0, volume.Dose[1, 0, 1], precision: 9);
        Assert.Equal(10.0, volume.Dose[0, 1, 1], precision: 9);
        Assert.Equal(15.0, volume.Dose[1, 1, 1], precision: 9);
    }

    [Fact]
    public void Unsigned16BitExistingBeamCase_RemainsCorrect()
    {
        DicomFile file = MakeDose(
            bitsAllocated: 16,
            representation: 0,
            scaling: 0.01,
            frames: [[2000, 3000, 4000, 5000]]);

        var volume = new RtDoseReader().Read(file);

        Assert.Equal(20.0, volume.Dose[0, 0, 0], precision: 9);
        Assert.Equal(30.0, volume.Dose[1, 0, 0], precision: 9);
        Assert.Equal(40.0, volume.Dose[0, 1, 0], precision: 9);
        Assert.Equal(50.0, volume.Dose[1, 1, 0], precision: 9);
    }

    [Fact]
    public void SignedPixelRepresentation_FailsClosed()
    {
        DicomFile file = MakeDose(
            bitsAllocated: 32,
            representation: 1,
            scaling: 0.00001,
            frames: [[1, 2, 3, 4]]);

        NotSupportedException error =
            Assert.Throws<NotSupportedException>(() =>
                new RtDoseReader().Read(file));

        Assert.Contains("PixelRepresentation=1", error.Message);
    }

    [Fact]
    public void Unsupported8BitSamples_FailClosed()
    {
        DicomFile file = MakeDose(
            bitsAllocated: 8,
            representation: 0,
            scaling: 0.01,
            frames: [[1, 2, 3, 4]]);

        Assert.Throws<NotSupportedException>(() =>
            new RtDoseReader().Read(file));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.01)]
    [InlineData(double.NaN)]
    public void InvalidDoseGridScaling_IsRejected(double scaling)
    {
        DicomFile file = MakeDose(
            bitsAllocated: 32,
            representation: 0,
            scaling: scaling,
            frames: [[1, 2, 3, 4]]);

        Assert.Throws<InvalidOperationException>(() =>
            new RtDoseReader().Read(file));
    }

    private static DicomFile MakeDose(
        int bitsAllocated,
        int representation,
        double scaling,
        uint[][] frames)
    {
        var ds =
            new DicomDataset(
                DicomTransferSyntax.ImplicitVRLittleEndian);

        ds.Add(DicomTag.Modality, "RTDOSE");
        ds.Add(DicomTag.SOPClassUID, DicomUID.RTDoseStorage);
        ds.Add(DicomTag.SOPInstanceUID, DicomUID.Generate());
        ds.Add(DicomTag.DoseUnits, "GY");
        ds.Add(DicomTag.DoseType, "PHYSICAL");
        ds.Add(DicomTag.DoseSummationType, "PLAN");
        ds.Add(DicomTag.DoseGridScaling, scaling);
        ds.Add(DicomTag.Rows, (ushort)2);
        ds.Add(DicomTag.Columns, (ushort)2);
        ds.Add(DicomTag.PixelSpacing, 1.0, 1.0);
        ds.Add(DicomTag.ImagePositionPatient, 0.0, 0.0, -10.0);
        ds.Add(
            DicomTag.ImageOrientationPatient,
            1.0, 0.0, 0.0, 0.0, 1.0, 0.0);

        ds.Add(
            DicomTag.GridFrameOffsetVector,
            Enumerable.Range(0, frames.Length)
                .Select(i => (double)i)
                .ToArray());

        ds.Add(DicomTag.SamplesPerPixel, (ushort)1);
        ds.Add(DicomTag.PhotometricInterpretation, "MONOCHROME2");
        ds.Add(DicomTag.BitsAllocated, (ushort)bitsAllocated);
        ds.Add(DicomTag.BitsStored, (ushort)bitsAllocated);
        ds.Add(DicomTag.HighBit, (ushort)(bitsAllocated - 1));
        ds.Add(DicomTag.PixelRepresentation, (ushort)representation);

        DicomPixelData pixels = DicomPixelData.Create(ds, true);

        foreach (uint[] values in frames)
        {
            var bytes = new byte[values.Length * bitsAllocated / 8];

            for (int i = 0; i < values.Length; i++)
            {
                if (bitsAllocated == 32)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(
                        bytes.AsSpan(i * 4, 4),
                        values[i]);
                }
                else if (bitsAllocated == 16)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(
                        bytes.AsSpan(i * 2, 2),
                        checked((ushort)values[i]));
                }
                else
                {
                    bytes[i] = checked((byte)values[i]);
                }
            }

            pixels.AddFrame(new MemoryByteBuffer(bytes));
        }

        return new DicomFile(ds);
    }
}
