using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Domain.Entities.WarehouseSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Queries.GetStockJourney;

internal static class StockJourneyDestinationReader
{
    public static async Task<IReadOnlySet<long>> VerifiedVoucherIssuesAsync(IWarehouseReadDbContext db, Guid company,
        IReadOnlyList<WarehouseMovementRow> rows, CancellationToken ct)
    {
        var candidates = rows.Where(x => x.CompanyId == company && x.DeltaKg < 0 && x.VoucherId.HasValue && x.VoucherDetailId.HasValue).ToArray();
        if (candidates.Length == 0) return new HashSet<long>();
        var ids = candidates.Select(x => x.VoucherDetailId!.Value).Distinct().ToArray();
        var details = await (from detail in db.WarehouseVoucherDetails.AsNoTracking()
            join voucher in db.WarehouseVouchers.AsNoTracking() on detail.VoucherId equals voucher.VoucherId
            where voucher.CompanyId == company && voucher.Status != null && voucher.Status!.ToUpper() == "APPLIED"
                && ids.Contains(detail.VoucherDetailId) && !detail.IsIncrease
            select new { detail.VoucherDetailId, detail.VoucherId, detail.ProductCode, detail.LotNumber }).ToArrayAsync(ct);
        var map = details.ToDictionary(x => x.VoucherDetailId);
        return candidates.Where(row => map.TryGetValue(row.VoucherDetailId!.Value, out var detail)
            && detail.VoucherId == row.VoucherId
            && !string.IsNullOrWhiteSpace(row.ProductCode)
            && string.Equals(detail.ProductCode?.Trim(), row.ProductCode?.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(detail.LotNumber?.Trim(), row.LotNumber?.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(x => x.LedgerId).ToHashSet();
    }

    public static string Status(StockMovementDto movement, IReadOnlyList<WarehouseMovementRow> rows, IReadOnlySet<long> verified)
    {
        if (movement.OutKg <= 0) return "NotApplicable";
        if (movement.ToShelves.Count > 0) return "InternalDestination";
        var outgoing = rows.Where(x => x.DeltaKg < 0).ToArray();
        return outgoing.Length > 0 && outgoing.All(x => verified.Contains(x.LedgerId))
            ? "VerifiedVoucherIssue" : "Unresolved";
    }
}
