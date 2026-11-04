using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.CompareFormulaDTOs
{
    public class GetCompareFormula
    {
        public Guid Id { get; set; }
        public string? ExternalId { get; set; }
        public string? ColourCode { get; set; }
        public virtual ICollection<GetSampleMfgFormulaMaterial> ManufacturingFormulaMaterials { get; set; } = new List<GetSampleMfgFormulaMaterial>();
    }
}
