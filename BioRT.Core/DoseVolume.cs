using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BioRT.Core.Models;

public class DoseVolume
{
    public double[,,] Dose { get; set; }

    public int SizeX => Dose.GetLength(0);
    public int SizeY => Dose.GetLength(1);
    public int SizeZ => Dose.GetLength(2);

    // mm
    public double SpacingX { get; set; }
    public double SpacingY { get; set; }
    public double SpacingZ { get; set; }

    // DICOM origin (mm)
    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public double OriginZ { get; set; }
    public double[] ZPositions { get; set; }

}
