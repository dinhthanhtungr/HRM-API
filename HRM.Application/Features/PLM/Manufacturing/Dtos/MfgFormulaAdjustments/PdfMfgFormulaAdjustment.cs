using System.Collections.Generic;
using System;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments
{
    public class PdfMfgFormulaAdjustment
    {
        public Guid ManufacturingFormulaAdjustmentId { get; set; }
        public string? ProductionOrderCode { get; set; }
        public string? ProductCode { get; set; }
        public string? ProductName { get; set; }
        public string? FormulaCode { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<PdfMfgFormulaAdjustmentBatch> Batches { get; set; } = new();
    }
}
