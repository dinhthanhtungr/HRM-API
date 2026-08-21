using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Enums.Boms;

namespace HRM.Domain.Entities.BomSchema;

/// <summary>
/// Định mức hao hụt gắn với phiên bản M-BOM, công đoạn hoặc dòng nguyên liệu.
/// </summary>
public class ManufacturingBomLossRule
{
    public Guid ManufacturingBomLossRuleId { get; set; }
    public Guid BomVersionId { get; set; }
    public Guid ManufacturingLossTypeId { get; set; }
    public Guid? BomVersionItemId { get; set; }
    public Guid? ManufacturingBomStageId { get; set; }
    public LossCalculationMethod CalculationMethod { get; set; }
    public decimal? RatePercent { get; set; }
    public decimal? FixedQuantityKg { get; set; }
    public decimal? QuantityPerEventKg { get; set; }
    public int? DefaultEventCount { get; set; }
    public int SequenceNo { get; set; }
    public bool IsRecoverable { get; set; }
    public bool IncludeInMaterialRequest { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }

    public virtual BomVersion BomVersion { get; set; } = null!;
    public virtual ManufacturingLossType LossType { get; set; } = null!;
    public virtual BomVersionItem? BomVersionItem { get; set; }
    public virtual ManufacturingBomStage? ManufacturingStage { get; set; }
    public virtual ICollection<MfgProductionOrderLoss> ProductionOrderLosses { get; set; } = new List<MfgProductionOrderLoss>();
}
