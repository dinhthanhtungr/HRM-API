using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Boms;

namespace HRM.Domain.Entities.ManufacturingSchema;

/// <summary>
/// Snapshot hao hụt kế hoạch và thực tế của một lệnh sản xuất.
/// </summary>
public class MfgProductionOrderLoss
{
    public Guid MfgProductionOrderLossId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid MfgProductionOrderId { get; set; }
    public Guid? SourceManufacturingBomLossRuleId { get; set; }

    public string LossTypeCodeSnapshot { get; set; } = string.Empty;
    public string LossTypeNameSnapshot { get; set; } = string.Empty;
    public LossCalculationMethod CalculationMethodSnapshot { get; set; }
    public string? StageCodeSnapshot { get; set; }
    public string? MaterialCodeSnapshot { get; set; }
    public decimal? RatePercentSnapshot { get; set; }
    public decimal? FixedQuantityKgSnapshot { get; set; }
    public decimal? QuantityPerEventKgSnapshot { get; set; }

    public decimal PlannedQuantityKg { get; set; }
    public decimal? ActualQuantityKg { get; set; }
    public int? EventCount { get; set; }
    public decimal RecoveredQuantityKg { get; set; }
    public bool IsFinalized { get; set; }
    public string? Note { get; set; }
    public DateTime RecordedDate { get; set; }
    public Guid RecordedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }

    public virtual MfgProductionOrder ProductionOrder { get; set; } = null!;
    public virtual ManufacturingBomLossRule? SourceLossRule { get; set; }
}
