using System.Text.Json;
using BioRT.Core.Radiobiology;
using Xunit;

namespace BioRT.Tests;

/// <summary>
/// Cross-project synthetic BED/EQD numerical vectors.
/// Matching values in HFC TypeScript tests are an interoperability gate,
/// not clinical validation of the underlying radiobiological model.
/// </summary>
public sealed class HfcLqParityTests
{
    [Fact]
    public void FractionationCorrector_ReproducesSharedHfcGoldenVectors()
    {
        string filePath = Path.Combine(
            AppContext.BaseDirectory,
            "data",
            "biorrt_hfc_lq_parity_v0_1.json");

        using JsonDocument doc =
            JsonDocument.Parse(File.ReadAllText(filePath));

        JsonElement root = doc.RootElement;
        Assert.Equal(
            "biorrt-hfc-lq-parity/0.1",
            root.GetProperty("schema_version").GetString());

        double tolerance =
            root.GetProperty("tolerance_abs_gy").GetDouble();

        JsonElement.ArrayEnumerator cases =
            root.GetProperty("cases").EnumerateArray();

        int caseCount = 0;

        foreach (JsonElement c in cases)
        {
            caseCount++;

            string id = c.GetProperty("id").GetString()!;
            int fractions = c.GetProperty("fractions").GetInt32();
            double physicalDose = c.GetProperty("total_dose_gy").GetDouble();
            double alphaBeta = c.GetProperty("alpha_beta_gy").GetDouble();
            double referenceDose =
                c.GetProperty("reference_fraction_gy").GetDouble();

            double actualBed =
                FractionationCorrector.CalculateBed(
                    physicalDose,
                    fractions,
                    alphaBeta);

            double actualEqd =
                FractionationCorrector.CalculateEquivalentDose(
                    physicalDose,
                    fractions,
                    alphaBeta,
                    referenceDose);

            double expectedBed =
                c.GetProperty("expected_bed_gy").GetDouble();

            double expectedEqd =
                c.GetProperty("expected_eqd_gy").GetDouble();

            Assert.True(
                Math.Abs(actualBed - expectedBed) <= tolerance,
                $"{id}: BED {actualBed:R} vs {expectedBed:R} Gy");

            Assert.True(
                Math.Abs(actualEqd - expectedEqd) <= tolerance,
                $"{id}: EQD {actualEqd:R} vs {expectedEqd:R} Gy");
        }

        Assert.Equal(5, caseCount);
    }
}
