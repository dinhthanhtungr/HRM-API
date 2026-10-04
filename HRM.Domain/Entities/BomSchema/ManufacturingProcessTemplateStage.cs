namespace HRM.Domain.Entities.BomSchema;

public class ManufacturingProcessTemplateStage
{
    public Guid ManufacturingProcessTemplateStageId { get; set; }
    public Guid ManufacturingProcessTemplateId { get; set; }
    public Guid? ManufacturingWorkInstructionTemplateId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SequenceNo { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ManufacturingProcessTemplate ProcessTemplate { get; set; } = null!;
    public virtual ManufacturingWorkInstructionTemplate? WorkInstructionTemplate { get; set; }
    public virtual ICollection<ManufacturingProcessTemplateStageMachine> Machines { get; set; } = [];
    public virtual ICollection<ManufacturingProcessTemplateStageTransition> OutgoingTransitions { get; set; } = [];
    public virtual ICollection<ManufacturingProcessTemplateStageTransition> IncomingTransitions { get; set; } = [];
}
