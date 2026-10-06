namespace BioRT.Core.Models;

public class PlanData
{
    public string PatientId { get; set; }

    public int Fractions { get; set; }

    public double DosePerFraction { get; set; }

    public double TotalDose => Fractions * DosePerFraction;

    public double OTTdays { get; set; }

    public Dictionary<string, StructureDVH> DVHs { get; set; } = new();

    public DoseVolume Dose { get; set; }
}
