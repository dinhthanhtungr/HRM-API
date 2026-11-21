using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Application.Features.Warehouse.Services;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Queries.GetStockJourney;

internal static class StockJourneyVoucherReader
{
    public static async Task<IReadOnlyDictionary<long, WarehouseVoucherBusinessTypeDto>> ReadAsync(
        IWarehouseReadDbContext db, IWarehouseVoucherBusinessReader reader, Guid company,
        IReadOnlyCollection<long> voucherIds, CancellationToken ct)
    {
        if (voucherIds.Count == 0) return new Dictionary<long, WarehouseVoucherBusinessTypeDto>();
        var inputs = await (from voucher in db.WarehouseVouchers.AsNoTracking()
            join request in db.WarehouseRequests.AsNoTracking() on voucher.RequestId equals (int?)request.RequestId
            where voucher.CompanyId == company && request.CompanyId == company && voucherIds.Contains(voucher.VoucherId)
            select new WarehouseVoucherBusinessInput(voucher.VoucherId, voucher.VoucherType, request.codeFromRequest)).ToArrayAsync(ct);
        var result = await reader.ReadAsync(company, inputs, ct);
        return result.Where(x => x.Value.BusinessType.Code != "Unknown").ToDictionary(x => x.Key, x => x.Value.BusinessType);
    }
}
