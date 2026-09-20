namespace HRM.Domain.Entities.BomSchema;

public class ManufacturingBomStageChecklistItem
{
    public Guid ManufacturingBomStageChecklistItemId { get; set; }
    public Guid ManufacturingBomStageWorkInstructionId { get; set; }
    public Guid? SourceChecklistItemId { get; set; }
    public string ExternalIdSnapshot { get; set; } = string.Empty;
    public string ContentSnapshot { get; set; } = string.Empty;
    public int SequenceNo { get; set; }
    public bool IsRequired { get; set; }
    public string? ExpectedValueSnapshot { get; set; }
    public string? UnitSnapshot { get; set; }

    public virtual ManufacturingBomStageWorkInstruction WorkInstruction { get; set; } = null!;
}
