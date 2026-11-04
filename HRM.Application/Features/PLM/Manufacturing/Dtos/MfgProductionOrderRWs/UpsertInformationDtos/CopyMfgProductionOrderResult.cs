using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrderRWs
{
    public class CopyMfgProductionOrderResult
    {
        public Guid MfgProductionOrderId { get; set; }
        public string? ExternalId { get; set; }
    }
}
