using System.Text.Json;
using Xunit;

namespace BioRT.Tests;

public class Tg166ReferenceTests
{
    private static JsonDocument LoadReference()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "data",
            "tg166_reference_values.json");

        Assert.True(File.Exists(path), $"TG-166 reference file not found: {path}");

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    [Fact]
    public void TableViiiReference_HasExpectedNumberOfRows()
    {
        using var doc = LoadReference();

        int count = doc.RootElement
            .GetProperty("table_viii_geud_gy")
            .GetArrayLength();

        Assert.Equal(23, count);
    }

    [Fact]
    public void TableViii_HeadNeckParotidA1ReferencesArePreserved()
    {
        using var doc = LoadReference();

        var entries = doc.RootElement
            .GetProperty("table_viii_geud_gy")
            .EnumerateArray()
            .Where(x =>
                x.GetProperty("case").GetString() == "Head & Neck" &&
                x.GetProperty("organ").GetString()!.Contains("parotid", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(2, entries.Length);

        var left = entries.Single(x =>
            x.GetProperty("organ").GetString()!.StartsWith("Lt", StringComparison.OrdinalIgnoreCase));

        var right = entries.Single(x =>
            x.GetProperty("organ").GetString()!.StartsWith("Rt", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(1.0, left.GetProperty("a").GetDouble(), precision: 12);
        Assert.Equal(23.12, left.GetProperty("monaco").GetDouble(), precision: 12);
        Assert.Equal(23.12, left.GetProperty("pinnacle").GetDouble(), precision: 12);
        Assert.Equal(23.61, left.GetProperty("eclipse").GetDouble(), precision: 12);

        Assert.Equal(1.0, right.GetProperty("a").GetDouble(), precision: 12);
        Assert.Equal(20.57, right.GetProperty("monaco").GetDouble(), precision: 12);
        Assert.Equal(23.05, right.GetProperty("pinnacle").GetDouble(), precision: 12);
        Assert.Equal(23.11, right.GetProperty("eclipse").GetDouble(), precision: 12);
    }

    [Fact]
    public void TableViiReference_PreservesPublishedTcpAndNtcpValues()
    {
        using var doc = LoadReference();

        var entries = doc.RootElement
            .GetProperty("table_vii")
            .GetProperty("entries")
            .EnumerateArray()
            .ToArray();

        Assert.Equal(6, entries.Length);

        double[] values = entries
            .Select(x => x.GetProperty("reference_percent").GetDouble())
            .ToArray();

        Assert.Equal(
            new[] { 94.1, 80.3, 26.6, 18.1, 23.5, 29.5 },
            values);
    }
}
