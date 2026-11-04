using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrderRWs.MfgGetInformationDtos
{
    public class GetMfgProductionOrderNoteInfor
    {
        public Guid? MfgProductionOrderId { get; set; }
        public string? LabNote { get; set; }
        public string? Requirement { get; set; }
        public string? PlpuNote { get; set; }
        public string? QcCheck { get; set; }
        public StepOfProduct? StepOfProduct { get; set; }
        public GetMfgProductionOrderFormulaInfor? FormulaInfor { get; set; }
    }
}
