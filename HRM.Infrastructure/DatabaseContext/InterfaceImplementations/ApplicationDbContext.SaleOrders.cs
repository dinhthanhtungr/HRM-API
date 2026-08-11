using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext : ISaleOrderDbContext
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Database.BeginTransactionAsync(cancellationToken);
    }
}
