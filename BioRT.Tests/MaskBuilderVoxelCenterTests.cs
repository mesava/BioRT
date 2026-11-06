using BioRT.Core.DVH;
using BioRT.Core.Models;
using Xunit;

namespace BioRT.Tests;

/// <summary>
/// Analytic voxel-centre geometry checks for the current axial-only
/// RTSTRUCT to RTDOSE rasterization. DICOM ImagePositionPatient describes
/// the centre of dose voxel [0,0,0], not the corner of its extent.
/// </summary>
public sealed class MaskBuilderVoxelCenterTests
{
    [Fact]
    public void FirstDoseVoxelCentre_IsExactlyImagePositionPatient()
    {
        DoseVolume dose = CreateDose(
            originX: 100.0,
            originY: 200.0,
            spacingX: 3.0,
            spacingY: 2.0);

        StructureMask mask = MaskBuilder.BuildMask(
            "single-corner-voxel",
            [Square(99.6, 199.6, 100.4, 200.4, 7.0)],
            dose);

        Assert.True(mask.Mask[0, 0, 0]);
        Assert.Equal(1, Count(mask));
        Assert.False(mask.Mask[1, 0, 0]);
        Assert.False(mask.Mask[0, 1, 0]);
    }

    [Fact]
    public void AnisotropicGrid_UsesActualPatientCentreForColumnAndRow()
    {
        DoseVolume dose = CreateDose(
            originX: 100.0,
            originY: 200.0,
            spacingX: 3.0,
            spacingY: 2.0);

        // Column 1, row 1: X=103 mm, Y=202 mm.
        // The old +0.5*Spacing implementation selected zero voxels.
        StructureMask mask = MaskBuilder.BuildMask(
            "single-interior-voxel",
            [Square(102.6, 201.6, 103.4, 202.4, 7.0)],
            dose);

        Assert.True(mask.Mask[1, 1, 0]);
        Assert.Equal(1, Count(mask));

        DVHResult? dvh = DVHCalculator.Calculate(mask, dose);

        Assert.NotNull(dvh);
        Assert.Equal(14.0, dvh!.MeanDose, precision: 12);
        Assert.Equal(14.0, dvh.MaxDose, precision: 12);
    }

    [Fact]
    public void TranslatingDoseOriginAndRoiTogether_DoesNotChangeMask()
    {
        DoseVolume original = CreateDose(
            originX: 100.0,
            originY: 200.0,
            spacingX: 3.0,
            spacingY: 2.0);

        DoseVolume translated = CreateDose(
            originX: -70.0,
            originY: 25.0,
            spacingX: 3.0,
            spacingY: 2.0);

        StructureMask first = MaskBuilder.BuildMask(
            "original",
            [Square(102.6, 201.6, 103.4, 202.4, 7.0)],
            original);

        StructureMask second = MaskBuilder.BuildMask(
            "translated",
            [Square(-67.4, 26.6, -66.6, 27.4, 7.0)],
            translated);

        for (int x = 0; x < original.SizeX; x++)
        for (int y = 0; y < original.SizeY; y++)
        {
            Assert.Equal(
                first.Mask[x, y, 0],
                second.Mask[x, y, 0]);
        }

        Assert.Equal(1, Count(first));
        Assert.Equal(1, Count(second));
    }

    private static DoseVolume CreateDose(
        double originX,
        double originY,
        double spacingX,
        double spacingY)
    {
        var values = new double[3, 3, 1];

        for (int x = 0; x < 3; x++)
        for (int y = 0; y < 3; y++)
            values[x, y, 0] = 10.0 + x + 3.0 * y;

        return new DoseVolume
        {
            Dose = values,
            OriginX = originX,
            OriginY = originY,
            OriginZ = 7.0,
            SpacingX = spacingX,
            SpacingY = spacingY,
            SpacingZ = 1.0,
            ZPositions = [7.0]
        };
    }

    private static double[] Square(
        double left,
        double bottom,
        double right,
        double top,
        double z)
        =>
        [
            left, bottom, z,
            right, bottom, z,
            right, top, z,
            left, top, z
        ];

    private static int Count(StructureMask mask)
    {
        int count = 0;

        foreach (bool selected in mask.Mask)
        {
            if (selected)
                count++;
        }

        return count;
    }
}
