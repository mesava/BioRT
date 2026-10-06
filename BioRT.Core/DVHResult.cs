using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BioRT.Core.Models;

public class DVHResult
{
    public string StructureName { get; set; }

    public double[] DoseBins { get; set; }   // Gy
    public double[] VolumeBins { get; set; } // %

    public double MeanDose { get; set; }
    public double MaxDose { get; set; }
}
