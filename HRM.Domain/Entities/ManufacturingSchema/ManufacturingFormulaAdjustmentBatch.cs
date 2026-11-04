using System;
using System.Collections.Generic;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.Manufacturings;

namespace HRM.Domain.Entities.ManufacturingSchema
{
    public class ManufacturingFormulaAdjustmentBatch
    {
        public Guid ManufacturingFormulaAdjustmentBatchId { get; set; }
        public Guid ManufacturingFormulaAdjustmentId { get; set; }

        public int BatchNo { get; set; }
        public int TrialNo { get; set; }
        public string Status { get; set; } = AdjustmentStatus.Draft.ToString();
        public AdjustmentType AdjustmentType { get; set; } = AdjustmentType.AddQuantity;
        public bool IsAdditionalBatch { get; set; } = false;
        public decimal? BatchQuantity { get; set; }
        public decimal? TakenQuantityGram { get; set; }
        public decimal? FinalQuantityGram { get; set; }
        public bool ApplyToNextBatches { get; set; }
        public string? Note { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public Guid? UpdatedBy { get; set; }

        public virtual ManufacturingFormulaAdjustment ManufacturingFormulaAdjustment { get; set; } = default!;
        public virtual Employee CreatedByNavigation { get; set; } = default!;
        public virtual Employee? UpdatedByNavigation { get; set; }
        public virtual ICollection<ManufacturingFormulaAdjustmentItem> Items { get; set; } = new List<ManufacturingFormulaAdjustmentItem>();
    }
}
