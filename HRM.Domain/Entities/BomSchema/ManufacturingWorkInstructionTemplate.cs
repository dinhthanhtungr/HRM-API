using HRM.Domain.Enums.Boms;

namespace HRM.Domain.Entities.BomSchema;

public class ManufacturingWorkInstructionTemplate
{
    public Guid ManufacturingWorkInstructionTemplateId { get; set; }
    public Guid CompanyId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int VersionNo { get; set; } = 1;
    public ManufacturingTemplateStatus Status { get; set; } = ManufacturingTemplateStatus.Draft;
    public string? Purpose { get; set; }
    public string? Preparation { get; set; }
    public string Procedure { get; set; } = string.Empty;
    public string? QualityRequirements { get; set; }
    public string? SafetyNotes { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? ReleasedDate { get; set; }
    public Guid? ReleasedBy { get; set; }

    public virtual ICollection<ManufacturingWorkInstructionChecklistItem> ChecklistItems { get; set; } = [];
    public virtual ICollection<ManufacturingProcessTemplateStage> ProcessStages { get; set; } = [];
}
