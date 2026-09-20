using HRM.Domain.Entities.MROSchema;

namespace HRM.Domain.Entities.BomSchema;

/// <summary>Máy được dùng cho một công đoạn của đúng một M-BOM version.</summary>
public class ManufacturingBomStageMachine
{
    public Guid ManufacturingBomStageMachineId { get; set; }
    public Guid ManufacturingBomStageId { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentExternalIdSnapshot { get; set; } = string.Empty;
    public string EquipmentNameSnapshot { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int SequenceNo { get; set; }
    public string? Note { get; set; }

    public virtual ManufacturingBomStage ManufacturingStage { get; set; } = null!;
    public virtual EquipmentMRO Equipment { get; set; } = null!;
}
