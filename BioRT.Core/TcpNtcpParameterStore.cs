using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace BioRT.Core.Radiobiology;

public class TcpNtcpParameterStore
{
    [JsonPropertyName("ntcp_parameters")]
    public Dictionary<string, NtcpOrganEntry> NtcpParameters { get; set; }
}
