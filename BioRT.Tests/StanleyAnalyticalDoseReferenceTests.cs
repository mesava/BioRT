using BioRT.Core.DVH;
using Xunit;

namespace BioRT.Tests;

/// <summary>
/// Numerical truth from Stanley et al., Medical Physics (2021)
/// DOI 10.1002/mp.14645, Eq. 1, s1=.249, s2=7.019,
/// s3=.029, s4=1.927. Analytic reference, not Monaco output.
/// </summary>
public sealed class StanleyAnalyticalDoseReferenceTests
{
    [Theory]
    [InlineData(3, 0.014137166941154066, 0.6270689269486488, 44.3560530592036)]
    [InlineData(5, 0.06544984694978735, 1.0522739315504, 16.077561378526934)]
    [InlineData(7, 0.17959438003021644, 1.636060705986882, 9.109754468439478)]
    [InlineData(10, 0.5235987755982988, 2.864059557519546, 5.469950830665868)]
    [InlineData(15, 1.7671458676442584, 6.059680326654485, 3.429077609044524)]
    [InlineData(20, 4.1887902047863905, 11.031835320653936, 2.6336566839867577)]
    public void PublishedRadialModel_HasExactSphericalIsodoseVolumes(
        double diameterMm,
        double expectedV20Cc,
        double expectedV10Cc,
        double expectedGi)
    {
        StanleyAnalyticalVolumes reference =
            StanleyAnalyticalDoseReference.Calculate(diameterMm);

        Assert.Equal(expectedV20Cc, reference.V20GyCc, precision: 10);
        Assert.Equal(expectedV10Cc, reference.V10GyCc, precision: 10);
        Assert.Equal(expectedGi, reference.GradientIndex, precision: 9);

        Assert.Equal(
            20.0,
            StanleyAnalyticalDoseReference.DoseAtRadiusGy(
                reference.SphereRadiusCm,
                diameterMm),
            precision: 10);

        Assert.Equal(
            10.0,
            StanleyAnalyticalDoseReference.DoseAtRadiusGy(
                reference.RadiusAt10GyCm,
                diameterMm),
            precision: 10);

        Assert.True(
            reference.RadiusAt10GyCm > reference.SphereRadiusCm);
        Assert.True(reference.CentralDoseGy > 20.0);
    }

    [Fact]
    public void Sphere10mm_ReproducesCenterDoseAndMonotonicRadialDose()
    {
        double center =
            StanleyAnalyticalDoseReference.DoseAtRadiusGy(
                radialDistanceCm: 0.0,
                sphereDiameterMm: 10.0);

        Assert.Equal(26.432808128060227, center, precision: 9);

        double internalRadius =
            StanleyAnalyticalDoseReference.DoseAtRadiusGy(
                0.25, 10.0);

        double surface =
            StanleyAnalyticalDoseReference.DoseAtRadiusGy(
                0.5, 10.0);

        double outside =
            StanleyAnalyticalDoseReference.DoseAtRadiusGy(
                0.75, 10.0);

        Assert.True(center > internalRadius);
        Assert.True(internalRadius > surface);
        Assert.True(surface > outside);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    public void InvalidNominalDiameter_IsRejected(double diameterMm)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StanleyAnalyticalDoseReference.Calculate(diameterMm));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidRadius_IsRejected(double radiusCm)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StanleyAnalyticalDoseReference.DoseAtRadiusGy(
                radiusCm, 10.0));
    }
}
