using HRM.Domain.Entities.MROSchema;

namespace HRM.Domain.Entities.BomSchema;

public class ManufacturingProcessTemplateStageMachine
{
    public Guid ManufacturingProcessTemplateStageMachineId { get; set; }
    public Guid ManufacturingProcessTemplateStageId { get; set; }
    public int EquipmentId { get; set; }
    public Guid? ConfigurationGroupKey { get; set; }
    public string? ConfigurationGroupName { get; set; }
    public bool IsDefault { get; set; }
    public int SequenceNo { get; set; }
    public string? Note { get; set; }

    public virtual ManufacturingProcessTemplateStage Stage { get; set; } = null!;
    public virtual EquipmentMRO Equipment { get; set; } = null!;
    public virtual ICollection<ManufacturingProcessTemplateStageMachineParameter> Parameters { get; set; } = [];
}
