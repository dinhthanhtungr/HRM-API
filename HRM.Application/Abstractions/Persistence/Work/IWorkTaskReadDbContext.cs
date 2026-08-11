using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.WorkTaskSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.Work;

public interface IWorkTaskReadDbContext
{
    DbSet<WorkTask> WorkTasks { get; }
    DbSet<WorkTaskList> WorkTaskLists { get; }
    DbSet<WorkTaskAssignee> WorkTaskAssignees { get; }
    DbSet<WorkTaskReference> WorkTaskReferences { get; }
    DbSet<Employee> Employees { get; }
}
