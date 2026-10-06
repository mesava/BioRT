using System.Globalization;
using System.Text.RegularExpressions;
using BioRT.Core.Models;

namespace BioRT.Core.DVH;

public static class DoseCriterionParser
{
    public static DoseCriterion Parse(string structureName, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        // =========================================================
        // Нормализация строки
        // =========================================================
        string s = raw
            .Replace("cm?", "cm3")
            .Replace("cm³", "cm3")
            .Replace("CM?", "cm3")
            .Replace("CM³", "cm3")
            .Replace(",", ".")
            .Trim();

        // Убираем толерансы Monaco: "(+1.02 Gy)", "(-3 %)" и т.п.
        s = Regex.Replace(s, @"\s*\(.*?\)", "").Trim();

        // =========================================================
        // Dmean <= X Gy
        // =========================================================
        var mDmean = Regex.Match(
            s, @"^Dmean\s*(<=|>=)\s*([\d\.]+)\s*Gy$",
            RegexOptions.IgnoreCase);

        if (mDmean.Success)
        {
            return new DoseCriterion
            {
                StructureName = structureName,
                Type = CriterionType.Dmean,
                Operator = mDmean.Groups[1].Value,
                Limit = ParseDouble(mDmean.Groups[2].Value),
                Raw = raw
            };
        }

        // =========================================================
        // Dmax <= X Gy
        // =========================================================
        var mDmax = Regex.Match(
            s, @"^Dmax\s*(<=|>=)\s*([\d\.]+)\s*Gy$",
            RegexOptions.IgnoreCase);

        if (mDmax.Success)
        {
            return new DoseCriterion
            {
                StructureName = structureName,
                Type = CriterionType.Dmax,
                Operator = mDmax.Groups[1].Value,
                Limit = ParseDouble(mDmax.Groups[2].Value),
                Raw = raw
            };
        }

        // =========================================================
        // Dxx% <= / >= X Gy
        // =========================================================
        var mDxx = Regex.Match(
            s, @"^D(\d+(?:\.\d+)?)%\s*(<=|>=)\s*([\d\.]+)\s*Gy$",
            RegexOptions.IgnoreCase);

        if (mDxx.Success)
        {
            return new DoseCriterion
            {
                StructureName = structureName,
                Type = CriterionType.DxxPercent,
                DxPercent = ParseDouble(mDxx.Groups[1].Value),
                Operator = mDxx.Groups[2].Value,
                Limit = ParseDouble(mDxx.Groups[3].Value),
                Raw = raw
            };
        }

        // =========================================================
        // Dcc  (D10cm3 <= X Gy)
        // =========================================================
        var mDcc = Regex.Match(
            s, @"^D([\d\.]+)cm3\s*(<=|>=)\s*([\d\.]+)\s*Gy$",
            RegexOptions.IgnoreCase);

        if (mDcc.Success)
        {
            return new DoseCriterion
            {
                StructureName = structureName,
                Type = CriterionType.Dcc,
                VolumeCc = ParseDouble(mDcc.Groups[1].Value),
                Operator = mDcc.Groups[2].Value,
                Limit = ParseDouble(mDcc.Groups[3].Value),
                Raw = raw
            };
        }

        // =========================================================
        // VxxGy <= / >= Y cm3   (ВАЖНО: раньше чем %)
        // =========================================================
        var mVcc = Regex.Match(
            s, @"^V([\d\.]+)Gy\s*(<=|>=)\s*([\d\.]+)\s*cm3$",
            RegexOptions.IgnoreCase);

        if (mVcc.Success)
        {
            return new DoseCriterion
            {
                StructureName = structureName,
                Type = CriterionType.VxxGyCc,
                DoseGy = ParseDouble(mVcc.Groups[1].Value),
                Operator = mVcc.Groups[2].Value,
                Limit = ParseDouble(mVcc.Groups[3].Value),
                Raw = raw
            };
        }

        // =========================================================
        // VxxGy <= / >= Y %
        // =========================================================
        var mVperc = Regex.Match(
            s, @"^V([\d\.]+)Gy\s*(<=|>=)\s*([\d\.]+)\s*%$",
            RegexOptions.IgnoreCase);

        if (mVperc.Success)
        {
            return new DoseCriterion
            {
                StructureName = structureName,
                Type = CriterionType.VxxGyPercent,
                DoseGy = ParseDouble(mVperc.Groups[1].Value),
                Operator = mVperc.Groups[2].Value,
                Limit = ParseDouble(mVperc.Groups[3].Value),
                Raw = raw
            };
        }

        // =========================================================
        // Неизвестный критерий → игнорируем (не валим приложение)
        // =========================================================
        return null;
    }

    private static double ParseDouble(string s)
        => double.Parse(s, CultureInfo.InvariantCulture);
}
