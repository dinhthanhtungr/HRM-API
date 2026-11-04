using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    public async Task LockMaterialSupplierPricesAsync(Guid materialId, Guid companyId, CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is null) throw new InvalidOperationException("Pricing mutation requires an active transaction.");
        await Database.ExecuteSqlInterpolatedAsync($"SELECT \"MaterialId\" FROM \"Material\".\"Materials\" WHERE \"MaterialId\"={materialId} AND \"CompanyId\"={companyId} FOR UPDATE", cancellationToken);
        await Database.ExecuteSqlInterpolatedAsync($"SELECT l.\"Materials_SuppliersId\" FROM \"Material\".\"Materials_Suppliers\" l JOIN \"Material\".\"Materials\" m ON m.\"MaterialId\"=l.\"MaterialId\" WHERE m.\"MaterialId\"={materialId} AND m.\"CompanyId\"={companyId} ORDER BY l.\"Materials_SuppliersId\" FOR UPDATE OF l", cancellationToken);
    }
    public async Task LockPurchasingCatalogAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is null)
            throw new InvalidOperationException("Catalog mutation requires an active transaction.");
        var key = $"purchasing-catalog:{companyId:N}";
        await Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }
}
