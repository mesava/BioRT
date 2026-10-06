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
                var data =
                    contour.GetValues<double>(DicomTag.ContourData);

                list.Add(data);
            }

            contours[roiNumber] = list;
        }

        return contours;
    }
}
