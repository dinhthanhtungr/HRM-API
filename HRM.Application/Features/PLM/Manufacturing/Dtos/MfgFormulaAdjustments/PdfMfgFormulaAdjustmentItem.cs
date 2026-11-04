using System;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments
{
    public class PdfMfgFormulaAdjustmentItem
    {
        public Guid CategoryId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public decimal? BaseQuantity { get; set; }
        public decimal? AdjustedQuantity { get; set; }
        public decimal? DeltaQuantity { get; set; }
        public string? Unit { get; set; }
        public string? LotNo { get; set; }
        public string? Note { get; set; }
        public int LineNo { get; set; }
    }
}
