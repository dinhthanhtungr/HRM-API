using HRM.Domain.Enums.WorkTaskEnums;

namespace HRM.Domain.Entities.WorkTaskSchema;

public sealed class WorkPlanReference
{
    public Guid Id { get; set; }
    public Guid WorkPlanId { get; set; }
    public WorkReferenceType ReferenceType { get; set; }
    public Guid ReferenceId { get; set; }
    public string? ReferenceCodeSnapshot { get; set; }
    public string? ReferenceNameSnapshot { get; set; }
    public bool IsPrimary { get; set; }

    public WorkPlan WorkPlan { get; set; } = default!;
}
