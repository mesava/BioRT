using System.Text.Json;
using BioRT.Core.Radiobiology;
using Xunit;

namespace BioRT.Tests;

public class TcpParameterLibraryTests
{
    private static string LibraryPath => Path.Combine(
        AppContext.BaseDirectory,
        "data",
        "tcp_parameters_v2.json");

    [Fact]
    public void Library_LoadsWithExpectedSchemaVersion()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(LibraryPath));

        string? version = doc.RootElement
            .GetProperty("metadata")
            .GetProperty("schema_version")
            .GetString();

        Assert.Equal("0.1.0", version);
    }

    [Fact]
    public void ModelCollections_HaveUniqueIds_AndTraceableSources()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(LibraryPath));

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int count = 0;

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array ||
                !property.Name.EndsWith("_models", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var model in property.Value.EnumerateArray())
            {
                count++;

                string id = model.GetProperty("id").GetString()!;
                Assert.True(ids.Add(id), $"Duplicate TCP model id: {id}");

                var source = model.GetProperty("source");

                Assert.False(
                    string.IsNullOrWhiteSpace(source.GetProperty("pmid").GetString()),
                    $"Missing PMID for TCP model '{id}'.");

                Assert.False(
                    string.IsNullOrWhiteSpace(source.GetProperty("doi").GetString()),
                    $"Missing DOI for TCP model '{id}'.");
            }
        }

        Assert.True(count >= 6);
    }

    [Fact]
    public void FindById_IsExactAndCaseInsensitive()
    {
        var library = TcpModelLibrary.Load(LibraryPath);

        var model = library.FindById(
            "SACHPazidis_2020_prostate_gland_lq_poisson_mixed_followup");

        Assert.NotNull(model);
        Assert.Equal(
            "sachpazidis_2020_prostate_gland_lq_poisson_mixed_followup",
            model!.Id);
    }

    [Fact]
    public void Selector_SeparatesProstateSbrtRiskGroups()
    {
        var selector = new TcpModelSelector(
            TcpModelLibrary.Load(LibraryPath));

        var high = selector.Select(new TcpModelQuery
        {
            Diagnosis = "prostate_cancer",
            RiskGroup = "high",
            Setting = "definitive_sbrt",
            IncludeRuntimeDisabled = true
        });

        Assert.Contains(
            high,
            m => m.Id == "royce_2021_prostate_sbrt_ffbr_5y_high");

        Assert.DoesNotContain(
            high,
            m => m.Id ==
                 "royce_2021_prostate_sbrt_ffbr_5y_low_intermediate");
    }

    [Fact]
    public void RoyceLowIntermediate_IsPreservedButRuntimeDisabled()
    {
        var library = TcpModelLibrary.Load(LibraryPath);

        var model = library.Models.Single(m =>
            m.Id == "royce_2021_prostate_sbrt_ffbr_5y_low_intermediate");

        Assert.Equal(
            "source_inconsistency_unresolved",
            model.Status);

        Assert.False(model.Implementation!.RuntimeEnabled);

        Assert.Contains(
            "inconsistent",
            model.Implementation.RuntimeReason!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SachpazidisModels_RequireProstateGlandRatherThanGenericPtv()
    {
        var library = TcpModelLibrary.Load(LibraryPath);

        var models = library.Models
            .Where(m => m.Id.StartsWith(
                "sachpazidis_2020",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(2, models.Length);

        Assert.All(
            models,
            m => Assert.Equal(
                "prostate_gland",
                m.Target.CanonicalStructure));
    }
}
