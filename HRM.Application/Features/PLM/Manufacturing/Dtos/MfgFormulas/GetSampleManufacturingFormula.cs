using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas
{
    public class GetSampleManufacturingFormula
    {
        public Guid ManufacturingFormulaId { get; set; }
        public string? ExternalId { get; set; }
        public string status { get; set; } = string.Empty;
        public string VersionName { get; set; } = string.Empty;
        public string? Note { get; set; } = string.Empty;
        public virtual ICollection<GetManufacturingFormulaMaterial> ManufacturingFormulaMaterials { get; set; } = new List<GetManufacturingFormulaMaterial>();
    }
}
