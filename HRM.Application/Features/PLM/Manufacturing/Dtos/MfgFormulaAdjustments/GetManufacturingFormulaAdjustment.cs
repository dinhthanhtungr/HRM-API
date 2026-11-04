using System.Collections.Generic;
using System;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments
{
    public class GetManufacturingFormulaAdjustment
    {
        public Guid ManufacturingFormulaAdjustmentId { get; set; }
        public Guid MfgProductionOrderId { get; set; }
        public string? MfgProductionOrderExternalId { get; set; }
        public Guid? ManufacturingFormulaId { get; set; }
        public string? ManufacturingFormulaExternalId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }
        public Guid CompanyId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }
        public List<GetManufacturingFormulaAdjustmentBatch> Batches { get; set; } = new();
    }
}
