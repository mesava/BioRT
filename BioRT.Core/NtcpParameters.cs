namespace BioRT.Core.Radiobiology;

public static class NtcpParameters
{
    // TD50 [Gy], m, n
    public static readonly Dictionary<string, (double td50, double m, double n)> Table =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "Brain",        (60.0, 0.15, 0.25) },
            { "BrainStem",    (54.0, 0.12, 0.25) },
            { "SpinalCord",   (50.0, 0.10, 0.20) },
            { "OpticNerve",   (55.0, 0.20, 0.25) },
            { "OpticChiasm",  (54.0, 0.20, 0.25) }
        };
}
