using System.Collections.Generic;
using System.Text.Json.Serialization;
using System;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Application.Features.PLM.Manufacturing.Dtos.MfgFormulaAdjustments
{
    public class GetManufacturingFormulaAdjustmentBatch
    {
        public Guid ManufacturingFormulaAdjustmentBatchId { get; set; }
        public Guid ManufacturingFormulaAdjustmentId { get; set; }
        public int BatchNo { get; set; }
        public int TrialNo { get; set; }
        public string Status { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AdjustmentType AdjustmentType { get; set; }
        public bool IsAdditionalBatch { get; set; }
        public decimal? BatchQuantity { get; set; }
        public decimal? TakenQuantityGram { get; set; }
        public decimal? FinalQuantityGram { get; set; }
        public bool ApplyToNextBatches { get; set; }
        public string? Note { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }
        public List<GetManufacturingFormulaAdjustmentItem> Items { get; set; } = new();
    }
}
