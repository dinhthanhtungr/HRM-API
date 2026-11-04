using System.Collections.Generic;
using System.Text.Json.Serialization;
using System;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments
{
    public class PostManufacturingFormulaAdjustmentBatch
    {
        public int BatchNo { get; set; }
        public int TrialNo { get; set; } = 1;
        public string Status { get; set; } = "Draft";

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AdjustmentType AdjustmentType { get; set; } = AdjustmentType.AddQuantity;
        public bool IsAdditionalBatch { get; set; }
        public decimal? BatchQuantity { get; set; }
        public decimal? TakenQuantityGram { get; set; }
        public decimal? FinalQuantityGram { get; set; }
        public bool ApplyToNextBatches { get; set; }
        public string? Note { get; set; }
        public DateTime? PreviewCreatedDate { get; set; }
        public List<PostManufacturingFormulaAdjustmentItem> Items { get; set; } = new();
    }
}
