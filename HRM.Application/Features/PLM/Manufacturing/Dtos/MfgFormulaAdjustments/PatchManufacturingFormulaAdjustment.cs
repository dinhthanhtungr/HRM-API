using System.Collections.Generic;
using System;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments
{
    public class PatchManufacturingFormulaAdjustment
    {
        public Guid? ManufacturingFormulaId { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
        public List<PostManufacturingFormulaAdjustmentBatch>? Batches { get; set; }
    }
}
