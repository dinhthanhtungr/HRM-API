using HRM.Domain.Entities.WorkTaskSchema;

namespace HRM.Domain.Entities.HrSchema;

public partial class Employee
{
    public ICollection<WorkTask> WorkTaskCompletedByNavigations { get; set; } = new List<WorkTask>();
    public ICollection<WorkTask> WorkTaskAssignedToEmployeeNavigations { get; set; } = new List<WorkTask>();
    public ICollection<WorkTask> WorkTaskCreatedByNavigations { get; set; } = new List<WorkTask>();
    public ICollection<WorkTask> WorkTaskUpdatedByNavigations { get; set; } = new List<WorkTask>();
    public ICollection<WorkTaskList> WorkTaskListOwnerEmployeeNavigations { get; set; } = new List<WorkTaskList>();
    public ICollection<WorkTaskList> WorkTaskListCreatedByNavigations { get; set; } = new List<WorkTaskList>();
    public ICollection<WorkTaskList> WorkTaskListUpdatedByNavigations { get; set; } = new List<WorkTaskList>();
    public ICollection<WorkTaskAssignee> WorkTaskAssigneeEmployees { get; set; } = new List<WorkTaskAssignee>();
    public ICollection<WorkTaskAssignee> WorkTaskAssigneeCreatedByNavigations { get; set; } = new List<WorkTaskAssignee>();
    public ICollection<WorkPlan> WorkPlanAssignedToEmployeeNavigations { get; set; } = new List<WorkPlan>();
    public ICollection<WorkPlan> WorkPlanCreatedByNavigations { get; set; } = new List<WorkPlan>();
    public ICollection<WorkPlan> WorkPlanUpdatedByNavigations { get; set; } = new List<WorkPlan>();
    public ICollection<WorkPlanAssignee> WorkPlanAssigneeEmployees { get; set; } = new List<WorkPlanAssignee>();
    public ICollection<WorkPlanAssignee> WorkPlanAssigneeCreatedByNavigations { get; set; } = new List<WorkPlanAssignee>();
}
