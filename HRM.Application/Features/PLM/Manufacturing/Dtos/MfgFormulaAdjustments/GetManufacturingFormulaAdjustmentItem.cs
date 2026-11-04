using System.Text.Json.Serialization;
using System;
using HRM.Domain.Enums.Formulas;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments
{
    public class GetManufacturingFormulaAdjustmentItem
    {
        public Guid ManufacturingFormulaAdjustmentItemId { get; set; }
        public Guid ManufacturingFormulaAdjustmentBatchId { get; set; }
        public Guid? MaterialId { get; set; }
        public Guid? ProductId { get; set; }
        public Guid CategoryId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ItemType itemType { get; set; }
        public decimal? BaseQuantity { get; set; }
        public decimal? AdjustedQuantity { get; set; }
        public decimal? DeltaQuantity { get; set; }
        public string? Unit { get; set; }
        public string? LotNo { get; set; }
        public string? Note { get; set; }
        public int LineNo { get; set; }
        public string? MaterialNameSnapshot { get; set; }
        public string? MaterialExternalIdSnapshot { get; set; }
        public string? ProductNameSnapshot { get; set; }
        public string? ProductExternalIdSnapshot { get; set; }
    }
}
