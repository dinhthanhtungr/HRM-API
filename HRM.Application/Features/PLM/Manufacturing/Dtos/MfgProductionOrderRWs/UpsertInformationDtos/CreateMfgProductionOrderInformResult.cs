using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrderRWs.UpsertInformationDtos
{
    public class CreateMfgProductionOrderInformResult
    {
        public Guid MfgProductionOrderId { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public Guid? ManufacturingFormulaId { get; set; }
        public string? ManufacturingFormulaExternalId { get; set; }
    }
}
