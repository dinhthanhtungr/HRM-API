using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders
{
    public class GetSampleMfgFormula
    {
        public Guid ManufacturingFormulaId { get; set; }
        public string? MfgProductionOrderExternalId { get; set; }
        public string? ExternalId { get; set; }
        public string? Status { get; set; }
        public bool? isStandard { get; set; } = false;
        public bool? IsSelect { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
