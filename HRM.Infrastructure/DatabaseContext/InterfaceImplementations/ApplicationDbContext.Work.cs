using HRM.Application.Abstractions.Persistence.Work;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext :
    IWorkTaskReadDbContext,
    IWorkTaskWriteDbContext
{
}
