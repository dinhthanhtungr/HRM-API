using HRM.Application.Abstractions.Persistence.Dispatch;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext :
    IDispatchReadDbContext,
    IDispatchWriteDbContext
{
}
