using HRM.Application.Abstractions.Persistence.Dispatch;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext :
    IDispatchReadDbContext,
    IDispatchWriteDbContext
{
    public Task<IDbContextTransaction> BeginDeliveryOrderTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        return Database.BeginTransactionAsync(cancellationToken);
    }
}
