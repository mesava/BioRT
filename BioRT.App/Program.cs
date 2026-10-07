using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

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

        var criteriaJson = JsonDocument.Parse(File.ReadAllText(criteriaJsonPath));
        var criteria = new List<DoseCriterion>();

        foreach (var p in criteriaJson.RootElement
                     .GetProperty("prescriptions")
                     .EnumerateArray())
        {
            var presc = p.GetProperty("prescription");
            string structureName = presc.GetProperty("structureName").GetString()!;

            foreach (var dg in presc.GetProperty("doseGoals").EnumerateArray())
            {
                var c = DoseCriterionParser.Parse(
                    structureName,
                    dg.GetProperty("doseGoal").GetString()!);

                if (c != null)
                    criteria.Add(c);
            }
        }

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
        // ================= IDENTIFY PTVs =================

        var ptvRx = new Dictionary<string, double>();

        foreach (var g in criteria.GroupBy(c => c.StructureName))
        {
            var d50 = g.FirstOrDefault(c =>
                c.Type == CriterionType.DxxPercent &&
                Math.Abs(c.DxPercent!.Value - 50.0) < 0.5 &&
                c.Operator == ">=");

            if (d50 != null)
                ptvRx[g.Key] = d50.Limit;
        }

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

        // ================= DVH + VOLUMES =================

        plan.DVHs.Clear();
        var structureVolumesCc = new Dictionary<string, double>();
        var structureMasks = new Dictionary<string, StructureMask>();

        double voxelVolumeCc =
            plan.Dose.SpacingX *
            plan.Dose.SpacingY *
            plan.Dose.SpacingZ / 1000.0;

        foreach (var group in criteria.GroupBy(c => c.StructureName))
        {
            string name = group.Key;

            if (name.StartsWith("PTV") && !ptvRx.ContainsKey(name))
                continue;

            var match = roiNames.FirstOrDefault(r =>
                r.Value.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                name.Contains(r.Value, StringComparison.OrdinalIgnoreCase));

            if (match.Key == 0 || !contours.ContainsKey(match.Key))
                continue;

            var mask = MaskBuilder.BuildMask(
                match.Value,
                contours[match.Key],
                plan.Dose);

            structureVolumesCc[match.Value] =
                mask.Mask.Cast<bool>().Count(v => v) * voxelVolumeCc;

            structureMasks[match.Value] = mask;

            var dvh = DVHCalculator.Calculate(mask, plan.Dose);
            if (dvh == null)
                continue;

            plan.DVHs[match.Value] = new StructureDVH
            {
                Name = match.Value,
                MeanDose = dvh.MeanDose,
                MaxDose = dvh.MaxDose,
                Dose = dvh.DoseBins,
                Volume = dvh.VolumeBins
            };
        }

        // ================= PTV METRICS =================

        Console.WriteLine("PTV metrics:");

        foreach (var kv in ptvRx)
        {
            var dvh = plan.DVHs.Values.FirstOrDefault(d =>
                d.Name.Contains(kv.Key, StringComparison.OrdinalIgnoreCase));

            if (dvh == null)
                continue;

            double rx = kv.Value;

            Console.WriteLine($"PTV: {kv.Key}");
            Console.WriteLine($"  Rx   : {rx:F2} Gy");
            Console.WriteLine($"  D2%  : {PtvMetricCalculator.D2(dvh):F2} Gy");
            Console.WriteLine($"  D98% : {PtvMetricCalculator.D98(dvh):F2} Gy");
            Console.WriteLine($"  D95% : {PtvMetricCalculator.D95(dvh):F2} Gy");
            Console.WriteLine($"  D50% : {PtvMetricCalculator.D50(dvh):F2} Gy");
            Console.WriteLine($"  HI   : {PtvMetricCalculator.HI(
                PtvMetricCalculator.D2(dvh),
                PtvMetricCalculator.D98(dvh)):F3}");

            if (structureMasks.TryGetValue(dvh.Name, out var ptvMask))
            {
                Console.WriteLine(
                    $"  CI   : {PtvSpatialMetrics.ComputeCI(ptvMask, plan.Dose, rx):F3}");
                Console.WriteLine(
                    $"  GI   : {PtvSpatialMetrics.ComputeGI(plan.Dose, rx):F3}");
            }

            Console.WriteLine();
        }

        // ================= CLINICAL CRITERIA =================

        Console.WriteLine("Clinical criteria evaluation:");

        foreach (var c in criteria.Where(c => !ptvRx.ContainsKey(c.StructureName)))
        {
            var dvh = plan.DVHs.Values.FirstOrDefault(d =>
                d.Name.Contains(c.StructureName, StringComparison.OrdinalIgnoreCase));

            if (dvh == null)
                continue;

            double value = c.Type switch
            {
                CriterionType.Dmean => DoseMetricCalculator.Dmean(dvh),
                CriterionType.Dmax => DoseMetricCalculator.Dmax(dvh),
                CriterionType.DxxPercent =>
                    DoseMetricCalculator.DxPercent(dvh, c.DxPercent!.Value),
                CriterionType.VxxGyPercent =>
                    DoseMetricCalculator.VxxGyPercent(
                        dvh, c.DoseGy!.Value),
                CriterionType.VxxGyCc =>
                    DoseMetricCalculator.VxxGyCc(
                        dvh, c.DoseGy!.Value, structureVolumesCc[dvh.Name]),
                CriterionType.Dcc =>
                    DoseMetricCalculator.Dcc(
                        dvh, c.VolumeCc!.Value, structureVolumesCc[dvh.Name]),
                _ => double.NaN
            };

            bool pass = c.Operator == "<=" ? value <= c.Limit : value >= c.Limit;

            Console.WriteLine(
                $"{dvh.Name,-15} {c.Raw,-25} Value={value:F2}  {(pass ? "PASS" : "FAIL")}");
        }

        // ================= NTCP XEROSTOMIA =================

        string dataPath = Path.Combine(
            AppContext.BaseDirectory, "data");

        string ntcpLibraryPath = Path.Combine(
            dataPath, "ntcp_parameters_v2.json");

        string aliasesPath = Path.Combine(
            dataPath, "aliases.json");

        if (!File.Exists(ntcpLibraryPath))
        {
            Console.WriteLine($"ERROR: NTCP parameter library not found: {ntcpLibraryPath}");
            return;
        }

        if (!File.Exists(aliasesPath))
        {
            Console.WriteLine($"ERROR: Structure alias file not found: {aliasesPath}");
            return;
        }

        var matcher = new StructureMatcher(aliasesPath);
        var ntcpLibrary = NtcpModelLibrary.Load(ntcpLibraryPath);
        var ntcpSelector = new NtcpModelSelector(ntcpLibrary);
        var ntcpEngine = new NtcpModelEngine();

        var ntcpContext = ClinicalContextMapper.ToNtcpEvaluationContext(
            clinicalContext,
            fractions: plan.Fractions > 0
                ? plan.Fractions
                : null,
            dosePerFractionGy: plan.DosePerFraction > 0
                ? plan.DosePerFraction
                : null);

        Console.WriteLine();
        Console.WriteLine("NTCP — provenance-aware runtime-compatible models:");
        Console.WriteLine(
            $"Plan fractionation context: N={plan.Fractions}, " +
            $"nominal target dose/fx={(plan.DosePerFraction > 0 ? $"{plan.DosePerFraction:F3} Gy" : "unknown")}");

        foreach (var dvh in plan.DVHs.Values.Where(d => !ptvRx.ContainsKey(d.Name)))
        {
            string? canonical = matcher.Match(dvh.Name);

            if (canonical == null)
                continue;

            var models = ntcpSelector
                .Select(new NtcpModelQuery
                {
                    CanonicalStructure = canonical
                })
                .Where(m =>
                    string.Equals(
                        m.Implementation?.InputMode,
                        "single_structure_dvh",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        m.Implementation?.InputMode,
                        "single_structure_predictor_vector",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (models.Length == 0)
                continue;

            Console.WriteLine();
            Console.WriteLine($"{dvh.Name} -> {canonical}  Dmean={dvh.MeanDose:F2} Gy  Dmax={dvh.MaxDose:F2} Gy");

            foreach (var model in models)
            {
                var result = ntcpEngine.Evaluate(
                    model,
                    dvh,
                    ntcpContext);

                PrintNtcpResult(result);
            }
        }

        Console.WriteLine();
        Console.WriteLine("Reference-only NTCP evidence/models stored for matched structures:");

        foreach (var dvh in plan.DVHs.Values.Where(d => !ptvRx.ContainsKey(d.Name)))
        {
            string? canonical = matcher.Match(dvh.Name);
            if (canonical == null)
                continue;

            var referenceModels = ntcpSelector
                .Select(new NtcpModelQuery
                {
                    CanonicalStructure = canonical,
                    IncludeRuntimeDisabled = true
                })
                .Where(m => m.Implementation?.RuntimeEnabled != true)
                .ToArray();

            if (referenceModels.Length == 0)
                continue;

            Console.WriteLine();
            Console.WriteLine($"{dvh.Name} -> {canonical}");

            foreach (var model in referenceModels)
            {
                var result = ntcpEngine.Evaluate(
                    model,
                    dvh,
                    ntcpContext);

                PrintNtcpResult(result);
            }
        }

        Console.WriteLine();
        Console.WriteLine("Additional NTCP models requiring clinical/model-specific inputs:");

        foreach (var model in ntcpSelector
                     .Select(new NtcpModelQuery
                     {
                         EquationId = "logistic"
                     })
                     .Where(m =>
                         string.Equals(
                             m.Implementation?.InputMode,
                             "predictor_vector",
                             StringComparison.OrdinalIgnoreCase)))
        {
            var result = ntcpEngine.Evaluate(
                model,
                dvh: null,
                context: ntcpContext);

            PrintNtcpResult(result);
        }
        Console.WriteLine();
        Console.WriteLine("Finished successfully.");
        Console.ReadKey();
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
