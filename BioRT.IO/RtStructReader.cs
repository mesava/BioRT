using FellowOakDicom;
using BioRT.Core.Models;

namespace BioRT.IO.Dicom;

public class RtStructReader
{
    public Dictionary<int, string> ReadStructureNames(DicomFile file)
    {
        var map = new Dictionary<int, string>();

        var ds = file.Dataset;

        var roiSeq = ds.GetSequence(DicomTag.StructureSetROISequence);

        foreach (var roi in roiSeq.Items)
        {
            int roiNumber =
                roi.GetSingleValue<int>(DicomTag.ROINumber);

            string name =
                roi.GetSingleValue<string>(DicomTag.ROIName);

            map[roiNumber] = name;
        }

        return map;
    }

    public Dictionary<int, List<double[]>> ReadContours(DicomFile file)
    {
        var contours = new Dictionary<int, List<double[]>>();

        var ds = file.Dataset;

        var roiContourSeq =
            ds.GetSequence(DicomTag.ROIContourSequence);

        foreach (var roiContour in roiContourSeq.Items)
        {
            int roiNumber =
                roiContour.GetSingleValue<int>(DicomTag.ReferencedROINumber);

            var contourSeq =
                roiContour.GetSequence(DicomTag.ContourSequence);

            var list = new List<double[]>();

            foreach (var contour in contourSeq.Items)
            {
                // Current dose-volume rasterization supports filled polygons
                // only. CLOSEDPLANAR_XOR requires exclusive-disjunction
                // topology; OPEN_* and POINT cannot form voxel area masks.
                string geometricType =
                    contour.GetSingleValue<string>(
                        DicomTag.ContourGeometricType);

                if (!string.Equals(
                        geometricType,
                        "CLOSED_PLANAR",
                        StringComparison.Ordinal))
                {
                    throw new NotSupportedException(
                        $"RTSTRUCT ROI {roiNumber}: contour type " +
                        $"'{geometricType}' is not supported for DVH. " +
                        "BioRT cannot treat XOR, open or point contours " +
                        "as ordinary filled polygons.");
                }

                double[] data =
                    contour.GetValues<double>(DicomTag.ContourData);

                if (data.Length < 9 ||
                    data.Length % 3 != 0 ||
                    data.Any(value => !double.IsFinite(value)))
                {
                    throw new InvalidOperationException(
                        $"RTSTRUCT ROI {roiNumber} has invalid " +
                        "CLOSED_PLANAR contour coordinates.");
                }

                list.Add(data);
            }

            contours[roiNumber] = list;
        }

        return contours;
    }
}
