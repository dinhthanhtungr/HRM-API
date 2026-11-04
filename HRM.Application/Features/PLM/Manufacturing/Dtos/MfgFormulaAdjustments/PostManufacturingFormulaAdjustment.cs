using System.Collections.Generic;
using System;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments
{
    public class PostManufacturingFormulaAdjustment
    {
        public Guid MfgProductionOrderId { get; set; }
        public Guid? ManufacturingFormulaId { get; set; }
        public string Status { get; set; } = "Draft";
        public string? Note { get; set; }
        public DateTime? PreviewCreatedDate { get; set; }
        public List<PostManufacturingFormulaAdjustmentBatch> Batches { get; set; } = new();
    }
}
