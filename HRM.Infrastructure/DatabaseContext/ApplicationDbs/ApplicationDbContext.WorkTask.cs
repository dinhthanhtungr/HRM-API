using HRM.Domain.Entities.WorkTaskSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<WorkTaskList> WorkTaskLists => Set<WorkTaskList>();
    public DbSet<WorkTaskAssignee> WorkTaskAssignees => Set<WorkTaskAssignee>();
    public DbSet<WorkTaskReference> WorkTaskReferences => Set<WorkTaskReference>();
    public DbSet<WorkPlan> WorkPlans => Set<WorkPlan>();
    public DbSet<WorkPlanAssignee> WorkPlanAssignees => Set<WorkPlanAssignee>();
    public DbSet<WorkPlanReference> WorkPlanReferences => Set<WorkPlanReference>();
}
