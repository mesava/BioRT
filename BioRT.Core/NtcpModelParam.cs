using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BioRT.Core.Radiobiology;

public class NtcpModelParam
{
    public string Model { get; set; }      // "LKB"
    public double TD50 { get; set; }
    public double M { get; set; }
    public double N { get; set; }

    public string Endpoint { get; set; }
    public string Source { get; set; }
}