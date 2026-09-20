namespace HRM.Domain.Entities.BomSchema;

public class ManufacturingWorkInstructionChecklistItem
{
    public Guid ManufacturingWorkInstructionChecklistItemId { get; set; }
    public Guid ManufacturingWorkInstructionTemplateId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int SequenceNo { get; set; }
    public bool IsRequired { get; set; }
    public string? ExpectedValue { get; set; }
    public string? Unit { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ManufacturingWorkInstructionTemplate WorkInstructionTemplate { get; set; } = null!;
}
