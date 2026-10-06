using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BioRT.Core.Models;

public class StructureDVH
{
    public string Name { get; set; }

    public double[] Dose { get; set; }

    public double[] Volume { get; set; }

    public double MeanDose { get; set; }

    public double MaxDose { get; set; }
}
