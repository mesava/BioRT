using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

using BioRT.Core.Analysis;
using BioRT.Core.DVH;
using BioRT.Core.Models;
using BioRT.Core.Radiobiology;
using BioRT.Core.Matching;
using BioRT.IO.Dicom;

using FellowOakDicom;

namespace BioRT.App;

internal class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Console.WriteLine(
            "FOR DECISION SUPPORT ONLY. NOT FOR PRIMARY CLINICAL DECISIONS.");
        Console.WriteLine();

        string path = "";

        // ================= SELECT FOLDER =================

        if (args.Length > 0 && Directory.Exists(args[0]))
        {
            path = args[0];
        }
        else
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var dialog = new FolderBrowserDialog
            {
                Description = "Select DICOM folder with RTPLAN / RTDOSE / RTSTRUCT",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            path = dialog.SelectedPath;
        }

        // ================= INIT LOG =================

        string logPath = Path.Combine(
            path,
            $"BioRT_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

        using var logFile = new StreamWriter(logPath, false, Encoding.UTF8);
        Console.SetOut(new DualWriter(Console.Out, logFile));

        Console.WriteLine($"Using folder: {path}");
        Console.WriteLine($"Log file    : {logPath}");
        Console.WriteLine();

        // ================= LOAD DICOM =================

        var importer = new DicomImporter();
        var plan = importer.Load(path);

        Console.WriteLine($"Loaded patient: {plan.PatientId}");

        if (plan.Dose == null)
        {
            Console.WriteLine("ERROR: RTDOSE not loaded!");
            return;
        }

        // ================= LOAD CRITERIA JSON =================

        var criteriaJsonPath = FindCriteriaJsonPath(path);

        if (criteriaJsonPath == null)
        {
            Console.WriteLine("WARNING: No criteria JSON found.");
            return;
        }

        IReadOnlyList<DoseCriterion> criteria =
            MonacoCriteriaParser.ParseJson(
                File.ReadAllText(criteriaJsonPath));

        Console.WriteLine(
            $"Criteria JSON: {Path.GetFileName(criteriaJsonPath)} ({criteria.Count} parsed criteria)");
        Console.WriteLine();
        // ================= OPTIONAL CLINICAL CONTEXT =================

        ClinicalContext? clinicalContext = null;

        string? clinicalContextPath = Directory
            .GetFiles(path, "*.json", SearchOption.AllDirectories)
            .FirstOrDefault(f =>
                string.Equals(
                    Path.GetFileName(f),
                    "clinical_context.json",
                    StringComparison.OrdinalIgnoreCase));

        if (clinicalContextPath != null)
        {
            try
            {
                clinicalContext = ClinicalContextLoader.Load(clinicalContextPath);
                Console.WriteLine(
                    $"Clinical context: loaded ({Path.GetFileName(clinicalContextPath)})");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"ERROR: invalid clinical_context.json: {ex.Message}");
                return;
            }
        }
        else
        {
            Console.WriteLine(
                "Clinical context: not provided (optional; multivariable models may report MissingInputs).");
        }

        Console.WriteLine();
        // ================= LOAD RTSTRUCT =================

        var structPath = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
            .First(f =>
                DicomFile.Open(f).Dataset.GetSingleValueOrDefault(
                    DicomTag.SOPClassUID, "") ==
                DicomUID.RTStructureSetStorage.UID);

        var structDicom = DicomFile.Open(structPath);
        var structReader = new RtStructReader();

        var roiNames = structReader.ReadStructureNames(structDicom);
        var contours = structReader.ReadContours(structDicom);

        // ================= SCIENTIFIC RESOURCES =================

        string dataPath = Path.Combine(
            AppContext.BaseDirectory, "data");

        string ntcpLibraryPath = Path.Combine(
            dataPath, "ntcp_parameters_v2.json");

        string tcpLibraryPath = Path.Combine(
            dataPath, "tcp_parameters_v2.json");

        string aliasesPath = Path.Combine(
            dataPath, "aliases.json");

        if (!File.Exists(ntcpLibraryPath))
        {
            Console.WriteLine(
                $"ERROR: NTCP parameter library not found: {ntcpLibraryPath}");
            return;
        }

        if (!File.Exists(aliasesPath))
        {
            Console.WriteLine(
                $"ERROR: Structure alias file not found: {aliasesPath}");
            return;
        }

        var matcher =
            new StructureMatcher(aliasesPath);

        var ntcpLibrary =
            NtcpModelLibrary.Load(ntcpLibraryPath);

        TcpModelLibrary? tcpLibrary =
            File.Exists(tcpLibraryPath)
                ? TcpModelLibrary.Load(tcpLibraryPath)
                : null;

        // ================= SHARED PLAN ANALYSIS =================

        PlanAnalysisResult analysis;

        try
        {
            analysis =
                new PlanAnalysisService().Analyze(
                    new PlanAnalysisRequest
                    {
                        Plan = plan,
                        StructureNames = roiNames,
                        Contours = contours,
                        Criteria = criteria,
                        ClinicalContext = clinicalContext,
                        StructureMatcher = matcher,
                        NtcpLibrary = ntcpLibrary,
                        TcpLibrary = tcpLibrary
                    });
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"ERROR: plan analysis failed safely: {ex.Message}");
            return;
        }

        // ================= PTV METRICS =================

        Console.WriteLine("PTV metrics:");

        foreach (PtvAnalysisResult ptv in analysis.PtvMetrics)
        {
            Console.WriteLine($"PTV: {ptv.CriterionStructureName}");
            Console.WriteLine($"  Matched ROI: {ptv.MatchedStructureName}");
            Console.WriteLine($"  Volume: {ptv.VolumeCc:F2} cm3");
            Console.WriteLine($"  Rx   : {ptv.PrescriptionDoseGy:F2} Gy");
            Console.WriteLine($"  D2%  : {ptv.D2Gy:F2} Gy");
            Console.WriteLine($"  D98% : {ptv.D98Gy:F2} Gy");
            Console.WriteLine($"  D95% : {ptv.D95Gy:F2} Gy");
            Console.WriteLine($"  D50% : {ptv.D50Gy:F2} Gy");
            Console.WriteLine($"  HI   : {ptv.Hi:F3}");
            Console.WriteLine($"  CI   : {ptv.Ci:F3}");
            Console.WriteLine($"  GI   : {ptv.Gi:F3}");
            Console.WriteLine();
        }

        // ================= CLINICAL CRITERIA =================

        Console.WriteLine("Clinical criteria evaluation:");

        foreach (ClinicalCriterionEvaluation item in analysis.ClinicalCriteria)
        {
            Console.WriteLine(
                $"{item.MatchedStructureName,-15} " +
                $"{item.Criterion.Raw,-25} " +
                $"Value={item.Value:F2}  " +
                $"{(item.Pass ? "PASS" : "FAIL")}");
        }

        // ================= NTCP =================

        Console.WriteLine();
        Console.WriteLine("NTCP — provenance-aware runtime-compatible models:");
        Console.WriteLine(
            $"Plan fractionation context: N={plan.Fractions}, " +
            $"nominal target dose/fx={(plan.DosePerFraction > 0 ? $"{plan.DosePerFraction:F3} Gy" : "unknown")}");

        foreach (var group in analysis.Ntcp
                     .Where(x =>
                         !x.ReferenceOnly &&
                         x.StructureName != null)
                     .GroupBy(x =>
                         (x.StructureName, x.CanonicalStructure)))
        {
            StructureDVH? dvh =
                plan.DVHs.Values.FirstOrDefault(d =>
                    string.Equals(
                        d.Name,
                        group.Key.StructureName,
                        StringComparison.OrdinalIgnoreCase));

            Console.WriteLine();
            Console.WriteLine(
                $"{group.Key.StructureName} -> {group.Key.CanonicalStructure}" +
                (dvh == null
                    ? ""
                    : $"  Dmean={dvh.MeanDose:F2} Gy  Dmax={dvh.MaxDose:F2} Gy"));

            foreach (NtcpAnalysisResult item in group)
                PrintNtcpResult(item.Result);
        }

        Console.WriteLine();
        Console.WriteLine(
            "Reference-only NTCP evidence/models stored for matched structures:");

        foreach (var group in analysis.Ntcp
                     .Where(x =>
                         x.ReferenceOnly &&
                         x.StructureName != null)
                     .GroupBy(x =>
                         (x.StructureName, x.CanonicalStructure)))
        {
            Console.WriteLine();
            Console.WriteLine(
                $"{group.Key.StructureName} -> {group.Key.CanonicalStructure}");

            foreach (NtcpAnalysisResult item in group)
                PrintNtcpResult(item.Result);
        }

        Console.WriteLine();
        Console.WriteLine(
            "Additional NTCP models requiring clinical/model-specific inputs:");

        foreach (NtcpAnalysisResult item in analysis.Ntcp.Where(
                     x => x.StructureName == null))
        {
            PrintNtcpResult(item.Result);
        }

        // ================= TCP =================

        Console.WriteLine();
        Console.WriteLine("TCP — explicit provenance-aware model selection:");

        if (analysis.Tcp == null)
        {
            if (clinicalContext == null ||
                string.IsNullOrWhiteSpace(
                    clinicalContext.Tcp.ModelId))
            {
                Console.WriteLine(
                    "TCP not requested. Add tcp.model_id to clinical_context.json to evaluate a specific validated model.");
            }
            else
            {
                Console.WriteLine(
                    "TCP was requested but no result was produced. See analysis warnings below.");
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(
                    analysis.Tcp.TargetStructureName))
            {
                Console.WriteLine(
                    $"TCP target: {analysis.Tcp.TargetStructureName}" +
                    (analysis.Tcp.TargetVolumeCc is double volume
                        ? $" | volume={volume:F2} cm3"
                        : ""));
            }

            PrintTcpResult(
                analysis.Tcp.Result);
        }

        if (analysis.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Analysis warnings:");

            foreach (string warning in analysis.Warnings)
                Console.WriteLine($"  WARNING: {warning}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Analysis fingerprint: {PlanAnalysisFingerprint.ComputeShort(plan, analysis)}");

        Console.WriteLine();
        Console.WriteLine("Finished successfully.");
        Console.ReadKey();
    }

    private static void PrintTcpResult(TcpEvaluationResult result)
    {
        string probability = result.Probability is double value
            ? $"{value * 100.0:F2}%"
            : "n/a";

        Console.WriteLine(
            $"  {result.ModelId,-52} {probability,8}  [{result.Status}]");

        Console.WriteLine(
            $"    Endpoint: {result.EndpointName}" +
            (string.IsNullOrWhiteSpace(result.TimePoint)
                ? ""
                : $" | {result.TimePoint}"));

        if (result.EffectiveDoseGy is double effectiveDose)
        {
            Console.WriteLine(
                $"    Effective dose: {effectiveDose:F2} Gy" +
                (string.IsNullOrWhiteSpace(result.AppliedDoseBasis)
                    ? ""
                    : $" | basis={result.AppliedDoseBasis}"));
        }

        if (!string.IsNullOrWhiteSpace(result.Pmid))
            Console.WriteLine($"    Source: PMID {result.Pmid}");

        if (result.MissingInputs.Count > 0)
        {
            Console.WriteLine(
                $"    Missing inputs: {string.Join(", ", result.MissingInputs)}");
        }

        foreach (string warning in result.Warnings)
            Console.WriteLine($"    WARNING: {warning}");
    }

    private static string? FindCriteriaJsonPath(string rootPath)
    {
        foreach (string file in Directory
                     .GetFiles(rootPath, "*.json", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(
                    Path.GetFileName(file),
                    "clinical_context.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));

                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty(
                        "prescriptions",
                        out var prescriptions) &&
                    prescriptions.ValueKind == JsonValueKind.Array)
                {
                    return file;
                }
            }
            catch (JsonException)
            {
                // Not a criteria JSON; continue searching.
            }
            catch (IOException)
            {
                // Unreadable candidate; continue searching.
            }
        }

        return null;
    }
    private static void PrintNtcpResult(NtcpEvaluationResult result)
    {
        string probability = result.Probability is double value
            ? $"{value * 100.0:F2}%"
            : "n/a";

        Console.WriteLine(
            $"  {result.ModelId,-42} {probability,8}  [{result.Status}]");
        Console.WriteLine(
            $"    Endpoint: {result.EndpointName}" +
            (string.IsNullOrWhiteSpace(result.TimePoint)
                ? ""
                : $" | {result.TimePoint}"));

        if (result.EffectiveDoseGy is double effectiveDose)
        {
            Console.WriteLine(
                $"    Effective dose: {effectiveDose:F2} Gy" +
                (string.IsNullOrWhiteSpace(result.AppliedDoseBasis)
                    ? ""
                    : $" | basis={result.AppliedDoseBasis}"));
        }

        if (!string.IsNullOrWhiteSpace(result.Pmid))
            Console.WriteLine($"    Source: PMID {result.Pmid}");

        if (result.MissingInputs.Count > 0)
            Console.WriteLine(
                $"    Missing inputs: {string.Join(", ", result.MissingInputs)}");

        foreach (string warning in result.Warnings)
            Console.WriteLine($"    WARNING: {warning}");
    }
}
// ================= LOG HELPER =================

class DualWriter : TextWriter
{
    private readonly TextWriter _console;
    private readonly TextWriter _file;

    public DualWriter(TextWriter console, TextWriter file)
    {
        _console = console;
        _file = file;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void WriteLine(string value)
    {
        _console.WriteLine(value);
        _file.WriteLine(value);
    }

    public override void Write(char value)
    {
        _console.Write(value);
        _file.Write(value);
    }
}
