using BioRT.Core.DVH;
using BioRT.Core.Models;
using Xunit;

namespace BioRT.Tests;

/// <summary>
/// Exact sphere-volume ground truth, with deliberately under-sampled
/// axial contours. Numerical method envelopes here are regression
/// assertions for this SYNTHETIC geometry only, not clinical tolerances.
/// </summary>
public sealed class AnalyticalSphereZBenchmarkTests
{
    [Theory]
    [InlineData(3, 0.30)]
    [InlineData(5, 0.12)]
    [InlineData(10, 0.05)]
    public void SphereWithSparseZContours_ComparesAgainstExactVolume(
        int radiusMm,
        double experimentalMaxRelativeError)
    {
        DoseVolume dose = SphereDoseGrid(radiusMm);
        RoiContours roi = SphereContours(radiusMm);

        ZSamplingComparison result =
            ExperimentalContourZSampling.Compare(
                roi, dose,
                thresholdGy: radiusMm,
                ZEndCapPolicy.NoExtrapolation);

        double exactSphereVolumeCc =
            4.0 / 3.0 *
            Math.PI *
            Math.Pow(radiusMm, 3) /
            1000.0;

        double legacyError =
            RelativeError(result.Legacy.VolumeCc, exactSphereVolumeCc);
        double nearestError =
            RelativeError(result.NearestContour.VolumeCc, exactSphereVolumeCc);
        double linearError =
            RelativeError(result.LinearOccupancy.VolumeCc, exactSphereVolumeCc);

        // With these explicitly defined sparse 2 mm contours and a 1 mm
        // dose-Z grid, the legacy method fills only source-Z frames.
        // Experimental methods also sample intermediate frames.
        Assert.True(
            nearestError < legacyError,
            $"R={radiusMm} mm; legacy error={legacyError:P3}; " +
            $"nearest error={nearestError:P3}.");

        Assert.True(
            linearError < legacyError,
            $"R={radiusMm} mm; legacy error={legacyError:P3}; " +
            $"linear occupancy error={linearError:P3}.");

        Assert.InRange(
            nearestError,
            0.0,
            experimentalMaxRelativeError);

        Assert.InRange(
            linearError,
            0.0,
            experimentalMaxRelativeError);

        // IMPORTANT: identical integral volume does not imply identical
        // dose sampling. The dose field here is D(z) = R + z Gy.
        // Symmetry gives a weighted mean R for the linear method.
        // Ties towards the lower source-Z plane in the nearest method
        // give a 0.5 Gy difference in these 2 mm/1 mm test grids.
        Assert.Equal(
            result.NearestContour.VolumeCc,
            result.LinearOccupancy.VolumeCc,
            precision: 12);

        Assert.Equal(
            radiusMm,
            result.LinearOccupancy.MeanDoseGy!.Value,
            precision: 8);

        Assert.Equal(
            radiusMm + 0.5,
            result.NearestContour.MeanDoseGy!.Value,
            precision: 8);
    }

    [Fact]
    public void SparseContourSpacing_IsNotClinicalVolumeAcceptance()
    {
        DoseVolume dose = SphereDoseGrid(3);
        RoiContours roi = SphereContours(3);

        ZSamplingComparison result =
            ExperimentalContourZSampling.Compare(
                roi, dose, thresholdGy: 3,
                ZEndCapPolicy.NoExtrapolation);

        double exact = 4.0 / 3.0 * Math.PI * 27 / 1000.0;
        double error = RelativeError(
            result.LinearOccupancy.VolumeCc, exact);

        // Even the mathematically more complete fractional-occupancy
        // method has >15% volume error for this 6 mm diameter sphere
        // under deliberately sparse 2 mm contour spacing.
        // A passing CI MUST NOT be mistaken for clinical accuracy.
        Assert.True(error > 0.15);
    }

    private static double RelativeError(double measured, double reference)
        => Math.Abs(measured - reference) / reference;

    private static DoseVolume SphereDoseGrid(int radius)
    {
        int width = radius * 2 + 1;
        double[] zPositions =
            Enumerable.Range(0, width)
                .Select(index => (double)(index - radius))
                .ToArray();

        var dose = new double[width, width, width];

        for (int k = 0; k < width; k++)
        for (int y = 0; y < width; y++)
        for (int x = 0; x < width; x++)
        {
            // Nonnegative linear Z gradient. Volume and dose
            // comparisons use the *same* synthetic dose grid.
            dose[x, y, k] = radius + zPositions[k];
        }

        return new DoseVolume
        {
            Dose = dose,
            OriginX = -radius + 0.17,
            OriginY = -radius + 0.17,
            OriginZ = -radius,
            SpacingX = 1.0,
            SpacingY = 1.0,
            SpacingZ = 1.0,
            ZPositions = zPositions
        };
    }

    private static RoiContours SphereContours(int radius)
    {
        const int verticesPerCircle = 128;
        var polygons = new List<double[]>();

        // All contour planes spaced by 2 mm; the source geometry is
        // a sampled sphere, not an exact continuous sphere surface.
        for (int z = -radius; z <= radius; z += 2)
        {
            double sectionRadius =
                Math.Sqrt(Math.Max(0, radius * radius - z * z));

            var polygon = new double[3 * verticesPerCircle];

            for (int vertex = 0; vertex < verticesPerCircle; vertex++)
            {
                double angle =
                    2.0 * Math.PI * vertex / verticesPerCircle;

                polygon[3 * vertex] =
                    sectionRadius * Math.Cos(angle);

                polygon[3 * vertex + 1] =
                    sectionRadius * Math.Sin(angle);

                polygon[3 * vertex + 2] = z;
            }

            polygons.Add(polygon);
        }

        return new RoiContours
        {
            Polygons = polygons,
            FillRule = RoiContourFillRule.Union
        };
    }
}
