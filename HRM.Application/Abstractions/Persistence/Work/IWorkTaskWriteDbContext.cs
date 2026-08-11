using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.WorkTaskSchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace HRM.Application.Abstractions.Persistence.Work;

public interface IWorkTaskWriteDbContext
{
    DbSet<WorkTask> WorkTasks { get; }
    DbSet<WorkTaskList> WorkTaskLists { get; }
    DbSet<WorkTaskAssignee> WorkTaskAssignees { get; }
    DbSet<WorkTaskReference> WorkTaskReferences { get; }
    DbSet<Employee> Employees { get; }
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
