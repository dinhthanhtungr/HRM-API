using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.WorkTaskEnums;

namespace HRM.Domain.Entities.WorkTaskSchema;

/// <summary>
/// Cong viec dung chung. Nghiep vu cha duoc lien ket qua WorkTaskReference thay vi FK chuyen biet.
/// </summary>
public sealed class WorkTask
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? NextAction { get; set; }
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.Pending;
    public WorkTaskPriority Priority { get; set; } = WorkTaskPriority.Normal;
    public DateTime? DueDate { get; set; }
    public DateTime? DueReminderSentAt { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid? CompletedBy { get; set; }
    public string? CompletionNote { get; set; }
    public Guid? AssignedToEmployeeId { get; set; }
    public Guid? WorkTaskListId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = default!;
    public Employee? CompletedByNavigation { get; set; }
    public Employee? AssignedToEmployee { get; set; }
    public WorkTaskList? WorkTaskList { get; set; }
    public Employee CreatedByNavigation { get; set; } = default!;
    public Employee? UpdatedByNavigation { get; set; }
    public ICollection<WorkTaskAssignee> Assignees { get; set; } = new List<WorkTaskAssignee>();
    public ICollection<WorkTaskReference> References { get; set; } = new List<WorkTaskReference>();
}
