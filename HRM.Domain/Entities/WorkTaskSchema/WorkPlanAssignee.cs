using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.WorkTaskSchema;

public sealed class WorkPlanAssignee
{
    public Guid Id { get; set; }
    public Guid WorkPlanId { get; set; }
    public Guid EmployeeId { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }

    public WorkPlan WorkPlan { get; set; } = default!;
    public Employee Employee { get; set; } = default!;
    public Employee CreatedByNavigation { get; set; } = default!;
}
