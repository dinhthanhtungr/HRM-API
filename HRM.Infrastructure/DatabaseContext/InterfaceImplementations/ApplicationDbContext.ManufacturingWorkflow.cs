using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext : IManufacturingDbContext
{
    public async Task LockProductionOrderAsync(Guid productionOrderId, Guid companyId, CancellationToken ct = default)
        => await LockManufacturingResourceAsync($"manufacturing:{companyId:N}:{productionOrderId:N}", ct);

    public async Task LockManufacturingFormulaAsync(Guid formulaId, Guid companyId, CancellationToken ct = default)
        => await LockManufacturingResourceAsync($"manufacturing-formula:{companyId:N}:{formulaId:N}", ct);

    public async Task LockManufacturingProductAsync(Guid productId, Guid companyId, CancellationToken ct = default)
        => await LockManufacturingResourceAsync($"manufacturing-product:{companyId:N}:{productId:N}", ct);
    public async Task LockManufacturingColorChipAsync(Guid recordId, Guid companyId, CancellationToken ct = default)
        => await LockManufacturingResourceAsync($"manufacturing-color-chip:{companyId:N}:{recordId:N}", ct);

    private async Task LockManufacturingResourceAsync(string key, CancellationToken ct)
    {
        if (Database.CurrentTransaction is null)
            throw new InvalidOperationException("Manufacturing mutation requires an active transaction.");
        await Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    }
}
