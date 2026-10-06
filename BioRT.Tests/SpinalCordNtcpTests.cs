using BioRT.Core.Models;
using BioRT.Core.Radiobiology;
using Xunit;

namespace BioRT.Tests;

public class SpinalCordNtcpTests
{
    private static string LibraryPath => Path.Combine(
        AppContext.BaseDirectory,
        "data",
        "ntcp_parameters_v2.json");

    [Fact]
    public void Library_LoadsSpinalCordModelsFromGenericCollection()
    {
        var library = NtcpModelLibrary.Load(LibraryPath);

        Assert.Contains(
            library.Models,
            m => m.Id == "burman_1991_spinal_cord_lkb_myelopathy");

        Assert.Contains(
            library.Models,
            m => m.Id == "quantec_2010_spinal_cord_conventional_reference");

        Assert.Contains(
            library.Models,
            m => m.Id == "hytec_2021_spinal_cord_denovo_sbrt_reference");
    }

    [Fact]
    public void BurmanLegacyLkb_AtTd50_GivesFiftyPercentInConventionalFractionation()
    {
        var model = LoadModel("burman_1991_spinal_cord_lkb_myelopathy");
        var engine = new NtcpModelEngine();

        var result = engine.Evaluate(
            model,
            UniformCumulativeDvh(66.5),
            new NtcpEvaluationContext
            {
                DosePerFractionGy = 2.0,
                Fractions = 33
            });

        Assert.Equal(NtcpEvaluationStatus.Calculated, result.Status);
        Assert.NotNull(result.Probability);
        Assert.Equal(0.5, result.Probability!.Value, precision: 12);
        Assert.Equal("physical_dose", result.AppliedDoseBasis);
    }

    [Fact]
    public void BurmanLegacyLkb_IsBlockedOutsideConfiguredConventionalFractionationRange()
    {
        var model = LoadModel("burman_1991_spinal_cord_lkb_myelopathy");
        var engine = new NtcpModelEngine();

        var result = engine.Evaluate(
            model,
            UniformCumulativeDvh(30.0),
            new NtcpEvaluationContext
            {
                DosePerFractionGy = 6.0,
                Fractions = 5
            });

        Assert.Equal(NtcpEvaluationStatus.NotApplicable, result.Status);
        Assert.Null(result.Probability);
        Assert.Contains(
            result.Warnings,
            w => w.Contains("outside", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void QuantecConventionalReference_IsStoredButNotInterpolatedAsContinuousNtcp()
    {
        var model = LoadModel("quantec_2010_spinal_cord_conventional_reference");
        var engine = new NtcpModelEngine();

        var result = engine.Evaluate(
            model,
            UniformCumulativeDvh(50.0),
            new NtcpEvaluationContext
            {
                DosePerFractionGy = 2.0,
                Fractions = 25
            });

        Assert.Equal(NtcpEvaluationStatus.RuntimeDisabled, result.Status);
        Assert.Null(result.Probability);
    }

    [Fact]
    public void HytecSbrtReference_IsKeptSeparateFromConventionalLkb()
    {
        var model = LoadModel("hytec_2021_spinal_cord_denovo_sbrt_reference");

        Assert.Equal("reference_constraint_table", model.EquationId);
        Assert.False(model.Implementation!.RuntimeEnabled);
        Assert.Equal(
            "fraction_specific_dmax_reference",
            model.Implementation.InputMode);
    }

    private static NtcpModelDefinition LoadModel(string id)
    {
        var library = NtcpModelLibrary.Load(LibraryPath);

        return library.Models.Single(m =>
            string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private static StructureDVH UniformCumulativeDvh(double doseGy)
    {
        return new StructureDVH
        {
            Name = "SpinalCord",
            Dose = [0.0, doseGy, doseGy + 0.1],
            Volume = [100.0, 100.0, 0.0],
            MeanDose = doseGy,
            MaxDose = doseGy
        };
    }
}
