using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrderRWs.MfgGetInformationDtos
{
    public class GetMfgProductionOrderFormulaInfor
    {
        public Guid ManufacturingFormulaId { get; set; }
        public string? ExternalId { get; set; }
        public string? Note { get; set; }
        public List<GetMfgProductionOrderFormulaItemsInfor> FormulaItems { get; set; } = new List<GetMfgProductionOrderFormulaItemsInfor>();
    }
}
