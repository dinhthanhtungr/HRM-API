namespace HRM.Domain.Entities.BomSchema;

public class ManufacturingBomStageWorkInstruction
{
    public Guid ManufacturingBomStageWorkInstructionId { get; set; }
    public Guid ManufacturingBomStageId { get; set; }
    public Guid? SourceWorkInstructionTemplateId { get; set; }
    public string ExternalIdSnapshot { get; set; } = string.Empty;
    public string NameSnapshot { get; set; } = string.Empty;
    public int VersionNoSnapshot { get; set; }
    public string? PurposeSnapshot { get; set; }
    public string? PreparationSnapshot { get; set; }
    public string ProcedureSnapshot { get; set; } = string.Empty;
    public string? QualityRequirementsSnapshot { get; set; }
    public string? SafetyNotesSnapshot { get; set; }
    public DateTime SnapshottedDate { get; set; }

    public virtual ManufacturingBomStage ManufacturingStage { get; set; } = null!;
    public virtual ICollection<ManufacturingBomStageChecklistItem> ChecklistItems { get; set; } = [];
}
