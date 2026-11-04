using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrderRWs.MfgGetInformationDtos;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrderRWs.MfgGetInformationInforDtos
{
    public class GetMfgProductionOrderInform
    {
        public Guid MfgProductionOrderId { get; set; }
        public string? ExternalId { get; set; }
        public Guid MerchandiseOrderId { get; set; }
        public Guid MerchandiseOrderDetailId { get; set; }
        public string? MerchandiseOrderExternalId { get; set; }
        public string? CustomerNameSnapshot { get; set; }
        public string? CustomerExternalIdSnapshot { get; set; }
        public string? PONo { get; set; }
        public Guid? ProductId { get; set; }
        public string? ProductExternalIdSnapshot { get; set; }
        public string? ProductNameSnapshot { get; set; }
        public Guid FormulaCustomerSelect { get; set; }
        public string FormulaCustomerExternalIdSelect { get; set; } = string.Empty;
        public Guid? ManufacturingFormulaIdIsSelect { get; set; }
        public string? ManufacturingFormulaExternalIdIsSelect { get; set; } = string.Empty;
        public DateTime? ManufacturingDate { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public DateTime RequiredDate { get; set; }
        public decimal TotalQuantityRequest { get; set; }
        public decimal? TotalQuantity { get; set; }
        /// <summary>
        /// Number of active QC adjustment batches marked as additional batches.
        /// </summary>
        public int AdditionalBatchCount { get; set; }
        /// <summary>
        /// Signed production quantity change caused by active QC adjustments, in kilograms.
        /// A positive value increases production quantity; a negative value decreases it.
        /// </summary>
        public decimal QcQuantityDeltaKg { get; set; }
        public int? NumOfBatches { get; set; }
        public decimal UnitPriceAgreed { get; set; }
        public string Status { get; set; } = ManufacturingProductOrder.New.ToString();
        public string? LabNote { get; set; }
        public string? Requirement { get; set; }
        public string? PlpuNote { get; set; }
        public string BagType { get; set; } = string.Empty;
        public string? QcCheck { get; set; }
        public StepOfProduct? StepOfProduct { get; set; }
        public GetMfgProductionOrderFormulaInfor GetMfgFormulaInform { get; set; } = new GetMfgProductionOrderFormulaInfor();
    }
}
