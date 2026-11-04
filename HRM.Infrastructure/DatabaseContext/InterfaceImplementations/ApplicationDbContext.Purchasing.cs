using HRM.Application.Abstractions.Persistence.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext : IPurchaseOrderDbContext
{
    public Task LockPurchaseOrderAsync(Guid purchaseOrderId, Guid companyId, CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is null) throw new InvalidOperationException("Purchase order lock requires a transaction.");
        return Database.ExecuteSqlInterpolatedAsync($"""
            SELECT 1 FROM "Orders"."PurchaseOrders"
            WHERE "PurchaseOrderId" = {purchaseOrderId} AND "CompanyId" = {companyId}
            FOR UPDATE
            """, cancellationToken);
    }
}
