using System.Collections.Generic;
using System;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments
{
    public class PdfMfgFormulaAdjustmentBatch
    {
        public Guid ManufacturingFormulaAdjustmentBatchId { get; set; }
        public int BatchNo { get; set; }
        public int TrialNo { get; set; }
        public AdjustmentType AdjustmentType { get; set; }
        public bool IsAdditionalBatch { get; set; }
        public decimal? BatchQuantity { get; set; }
        public decimal? TakenQuantityGram { get; set; }
        public decimal? FinalQuantityGram { get; set; }
        public string? Note { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<PdfMfgFormulaAdjustmentItem> Items { get; set; } = new();
    }
}
