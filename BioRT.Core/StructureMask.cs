using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BioRT.Core.Models;

public class StructureMask
{
    public string Name { get; set; }

    // true = voxel belongs to structure
    public bool[,,] Mask { get; set; }
}
