using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Boms;
using HRM.Domain.Enums.Formulas;

namespace HRM.Domain.Entities.BomSchema;

/// <summary>
/// Rule hao hụt trong một profile. Target stage/transition dùng code để có thể map
/// sang công đoạn thuộc các M-BOM version khác nhau.
/// </summary>
public class ManufacturingLossProfileRule
{
    public Guid ManufacturingLossProfileRuleId { get; set; }
    public Guid ManufacturingLossProfileId { get; set; }
    public Guid ManufacturingLossTypeId { get; set; }
    public Guid? MaterialId { get; set; }
    public ManufacturingLossScope Scope { get; set; }
    public string? TargetStageCode { get; set; }
    public string? FromStageCode { get; set; }
    public string? ToStageCode { get; set; }
    public ManufacturingLossAllocationMethod AllocationMethod { get; set; }
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

    public virtual ManufacturingLossProfile Profile { get; set; } = null!;
    public virtual ManufacturingLossType LossType { get; set; } = null!;
    public virtual Material? Material { get; set; }
}
