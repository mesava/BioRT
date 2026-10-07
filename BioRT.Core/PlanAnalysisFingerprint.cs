using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BioRT.Core.Models;

namespace BioRT.Core.Analysis;

/// <summary>
/// Deterministic fingerprint of a completed analysis.
///
/// The fingerprint is intended for commissioning comparisons between front ends
/// (for example BioRT.App and BioRT.Web) that analyze the same de-identified
/// input set. It is not a patient identifier and must not be used as one.
/// </summary>
public static class PlanAnalysisFingerprint
{
    public static string Compute(
        PlanData plan,
        PlanAnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(result);

        var canonical = new StringBuilder();

        Append(canonical, "fractions", plan.Fractions);
        Append(canonical, "dose_per_fraction", plan.DosePerFraction);
        Append(canonical, "total_dose", plan.TotalDose);

        if (plan.Dose != null)
        {
            Append(canonical, "dose_size_x", plan.Dose.SizeX);
            Append(canonical, "dose_size_y", plan.Dose.SizeY);
            Append(canonical, "dose_size_z", plan.Dose.SizeZ);
            Append(canonical, "spacing_x", plan.Dose.SpacingX);
            Append(canonical, "spacing_y", plan.Dose.SpacingY);
            Append(canonical, "spacing_z", plan.Dose.SpacingZ);
            Append(canonical, "origin_x", plan.Dose.OriginX);
            Append(canonical, "origin_y", plan.Dose.OriginY);
            Append(canonical, "origin_z", plan.Dose.OriginZ);

            for (int i = 0; i < plan.Dose.ZPositions.Length; i++)
                Append(canonical, $"z[{i}]", plan.Dose.ZPositions[i]);
        }

        foreach (StructureAnalysisResult structure in result.Structures
                     .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            Append(canonical, "structure", structure.Name);
            Append(canonical, "volume_cc", structure.VolumeCc);
            Append(canonical, "mean_dose", structure.Dvh.MeanDose);
            Append(canonical, "max_dose", structure.Dvh.MaxDose);

            for (int i = 0; i < structure.Dvh.Dose.Length; i++)
            {
                Append(
                    canonical,
                    $"dvh:{structure.Name}:dose[{i}]",
                    structure.Dvh.Dose[i]);

                Append(
                    canonical,
                    $"dvh:{structure.Name}:vol[{i}]",
                    structure.Dvh.Volume[i]);
            }
        }

        foreach (PtvAnalysisResult ptv in result.PtvMetrics
                     .OrderBy(x => x.CriterionStructureName, StringComparer.OrdinalIgnoreCase))
        {
            Append(canonical, "ptv", ptv.CriterionStructureName);
            Append(canonical, "ptv_match", ptv.MatchedStructureName);
            Append(canonical, "rx", ptv.PrescriptionDoseGy);
            Append(canonical, "ptv_volume_cc", ptv.VolumeCc);
            Append(canonical, "d2", ptv.D2Gy);
            Append(canonical, "d98", ptv.D98Gy);
            Append(canonical, "d95", ptv.D95Gy);
            Append(canonical, "d50", ptv.D50Gy);
            Append(canonical, "hi", ptv.Hi);
            Append(canonical, "ci", ptv.Ci);
            Append(canonical, "gi", ptv.Gi);
        }

        foreach (ClinicalCriterionEvaluation criterion in result.ClinicalCriteria
                     .OrderBy(x => x.MatchedStructureName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.Criterion.Raw, StringComparer.OrdinalIgnoreCase))
        {
            Append(canonical, "criterion_structure", criterion.MatchedStructureName);
            Append(canonical, "criterion_raw", criterion.Criterion.Raw);
            Append(canonical, "criterion_value", criterion.Value);
            Append(canonical, "criterion_pass", criterion.Pass);
        }

        foreach (NtcpAnalysisResult ntcp in result.Ntcp
                     .OrderBy(x => x.StructureName ?? "", StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.Result.ModelId, StringComparer.OrdinalIgnoreCase))
        {
            Append(canonical, "ntcp_structure", ntcp.StructureName ?? "");
            Append(canonical, "ntcp_canonical", ntcp.CanonicalStructure ?? "");
            Append(canonical, "ntcp_model", ntcp.Result.ModelId);
            Append(canonical, "ntcp_status", ntcp.Result.Status.ToString());
            Append(canonical, "ntcp_probability", ntcp.Result.Probability);
            Append(canonical, "ntcp_effective_dose", ntcp.Result.EffectiveDoseGy);
            Append(canonical, "ntcp_basis", ntcp.Result.AppliedDoseBasis ?? "");
        }

        if (result.Tcp != null)
        {
            Append(canonical, "tcp_target", result.Tcp.TargetStructureName ?? "");
            Append(canonical, "tcp_target_volume_cc", result.Tcp.TargetVolumeCc);
            Append(canonical, "tcp_model", result.Tcp.Result.ModelId);
            Append(canonical, "tcp_status", result.Tcp.Result.Status.ToString());
            Append(canonical, "tcp_probability", result.Tcp.Result.Probability);
            Append(canonical, "tcp_effective_dose", result.Tcp.Result.EffectiveDoseGy);
            Append(canonical, "tcp_basis", result.Tcp.Result.AppliedDoseBasis ?? "");
        }

        foreach (string warning in result.Warnings.OrderBy(
                     x => x,
                     StringComparer.Ordinal))
        {
            Append(canonical, "warning", warning);
        }

        byte[] bytes =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    canonical.ToString()));

        return Convert.ToHexString(bytes)
            .ToLowerInvariant();
    }

    public static string ComputeShort(
        PlanData plan,
        PlanAnalysisResult result,
        int characters = 16)
    {
        if (characters <= 0 || characters > 64)
            throw new ArgumentOutOfRangeException(nameof(characters));

        return Compute(plan, result)[..characters];
    }

    private static void Append(
        StringBuilder builder,
        string key,
        string value)
    {
        builder
            .Append(key)
            .Append('=')
            .Append(value)
            .Append('\n');
    }

    private static void Append(
        StringBuilder builder,
        string key,
        int value)
        => Append(
            builder,
            key,
            value.ToString(
                CultureInfo.InvariantCulture));

    private static void Append(
        StringBuilder builder,
        string key,
        bool value)
        => Append(
            builder,
            key,
            value ? "1" : "0");

    private static void Append(
        StringBuilder builder,
        string key,
        double value)
        => Append(
            builder,
            key,
            value.ToString(
                "R",
                CultureInfo.InvariantCulture));

    private static void Append(
        StringBuilder builder,
        string key,
        double? value)
        => Append(
            builder,
            key,
            value is double number
                ? number.ToString(
                    "R",
                    CultureInfo.InvariantCulture)
                : "null");
}
