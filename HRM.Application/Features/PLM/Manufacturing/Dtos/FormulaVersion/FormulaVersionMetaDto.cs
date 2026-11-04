using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.FormulaVersion
{
    public class FormulaVersionMetaDto
    {
        public Guid ManufacturingFormulaVersionId { get; set; }
        public int VersionNo { get; set; }
        public string Status { get; set; } = default!;
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string? Note { get; set; }
        public virtual ICollection<GetManufacturingFormulaMaterial> ManufacturingFormulaMaterials { get; set; } = new List<GetManufacturingFormulaMaterial>();
    }
}
