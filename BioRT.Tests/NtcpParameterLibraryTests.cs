using System.Text.Json;
using Xunit;

namespace BioRT.Tests;

public class NtcpParameterLibraryTests
{
    private static JsonDocument LoadLibrary()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "data",
            "ntcp_parameters_v2.json");

        Assert.True(File.Exists(path), $"Parameter library not found: {path}");

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    [Fact]
    public void Library_IsValidJson_AndHasExpectedSchemaVersion()
    {
        using var doc = LoadLibrary();

        string? version = doc.RootElement
            .GetProperty("metadata")
            .GetProperty("schema_version")
            .GetString();

        Assert.Equal("0.2.0", version);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("xerostomia_models").ValueKind);
    }

    [Fact]
    public void ComputableModels_HaveUniqueIds_AndTraceablePrimarySources()
    {
        using var doc = LoadLibrary();

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in doc.RootElement.GetProperty("xerostomia_models").EnumerateArray())
        {
            string id = model.GetProperty("id").GetString()!;

            Assert.True(ids.Add(id), $"Duplicate model id: {id}");

            var source = model.GetProperty("source");

            Assert.False(string.IsNullOrWhiteSpace(source.GetProperty("pmid").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(source.GetProperty("doi").GetString()));

            string equationId = model.GetProperty("equation_id").GetString()!;
            Assert.True(
                equationId is "lkb_probit" or "logistic",
                $"Unsupported equation id '{equationId}' in model '{id}'.");
        }
    }

    [Fact]
    public void Semenenko2008_ParametersMatchPublishedCombinedAnalysisRecord()
    {
        using var doc = LoadLibrary();

        var model = FindModel(doc, "semenenko_li_2008_xerostomia_lkb_6m");
        var p = model.GetProperty("parameters");

        Assert.Equal(31.4, p.GetProperty("td50_gy").GetDouble(), precision: 12);
        Assert.Equal(0.53, p.GetProperty("m").GetDouble(), precision: 12);
        Assert.Equal(1.0, p.GetProperty("n").GetDouble(), precision: 12);

        var doseBasis = model.GetProperty("dose_basis");
        Assert.Equal(3.0, doseBasis.GetProperty("alpha_beta_gy").GetDouble(), precision: 12);
    }

    [Fact]
    public void LippV22_ContainsPublished2025ValidationEquation()
    {
        using var doc = LoadLibrary();

        var model = FindModel(doc, "lipp_v2_2_xerostomia_logistic_6m");
        var p = model.GetProperty("parameters");

        Assert.Equal(-2.295, p.GetProperty("intercept").GetDouble(), precision: 12);

        var predictors = p.GetProperty("predictors").EnumerateArray().ToArray();

        Assert.Equal(3, predictors.Length);
        Assert.Equal(0.0996, predictors[0].GetProperty("coefficient").GetDouble(), precision: 12);
        Assert.Equal(0.0182, predictors[1].GetProperty("coefficient").GetDouble(), precision: 12);

        var baseline = predictors[2].GetProperty("coding");
        Assert.Equal(0.0, baseline.GetProperty("none").GetDouble(), precision: 12);
        Assert.Equal(0.459, baseline.GetProperty("mild").GetDouble(), precision: 12);
        Assert.Equal(1.207, baseline.GetProperty("moderate_to_severe").GetDouble(), precision: 12);
    }

    [Fact]
    public void LegacyAveragedParotidParameters_AreDeprecated_NotPromoted()
    {
        using var doc = LoadLibrary();

        foreach (var model in doc.RootElement.GetProperty("xerostomia_models").EnumerateArray())
        {
            if (!model.TryGetProperty("parameters", out var p))
                continue;

            if (!p.TryGetProperty("td50_gy", out var td50) ||
                !p.TryGetProperty("m", out var m))
                continue;

            bool isLegacyComposite =
                Math.Abs(td50.GetDouble() - 40.8) < 1e-12 &&
                Math.Abs(m.GetDouble() - 0.54) < 1e-12;

            Assert.False(
                isLegacyComposite,
                "The old 40.8 Gy / 0.54 composite must not appear as a valid computable model.");
        }

        var deprecated = doc.RootElement
            .GetProperty("deprecated_or_invalid_composites")
            .EnumerateArray()
            .Select(x => x.GetProperty("id").GetString())
            .ToArray();

        Assert.Contains("legacy_parotid_40_8_0_54", deprecated);
    }

    private static JsonElement FindModel(JsonDocument doc, string id)
    {
        foreach (var model in doc.RootElement.GetProperty("xerostomia_models").EnumerateArray())
        {
            if (string.Equals(
                model.GetProperty("id").GetString(),
                id,
                StringComparison.OrdinalIgnoreCase))
            {
                return model;
            }
        }

        throw new Xunit.Sdk.XunitException($"Model '{id}' not found.");
    }
}
