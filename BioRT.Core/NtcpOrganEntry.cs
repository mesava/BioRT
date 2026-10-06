using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace BioRT.Core.Radiobiology;

public class NtcpOrganEntry
{
    [JsonPropertyName("emami_burman")]
    public NtcpLkbModel EmamiBurman { get; set; }

    [JsonPropertyName("quantec")]
    public NtcpQuantecModel Quantec { get; set; }

    // Можно расширять:
    // sbrt_single, sbrt_3fractions и т.д.
}