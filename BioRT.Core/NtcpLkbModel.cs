using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace BioRT.Core.Radiobiology;

public class NtcpLkbModel
{
    [JsonPropertyName("td50_gy")]
    public double TD50 { get; set; }

    [JsonPropertyName("m")]
    public double M { get; set; }

    [JsonPropertyName("n")]
    public double N { get; set; }

    [JsonPropertyName("alpha_beta_gy")]
    public double? AlphaBeta { get; set; }

    [JsonIgnore]
    public string Source => "Emami/Burman";
}