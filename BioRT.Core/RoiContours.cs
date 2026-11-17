namespace BioRT.Core.Models;

/// <summary>
/// How multiple DICOM RTSTRUCT contours within one ROI are combined
/// on a single dose plane. This is ROI-level metadata: DICOM PS3.3
/// requires an ROI containing CLOSEDPLANAR_XOR to use that type for
/// every contour in the ROI.
/// </summary>
public enum RoiContourFillRule
{
    Union = 0,
    Xor = 1
}

/// <summary>
/// Axial closed-planar ROI polygons with their explicit DICOM fill rule.
/// This contract preserves ROI topology through IO -> Web/Console -> DVH.
/// Each polygon is a flat sequence of (x,y,z) patient-coordinate triplets.
/// </summary>
public sealed class RoiContours
{
    public required List<double[]> Polygons { get; init; }
    public required RoiContourFillRule FillRule { get; init; }
}
