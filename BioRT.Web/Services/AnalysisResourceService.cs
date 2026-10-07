using System.Reflection;
using BioRT.Core.Matching;
using BioRT.Core.Radiobiology;

namespace BioRT.Web.Services;

public sealed class AnalysisResourceService
{
    public StructureMatcher StructureMatcher { get; }

    public NtcpModelLibrary NtcpLibrary { get; }

    public TcpModelLibrary TcpLibrary { get; }

    public AnalysisResourceService()
    {
        StructureMatcher =
            StructureMatcher.FromJson(
                ReadEmbeddedText(
                    "BioRT.Web.Data.aliases.json"));

        NtcpLibrary =
            NtcpModelLibrary.LoadJson(
                ReadEmbeddedText(
                    "BioRT.Web.Data.ntcp_parameters_v2.json"));

        TcpLibrary =
            TcpModelLibrary.LoadJson(
                ReadEmbeddedText(
                    "BioRT.Web.Data.tcp_parameters_v2.json"));
    }

    private static string ReadEmbeddedText(string resourceName)
    {
        using Stream stream =
            Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded scientific resource not found: {resourceName}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
