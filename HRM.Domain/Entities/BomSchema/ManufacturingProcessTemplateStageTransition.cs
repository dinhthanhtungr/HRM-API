using HRM.Domain.Enums.Boms;

namespace HRM.Domain.Entities.BomSchema;

/// <summary>Đường chuyển giữa hai công đoạn trong một phiên bản process template.</summary>
public class ManufacturingProcessTemplateStageTransition
{
    public Guid ManufacturingProcessTemplateStageTransitionId { get; set; }
    public Guid ManufacturingProcessTemplateId { get; set; }
    public Guid FromManufacturingProcessTemplateStageId { get; set; }
    public Guid ToManufacturingProcessTemplateStageId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ManufacturingStageTransitionType TransitionType { get; set; }
    public int? DefaultEventCount { get; set; }
    public int SequenceNo { get; set; }
    public string? Note { get; set; }

    public virtual ManufacturingProcessTemplate ProcessTemplate { get; set; } = null!;
    public virtual ManufacturingProcessTemplateStage FromStage { get; set; } = null!;
    public virtual ManufacturingProcessTemplateStage ToStage { get; set; } = null!;
}
