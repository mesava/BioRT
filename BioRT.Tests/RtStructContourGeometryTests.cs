using BioRT.IO.Dicom;
using FellowOakDicom;
using Xunit;

namespace BioRT.Tests;

/// <summary>
/// Do not silently interpret DICOM XOR, open or point contours as
/// filled polygons. Stage 2 safety gate before supporting topology.
/// </summary>
public sealed class RtStructContourGeometryTests
{
    [Fact]
    public void StandardClosedPlanarContour_ImportsAsExpected()
    {
        DicomFile file = CreateRtStruct("CLOSED_PLANAR");

        var contours = new RtStructReader().ReadContours(file);

        Assert.Single(contours);
        double[] polygon = Assert.Single(contours[7]);

        Assert.Equal(12, polygon.Length);
    }

    [Theory]
    [InlineData("CLOSEDPLANAR_XOR")]
    [InlineData("OPEN_PLANAR")]
    [InlineData("OPEN_NONPLANAR")]
    [InlineData("POINT")]
    public void UnsupportedTopology_FailsClosedInsteadOfProducingWrongDvh(
        string geometricType)
    {
        DicomFile file = CreateRtStruct(geometricType);

        NotSupportedException ex =
            Assert.Throws<NotSupportedException>(
                () => new RtStructReader().ReadContours(file));

        Assert.Contains(geometricType, ex.Message);
        Assert.Contains("ROI 7", ex.Message);
    }

    [Fact]
    public void ClosedPlanarContourWithTooFewVertices_IsRejected()
    {
        var ds = new DicomDataset();
        var contour = new DicomDataset();

        contour.Add(DicomTag.ContourGeometricType, "CLOSED_PLANAR");
        contour.Add(DicomTag.NumberOfContourPoints, 2);
        contour.Add(
            DicomTag.ContourData,
            new double[] { 0, 0, 0, 1, 1, 0 });

        var roiContour = new DicomDataset();
        roiContour.Add(DicomTag.ReferencedROINumber, 7);
        roiContour.Add(
            new DicomSequence(
                DicomTag.ContourSequence,
                contour));

        ds.Add(
            new DicomSequence(
                DicomTag.ROIContourSequence,
                roiContour));

        InvalidOperationException ex =
            Assert.Throws<InvalidOperationException>(
                () => new RtStructReader().ReadContours(
                    new DicomFile(ds)));

        Assert.Contains("invalid", ex.Message);
    }

    private static DicomFile CreateRtStruct(
        string geometricType)
    {
        var contour = new DicomDataset();
        contour.Add(DicomTag.ContourGeometricType, geometricType);
        contour.Add(DicomTag.NumberOfContourPoints, 4);
        contour.Add(
            DicomTag.ContourData,
            new double[]
            {
                0, 0, 0,
                2, 0, 0,
                2, 2, 0,
                0, 2, 0
            });

        var roiContour = new DicomDataset();
        roiContour.Add(DicomTag.ReferencedROINumber, 7);
        roiContour.Add(
            new DicomSequence(
                DicomTag.ContourSequence,
                contour));

        var ds = new DicomDataset();
        ds.Add(
            new DicomSequence(
                DicomTag.ROIContourSequence,
                roiContour));

        return new DicomFile(ds);
    }
}
