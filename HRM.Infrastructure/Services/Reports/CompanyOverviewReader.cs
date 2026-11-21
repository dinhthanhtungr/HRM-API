using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Reports.CompanyOverview;
using HRM.Application.Features.Warehouse.Helpers.Publics;
using HRM.Application.Features.Warehouse.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Domain.Enums.WareHouses;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace HRM.Infrastructure.Services.Reports;

internal sealed class CompanyOverviewReader(
    ApplicationDbContext db, IWarehouseReadDbContext warehouse,
    IWarehouseStockVisibilityService visibility, ICurrentUser user, ICurrentUserPermissionService permissions)
    : ICompanyOverviewReader
{
    private void RequireAccess(Guid companyId)
    {
        if (!CompanyOverviewAccess.CanRead(user, permissions, companyId))
            throw new UnauthorizedAccessException("Company overview access denied.");
    }

    public async Task<IReadOnlyList<CompanyPurchaseFact>> ReadPurchasesAsync(
        Guid companyId, DateTime from, DateTime toExclusive, CancellationToken ct)
    {
        RequireAccess(companyId);
        // Unmapped read model: no MRO schema ownership, migration or tracked entities.
        var facts = await ReadFactsAsync(CompanyPurchaseSql.Facts, companyId, from, toExclusive, ct);
        var supplyOrders = await ReadFactsAsync(CompanySupplyPurchaseSql.Facts, companyId, from, toExclusive, ct);
        return CompanyPurchaseCalculator.ReplaceSupplyOrders(facts, supplyOrders);
    }

    private async Task<List<CompanyPurchaseFact>> ReadFactsAsync(
        string sql, Guid companyId, DateTime from, DateTime toExclusive, CancellationToken ct)
    {
        return await db.Database.SqlQueryRaw<CompanyPurchaseFact>(sql,
            new NpgsqlParameter("company", companyId),
            new NpgsqlParameter("from", NpgsqlDbType.Timestamp) { Value = DateTime.SpecifyKind(from, DateTimeKind.Unspecified) },
            new NpgsqlParameter("until", NpgsqlDbType.Timestamp) { Value = DateTime.SpecifyKind(toExclusive, DateTimeKind.Unspecified) })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CompanyStockDto>> ReadCurrentStockAsync(Guid companyId, CancellationToken ct)
    {
        RequireAccess(companyId);
        var query = WarehouseStockQueryHelper.ActiveShelfStocks(warehouse.WarehouseShelfStocks.AsNoTracking(), companyId)
            .Where(x => x.WarehouseShelves!.CompanyId == companyId
                && (x.StockType == StockType.RawMaterial || x.StockType == StockType.DefectiveRawMaterial
                    || x.StockType == StockType.FinishedGood || x.StockType == StockType.DefectiveFinishedGood));
        if (!permissions.HasPermission(ApplicationPermissions.Warehouse.ViewMixingStock))
            query = query.Where(x => x.ShelfStockCode != WarehouseStockQueryHelper.MixingShelfCode
                && x.WarehouseShelves!.SlotCode != WarehouseStockQueryHelper.MixingShelfCode);
        query = await visibility.ApplyAsync(query, companyId, null, ct);
        var totals = await query.GroupBy(x => x.StockType)
            .Select(x => new { StockType = x.Key, Quantity = x.Sum(r => r.QtyKg) }).ToListAsync(ct);
        return totals.Select(x => new CompanyStockDto(x.StockType.ToString(), x.Quantity)).ToArray();
    }
}
