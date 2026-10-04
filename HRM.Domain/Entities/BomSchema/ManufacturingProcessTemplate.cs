using HRM.Domain.Enums.Boms;

namespace HRM.Domain.Entities.BomSchema;

public class ManufacturingProcessTemplate
{
    public Guid ManufacturingProcessTemplateId { get; set; }
    public Guid CompanyId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int VersionNo { get; set; } = 1;
    public ManufacturingTemplateStatus Status { get; set; } = ManufacturingTemplateStatus.Draft;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? ReleasedDate { get; set; }
    public Guid? ReleasedBy { get; set; }

    public virtual ICollection<ManufacturingProcessTemplateStage> Stages { get; set; } = [];
    public virtual ICollection<ManufacturingProcessTemplateStageTransition> StageTransitions { get; set; } = [];
    public virtual ICollection<ManufacturingProcessTemplateApplicability> ApplicabilityRules { get; set; } = [];
}
