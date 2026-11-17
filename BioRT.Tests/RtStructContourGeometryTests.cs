using BioRT.IO.Dicom;
using BioRT.Core.Models;
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
                    CreateDicomFile(ds)));

        Assert.Contains("invalid", ex.Message);
    }

    [Fact]
    public void ClosedPlanarContour_RejectsIncorrectPointCount()
    {
        DicomFile file = CreateRtStruct(
            "CLOSED_PLANAR",
            declaredPoints: 5);

        InvalidOperationException ex =
            Assert.Throws<InvalidOperationException>(
                () => new RtStructReader().ReadContours(file));

        Assert.Contains("NumberOfContourPoints", ex.Message);
    }

    [Fact]
    public void ClosedPlanarContour_RejectsNonAxialPlane()
    {
        DicomFile file = CreateRtStruct(
            "CLOSED_PLANAR",
            nonAxial: true);

        NotSupportedException ex =
            Assert.Throws<NotSupportedException>(
                () => new RtStructReader().ReadContours(file));

        Assert.Contains("non-axial", ex.Message);
    }

    [Fact]
    public void TypedReader_PreservesXorRuleWithTwoPolygons()
    {
        DicomFile file = CreateMultiContourRoi(
            "CLOSEDPLANAR_XOR",
            "CLOSEDPLANAR_XOR");

        Dictionary<int, RoiContours> rois =
            new RtStructReader().ReadRoiContours(file);

        RoiContours roi = Assert.Single(rois).Value;
        Assert.Equal(RoiContourFillRule.Xor, roi.FillRule);
        Assert.Equal(2, roi.Polygons.Count);
    }

    [Fact]
    public void LegacyFlatReader_StillRejectsXorWithoutMetadata()
    {
        DicomFile file = CreateMultiContourRoi(
            "CLOSEDPLANAR_XOR",
            "CLOSEDPLANAR_XOR");

        Assert.Throws<NotSupportedException>(
            () => new RtStructReader().ReadContours(file));
    }

    [Fact]
    public void MixedXorAndOrdinaryContours_AreRejected()
    {
        DicomFile file = CreateMultiContourRoi(
            "CLOSEDPLANAR_XOR",
            "CLOSED_PLANAR");

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => new RtStructReader().ReadRoiContours(file));

        Assert.Contains("mixes", exception.Message);
    }

    [Fact]
    public void SingleXorContour_IsRejectedByTypedReader()
    {
        DicomFile file = CreateMultiContourRoi(
            "CLOSEDPLANAR_XOR");

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () => new RtStructReader().ReadRoiContours(file));

        Assert.Contains("more than one", exception.Message);
    }

    private static DicomFile CreateMultiContourRoi(
        params string[] geometricTypes)
    {
        var items = new List<DicomDataset>();

        for (int i = 0; i < geometricTypes.Length; i++)
        {
            var contour = new DicomDataset();
            contour.Add(
                DicomTag.ContourGeometricType,
                geometricTypes[i]);
            contour.Add(DicomTag.NumberOfContourPoints, 4);

            double inset = i == 0 ? 0.0 : 0.5;
            contour.Add(
                DicomTag.ContourData,
                new double[]
                {
                    inset, inset, 0,
                    4-inset, inset, 0,
                    4-inset, 4-inset, 0,
                    inset, 4-inset, 0
                });

            items.Add(contour);
        }

        var roiContour = new DicomDataset();
        roiContour.Add(DicomTag.ReferencedROINumber, 7);
        roiContour.Add(
            new DicomSequence(
                DicomTag.ContourSequence,
                items.ToArray()));

        var ds = new DicomDataset();
        ds.Add(
            new DicomSequence(
                DicomTag.ROIContourSequence,
                roiContour));

        return CreateDicomFile(ds);
    }

    private static DicomFile CreateDicomFile(DicomDataset ds)
    {
        ds.Add(DicomTag.Modality, "RTSTRUCT");
        ds.Add(DicomTag.SOPClassUID, DicomUID.RTStructureSetStorage);
        ds.Add(DicomTag.SOPInstanceUID, DicomUID.Generate());
        return new DicomFile(ds);
    }

    private static DicomFile CreateRtStruct(
        string geometricType,
        int declaredPoints = 4,
        bool nonAxial = false)
    {
        var contour = new DicomDataset();
        contour.Add(DicomTag.ContourGeometricType, geometricType);
        contour.Add(DicomTag.NumberOfContourPoints, declaredPoints);
        contour.Add(
            DicomTag.ContourData,
            new double[]
            {
                0, 0, 0,
                2, 0, 0,
                2, 2, nonAxial ? 0.25 : 0.0,
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

        return CreateDicomFile(ds);
    }
}
