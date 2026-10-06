using FellowOakDicom;
using FellowOakDicom.Imaging;
using BioRT.Core.Models;

namespace BioRT.IO.Dicom;

public class RtDoseReader
{
    public DoseVolume Read(DicomFile file)
    {
        var ds = file.Dataset;

        // --- Scaling ---
        double scaling =
            ds.GetSingleValue<double>(DicomTag.DoseGridScaling);

        // --- Dimensions ---
        int rows = ds.GetSingleValue<int>(DicomTag.Rows);
        int cols = ds.GetSingleValue<int>(DicomTag.Columns);

        int frames =
            ds.GetSingleValueOrDefault(DicomTag.NumberOfFrames, 1);

        // --- Pixel data ---
        var pixelData = DicomPixelData.Create(ds);

        // --- Spacing XY ---
        var pixelSpacing =
            ds.GetValues<double>(DicomTag.PixelSpacing);

        double dx = pixelSpacing[0];
        double dy = pixelSpacing[1];

        // --- Image Position Patient (FIRST frame) ---
        var ipp =
            ds.GetValues<double>(DicomTag.ImagePositionPatient);

        double originX = ipp[0];
        double originY = ipp[1];
        double originZ = ipp[2];

        // --- GridFrameOffsetVector (Z offsets for EACH frame) ---
        var zOffsets =
            ds.GetValues<double>(DicomTag.GridFrameOffsetVector);

        if (zOffsets.Length != frames)
            throw new InvalidOperationException(
                "GridFrameOffsetVector length does not match NumberOfFrames");

        // --- Absolute Z positions ---
        var zPositions = new double[frames];
        for (int k = 0; k < frames; k++)
            zPositions[k] = originZ + zOffsets[k];

        // --- Dose array ---
        var dose = new double[cols, rows, frames];

        // --- Read frames ---
        for (int k = 0; k < frames; k++)
        {
            var buffer = pixelData.GetFrame(k);
            byte[] bytes = buffer.Data;

            int index = 0;

            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    ushort raw =
                        BitConverter.ToUInt16(bytes, index);

                    index += 2;

                    dose[x, y, k] = raw * scaling;
                }
        }

        return new DoseVolume
        {
            Dose = dose,

            SpacingX = dx,
            SpacingY = dy,
            SpacingZ = zOffsets.Length > 1
                ? Math.Abs(zOffsets[1] - zOffsets[0])
                : 1.0, // только для информации

            OriginX = originX,
            OriginY = originY,
            OriginZ = originZ,

            ZPositions = zPositions
        };
    }
}
