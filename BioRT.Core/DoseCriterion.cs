using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BioRT.Core.Models;

public enum CriterionType
{
    DxxPercent,   // D95%, D50%
    Dcc,          // D10cm3
    VxxGyPercent, // V20Gy <= 30 %
    VxxGyCc,      // V20Gy <= 65 cm3
    Dmean,
    Dmax
}

public class DoseCriterion
{
    public string StructureName { get; set; }

    public CriterionType Type { get; set; }

    // ===== Для Dxx% =====
    public double? DxPercent { get; set; }   // 2, 50, 95 ...

    // ===== Для VxxGy =====
    public double? DoseGy { get; set; }      // 20 Gy, 30 Gy ...

    // ===== Для Dcc =====
    public double? VolumeCc { get; set; }    // 0.1, 1, 10 cm3

    // ===== Общие =====
    public string Operator { get; set; }     // <= или >=
    public double Limit { get; set; }        // Gy или % или cm3 (в зависимости от типа)

    // ===== Для логов и QA =====
    public string Raw { get; set; }
}