using BioRT.Core.Models;
using FellowOakDicom;
using FellowOakDicom.Imaging;

namespace BioRT.IO.Dicom;

/// <summary>
/// Reads a conventional, axis-aligned RTDOSE grid.
///
/// Until full orientation-aware contour rasterization is implemented, a rotated
/// or oblique grid must be rejected rather than silently treated as axial.
/// DICOM references: PS3.3 C.7.6.2.1.1 and C.8.8.3.2.
/// </summary>
public sealed class RtDoseReader
{
    private const double GeometryToleranceMm = 1e-3;
    private const double DirectionTolerance = 1e-6;

    public DoseVolume Read(DicomFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        DicomDataset ds = file.Dataset;

        double scaling =
            ds.GetSingleValue<double>(DicomTag.DoseGridScaling);

        int rows = ds.GetSingleValue<int>(DicomTag.Rows);
        int cols = ds.GetSingleValue<int>(DicomTag.Columns);
        int frames =
            ds.GetSingleValueOrDefault(DicomTag.NumberOfFrames, 1);

        if (rows <= 0 || cols <= 0 || frames <= 0)
        {
            throw new InvalidOperationException(
                "RTDOSE must have positive Rows, Columns, and NumberOfFrames.");
        }

        double[] pixelSpacing =
            ds.GetValues<double>(DicomTag.PixelSpacing);

        if (pixelSpacing.Length != 2 ||
            pixelSpacing.Any(x => !double.IsFinite(x) || x <= 0))
        {
            throw new InvalidOperationException(
                "RTDOSE PixelSpacing must contain two positive finite values.");
        }

        // DICOM PixelSpacing = [row spacing, column spacing].
        // X indexes columns; Y indexes rows for the only orientation supported.
        double dx = pixelSpacing[1];
        double dy = pixelSpacing[0];

        double[] imageOrientation =
            ds.GetValues<double>(DicomTag.ImageOrientationPatient);

        if (imageOrientation.Length != 6 ||
            imageOrientation.Any(x => !double.IsFinite(x)))
        {
            throw new InvalidOperationException(
                "RTDOSE ImageOrientationPatient must contain six finite values.");
        }

        // MaskBuilder currently works in physical XY and accepts only these
        // direction cosines. Reject rotated/flipped grids until its full
        // patient-to-grid transform is implemented and validated.
        double[] expectedAxialOrientation = [1, 0, 0, 0, 1, 0];

        if (imageOrientation.Where(
                (value, index) =>
                    Math.Abs(value - expectedAxialOrientation[index]) >
                    DirectionTolerance).Any())
        {
            throw new NotSupportedException(
                "RTDOSE has a non-standard or oblique ImageOrientationPatient. " +
                "BioRT currently supports only axial [1,0,0,0,1,0] RTDOSE; " +
                "the dose grid cannot be safely aligned to RTSTRUCT.");
        }

        double[] ipp =
            ds.GetValues<double>(DicomTag.ImagePositionPatient);

        if (ipp.Length != 3 ||
            ipp.Any(x => !double.IsFinite(x)))
        {
            throw new InvalidOperationException(
                "RTDOSE ImagePositionPatient must contain three finite coordinates.");
        }

        double originX = ipp[0];
        double originY = ipp[1];
        double originZ = ipp[2];

        double[] offsets =
            ds.Contains(DicomTag.GridFrameOffsetVector)
                ? ds.GetValues<double>(DicomTag.GridFrameOffsetVector)
                : frames == 1
                    ? [0.0]
                    : throw new InvalidOperationException(
                        "Multi-frame RTDOSE requires GridFrameOffsetVector.");

        if (offsets.Length != frames ||
            offsets.Any(x => !double.IsFinite(x)))
        {
            throw new InvalidOperationException(
                "GridFrameOffsetVector must contain one finite value per dose frame.");
        }

        // DICOM PS3.3 C.8.8.3.2 permits:
        // (a) first value 0: offsets relative to IPP;
        // (b) first value equals IPP.Z with axial IOP: absolute patient Z.
        bool relativeOffsets =
            Math.Abs(offsets[0]) <= GeometryToleranceMm;

        bool absoluteAxialZ =
            !relativeOffsets &&
            Math.Abs(offsets[0] - originZ) <= GeometryToleranceMm;

        if (!relativeOffsets && !absoluteAxialZ)
        {
            throw new InvalidOperationException(
                "GridFrameOffsetVector is neither relative to the first dose " +
                "plane nor valid absolute axial patient-Z coordinates.");
        }

        var zPositions = new double[frames];
        for (int k = 0; k < frames; k++)
        {
            zPositions[k] = relativeOffsets
                ? originZ + offsets[k]
                : offsets[k];
        }

        double spacingZ = 1.0;

        if (frames > 1)
        {
            double firstStep = zPositions[1] - zPositions[0];
            spacingZ = Math.Abs(firstStep);

            if (spacingZ <= GeometryToleranceMm)
            {
                throw new InvalidOperationException(
                    "RTDOSE Z positions must vary monotonically.");
            }

            for (int k = 2; k < frames; k++)
            {
                double step = zPositions[k] - zPositions[k - 1];

                // Current DVH/volume algorithms assume one fixed voxel
                // thickness. Non-uniform spacing is not silently averaged.
                if (Math.Abs(step - firstStep) > GeometryToleranceMm)
                {
                    throw new NotSupportedException(
                        "Non-uniform RTDOSE frame spacing is not yet " +
                        "supported by BioRT volume and DVH algorithms.");
                }
            }
        }

        DicomPixelData pixelData = DicomPixelData.Create(ds);
        var dose = new double[cols, rows, frames];

        for (int k = 0; k < frames; k++)
        {
            byte[] bytes = pixelData.GetFrame(k).Data;
            int index = 0;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    ushort raw = BitConverter.ToUInt16(bytes, index);
                    index += sizeof(ushort);
                    dose[x, y, k] = raw * scaling;
                }
            }
        }

        return new DoseVolume
        {
            Dose = dose,
            SpacingX = dx,
            SpacingY = dy,
            SpacingZ = spacingZ,
            OriginX = originX,
            OriginY = originY,
            OriginZ = originZ,
            ZPositions = zPositions
        };
    }
}
