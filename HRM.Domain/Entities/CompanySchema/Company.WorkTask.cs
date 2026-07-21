using HRM.Domain.Entities.WorkTaskSchema;

namespace HRM.Domain.Entities.CompanySchema;

public partial class Company
{
    public ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();
    public ICollection<WorkTaskList> WorkTaskLists { get; set; } = new List<WorkTaskList>();
    public ICollection<WorkPlan> WorkPlans { get; set; } = new List<WorkPlan>();
}
