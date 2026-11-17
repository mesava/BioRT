using BioRT.Core.Models;
using FellowOakDicom;

namespace BioRT.IO.Dicom;

public sealed class RtStructReader
{
    private const double AxialToleranceMm = 1e-3;

    public Dictionary<int, string> ReadStructureNames(DicomFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var map = new Dictionary<int, string>();

        DicomSequence roiSeq =
            file.Dataset.GetSequence(DicomTag.StructureSetROISequence);

        foreach (DicomDataset roi in roiSeq.Items)
        {
            int number =
                roi.GetSingleValue<int>(DicomTag.ROINumber);
            string name =
                roi.GetSingleValue<string>(DicomTag.ROIName);

            map.Add(number, name);
        }

        return map;
    }

    /// <summary>
    /// Legacy flat-contour interface. It deliberately REJECTS XOR,
    /// which cannot safely be represented without composition metadata.
    /// New analysis paths must use ReadRoiContours.
    /// </summary>
    public Dictionary<int, List<double[]>> ReadContours(DicomFile file)
    {
        Dictionary<int, RoiContours> rois =
            ParseRoiContours(file, allowXor: false);

        return rois.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Polygons);
    }

    /// <summary>
    /// Typed interface preserving CLOSED_PLANAR union and
    /// CLOSEDPLANAR_XOR exclusive-disjunction semantics end-to-end.
    /// </summary>
    public Dictionary<int, RoiContours> ReadRoiContours(DicomFile file)
        => ParseRoiContours(file, allowXor: true);

    private static Dictionary<int, RoiContours> ParseRoiContours(
        DicomFile file,
        bool allowXor)
    {
        ArgumentNullException.ThrowIfNull(file);

        var result = new Dictionary<int, RoiContours>();

        DicomSequence roiContourSeq =
            file.Dataset.GetSequence(DicomTag.ROIContourSequence);

        foreach (DicomDataset roi in roiContourSeq.Items)
        {
            int number =
                roi.GetSingleValue<int>(
                    DicomTag.ReferencedROINumber);

            DicomSequence contourSeq =
                roi.GetSequence(DicomTag.ContourSequence);

            var polygons = new List<double[]>();
            RoiContourFillRule? rule = null;

            foreach (DicomDataset contour in contourSeq.Items)
            {
                string geometricType =
                    contour.GetSingleValue<string>(
                        DicomTag.ContourGeometricType);

                RoiContourFillRule currentRule;

                if (string.Equals(
                        geometricType,
                        "CLOSED_PLANAR",
                        StringComparison.Ordinal))
                {
                    currentRule = RoiContourFillRule.Union;
                }
                else if (string.Equals(
                             geometricType,
                             "CLOSEDPLANAR_XOR",
                             StringComparison.Ordinal))
                {
                    if (!allowXor)
                    {
                        throw new NotSupportedException(
                            $"RTSTRUCT ROI {number}: contour type " +
                            "'CLOSEDPLANAR_XOR' requires topology-aware " +
                            "ReadRoiContours; flat ReadContours is unsafe.");
                    }

                    currentRule = RoiContourFillRule.Xor;
                }
                else
                {
                    throw new NotSupportedException(
                        $"RTSTRUCT ROI {number}: contour type " +
                        $"'{geometricType}' is not supported for DVH. " +
                        "Open and point contours are not volume regions.");
                }

                if (rule is not null && rule != currentRule)
                {
                    throw new InvalidOperationException(
                        $"RTSTRUCT ROI {number} mixes CLOSED_PLANAR " +
                        "and CLOSEDPLANAR_XOR contour types; DICOM " +
                        "requires the same geometric type for the " +
                        "entire ROI when XOR is used.");
                }

                rule = currentRule;

                double[] data =
                    contour.GetValues<double>(DicomTag.ContourData);

                if (data.Length < 9 ||
                    data.Length % 3 != 0 ||
                    data.Any(value => !double.IsFinite(value)))
                {
                    throw new InvalidOperationException(
                        $"RTSTRUCT ROI {number} has invalid " +
                        "closed-planar contour coordinates.");
                }

                int expectedPoints =
                    contour.GetSingleValue<int>(
                        DicomTag.NumberOfContourPoints);

                if (data.Length / 3 != expectedPoints)
                {
                    throw new InvalidOperationException(
                        $"RTSTRUCT ROI {number}: NumberOfContourPoints " +
                        "does not match ContourData length.");
                }

                double planeZ = data[2];
                for (int point = 1; point < expectedPoints; point++)
                {
                    if (Math.Abs(
                            data[3 * point + 2] - planeZ) >
                        AxialToleranceMm)
                    {
                        throw new NotSupportedException(
                            $"RTSTRUCT ROI {number}: an oblique or " +
                            "non-axial contour plane is not yet supported.");
                    }
                }

                polygons.Add(data);
            }

            if (rule == RoiContourFillRule.Xor &&
                polygons.Count < 2)
            {
                throw new InvalidOperationException(
                    $"RTSTRUCT ROI {number}: CLOSEDPLANAR_XOR " +
                    "requires more than one contour.");
            }

            result.Add(
                number,
                new RoiContours
                {
                    Polygons = polygons,
                    FillRule = rule ?? RoiContourFillRule.Union
                });
        }

        return result;
    }
}
