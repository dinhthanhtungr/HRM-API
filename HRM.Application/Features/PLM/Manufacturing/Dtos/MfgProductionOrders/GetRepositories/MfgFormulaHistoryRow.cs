using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrders.GetRepositories
{
    public class MfgFormulaHistoryRow
    {
        public Guid MfgProductionOrderId { get; set; }
        public string? MfgProductionOrderExternalId { get; set; }
        public string MfgFormulaExternalId { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }
}
