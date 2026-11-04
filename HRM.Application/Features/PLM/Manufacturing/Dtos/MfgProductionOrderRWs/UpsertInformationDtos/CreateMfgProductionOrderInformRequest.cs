using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgProductionOrderRWs.UpsertInformationDtos
{
    public class CreateMfgProductionOrderInformRequest
    {
        public Guid MerchandiseOrderId { get; set; }
        public Guid MerchandiseOrderDetailId { get; set; }
        public string? MerchandiseOrderExternalId { get; set; }
        public Guid? ProductId { get; set; }
        public string? ProductExternalIdSnapshot { get; set; }
        public string? ProductNameSnapshot { get; set; }
        public Guid? CustomerId { get; set; }
        public string? CustomerNameSnapshot { get; set; }
        public string? CustomerExternalIdSnapshot { get; set; }
        public Guid FormulaCustomerSelect { get; set; }
        public string? FormulaCustomerExternalIdSelect { get; set; }
        public Guid? ManufacturingFormulaIdIsSelect { get; set; }
        public string? ManufacturingFormulaExternalIdIsSelect { get; set; }
        public DateTime? ManufacturingDate { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public DateTime RequiredDate { get; set; }
        public decimal? TotalQuantity { get; set; }
        public decimal TotalQuantityRequest { get; set; }
        public int? NumOfBatches { get; set; }
        public decimal UnitPriceAgreed { get; set; }
        public string? LabNote { get; set; }
        public string? Requirement { get; set; }
        public string? PlpuNote { get; set; }
        public string? QcCheck { get; set; }
        public string? BagType { get; set; }
        public StepOfProduct? StepOfProduct { get; set; }
        public List<CreateMfgProductionOrderFormulaItemRequest> FormulaItems { get; set; } = new();
    }
}
