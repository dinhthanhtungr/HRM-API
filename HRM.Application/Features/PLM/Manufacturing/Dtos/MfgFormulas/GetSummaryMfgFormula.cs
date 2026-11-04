using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulas
{
    public class GetSummaryMfgFormula
    {
        public Guid ManufacturingFormulaId { get; set; }
        public string? ExternalId { get; set; }
        public string? Name { get; set; }
        public decimal? TotalPrice { get; set; }
        public DateTime? FormulaSourceIdCreatedDate { get; set; }
        public virtual ICollection<GetSampleMfgFormulaMaterial> ManufacturingFormulaMaterials { get; set; } = new List<GetSampleMfgFormulaMaterial>();
    }
}
