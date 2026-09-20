using HRM.Domain.Enums.Boms;

namespace HRM.Domain.Entities.BomSchema;

/// <summary>Chuyển công đoạn/chuyển máy thuộc một M-BOM version.</summary>
public class ManufacturingBomStageTransition
{
    public Guid ManufacturingBomStageTransitionId { get; set; }
    public Guid BomVersionId { get; set; }
    public Guid FromManufacturingBomStageId { get; set; }
    public Guid ToManufacturingBomStageId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public ManufacturingStageTransitionType TransitionType { get; set; }
    public int? DefaultEventCount { get; set; }
    public int SequenceNo { get; set; }
    public string? Note { get; set; }

    public virtual BomVersion BomVersion { get; set; } = null!;
    public virtual ManufacturingBomStage FromStage { get; set; } = null!;
    public virtual ManufacturingBomStage ToStage { get; set; } = null!;
    public virtual ICollection<ManufacturingBomLossRule> LossRules { get; set; } = new List<ManufacturingBomLossRule>();
}
