using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.WorkTaskEnums;

namespace HRM.Domain.Entities.WorkTaskSchema;

public sealed class WorkPlan
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string? Objective { get; set; }
    public string? Strategy { get; set; }
    public string? DiscussionSummary { get; set; }
    public string? NextAction { get; set; }
    public WorkPlanStatus Status { get; set; } = WorkPlanStatus.Active;
    public WorkTaskPriority Priority { get; set; } = WorkTaskPriority.Normal;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public Guid? AssignedToEmployeeId { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = default!;
    public Employee? AssignedToEmployee { get; set; }
    public Employee CreatedByNavigation { get; set; } = default!;
    public Employee? UpdatedByNavigation { get; set; }
    public ICollection<WorkPlanAssignee> Assignees { get; set; } = new List<WorkPlanAssignee>();
    public ICollection<WorkPlanReference> References { get; set; } = new List<WorkPlanReference>();
}
