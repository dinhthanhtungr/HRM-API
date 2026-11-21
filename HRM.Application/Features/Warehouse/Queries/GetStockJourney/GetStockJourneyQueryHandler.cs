using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.Warehouse.Dtos;
using HRM.Application.Features.Warehouse.Services;
using HRM.Domain.Entities.WarehouseSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Warehouse.Queries.GetStockJourney;

internal sealed class GetStockJourneyQueryHandler(IWarehouseReadDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, IWarehouseVoucherBusinessReader voucherBusinessReader) : IRequestHandler<GetStockJourneyQuery, OperationResult<StockJourneyDto>>
{
    private const int PageSize = 40;

    public async Task<OperationResult<StockJourneyDto>> Handle(GetStockJourneyQuery request, CancellationToken ct)
    {
        using var timing = new WarehouseMovementQueryTiming("wj");
        if (!StockMovementReader.CanRead(user) || string.IsNullOrWhiteSpace(request.MovementKey) ||
            request.MovementKey.Length > 100 || request.LedgerId <= 0 || request.PageNumber is < 1 or > 1000000)
            return OperationResult<StockJourneyDto>.Fail("Không tìm thấy hành trình tồn kho.");
        var canViewMixing = permissions.HasPermission(ApplicationPermissions.Warehouse.ViewMixingStock);
        var companyRows = db.ReadMovementHistory(user.CompanyId!.Value).Where(x => x.CompanyId == user.CompanyId);
        // Narrow by complete movement keys BEFORE the mixing exclusion. Never narrow by ledger/lot
        // first: a different line in the same movement may be the restricted CT leg.
        timing.RecordScope();
        var movementKey = await timing.RunAsync("key", () => StockMovementReader.ResolveLegacyKeyAsync(VisibleGroups(companyRows, canViewMixing), request.MovementKey, ct));
        var seedGroup = VisibleGroups(companyRows.Where(x => x.MovementKey == movementKey), canViewMixing);
        var seed = await timing.RunAsync("seed", () => seedGroup.Where(x => x.LedgerId == request.LedgerId)
            .Select(x => new { x.ProductCode, x.LotNumber })
            .FirstOrDefaultAsync(ct));
        if (seed == null || string.IsNullOrWhiteSpace(seed.ProductCode))
            return OperationResult<StockJourneyDto>.Fail("Không tìm thấy hành trình tồn kho.");

        var code = seed.ProductCode.Trim().ToUpperInvariant();
        var lot = seed.LotNumber?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(lot))
            return OperationResult<StockJourneyDto>.Ok(new StockJourneyDto(code, null, movementKey, false,
                !canViewMixing, null, null, new PagedResult<StockJourneyEventDto>([], 0, 1, PageSize), [], []));

        // Cross stock types, but never cross company, product or lot. Null lots cannot identify a batch.
        var matchingKeys = companyRows.Where(x => x.ProductCode == code && x.LotNumber == lot)
            .Select(x => x.MovementKey).Distinct();
        var visible = VisibleGroups(companyRows.Where(x => matchingKeys.Contains(x.MovementKey)), canViewMixing);
        var lotRows = visible.Where(x => x.ProductCode == code && x.LotNumber == lot);
        // MovementAt is a window aggregate in the canonical view. Referencing it forces PostgreSQL
        // to compute windows for the whole company before applying the lot filter. Its exact value
        // is MIN(CreatedAt) over the WHOLE movement, including other products/lots in that group.
        // Keep that scope, but only use plain ledger columns so unused view windows can be pruned.
        // Compute CT visibility as a whole-group aggregate here, avoiding repeated anti-subqueries
        // over the company history in the statistics and page-key queries.
        var groups = companyRows.Where(x => matchingKeys.Contains(x.MovementKey)).GroupBy(x => x.MovementKey)
            .Select(g => new
            {
                Key = g.Key,
                At = g.Min(x => x.CreatedAt),
                MixingLegs = g.Max(x => x.Scope == "CT" ? 1 : 0),
                FirstId = g.Min(x => x.ProductCode == code && x.LotNumber == lot ? (long?)x.LedgerId : null)
            }).Where(x => x.FirstId.HasValue && (canViewMixing || x.MixingLegs == 0));
        var statistics = await timing.RunAsync("statistics", () => groups.GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), FirstAt = g.Min(x => x.At), LastAt = g.Max(x => x.At) })
            .FirstOrDefaultAsync(ct));
        var count = statistics?.Count ?? 0;
        var firstAt = statistics?.FirstAt;
        var lastAt = statistics?.LastAt;
        var keys = await timing.RunAsync("page", () => groups.OrderBy(x => x.At).ThenBy(x => x.FirstId)
            .Skip((request.PageNumber - 1) * PageSize).Take(PageSize).Select(x => x.Key).ToArrayAsync(ct));
        var rows = await timing.RunAsync("rows", () => VisibleGroups(companyRows.Where(x => keys.Contains(x.MovementKey)), canViewMixing)
            .Where(x => x.ProductCode == code && x.LotNumber == lot).OrderBy(x => x.LedgerId).ToArrayAsync(ct));
        var context = await timing.RunAsync("context", () => StockMovementContextReader.ReadAsync(db, user.CompanyId!.Value, rows, ct, timing));
        var verifiedVoucherIssues = await timing.RunAsync("verification", () => StockJourneyDestinationReader.VerifiedVoucherIssuesAsync(db, user.CompanyId.Value, rows, ct));
        var voucherBusinessTypes = await timing.RunAsync("business", () => StockJourneyVoucherReader.ReadAsync(db, voucherBusinessReader, user.CompanyId.Value,
            rows.Where(row => StockMovementReader.Category(row, context) == "Voucher")
                .Select(x => x.VoucherId).OfType<long>().Distinct().ToArray(), ct));
        var byKey = timing.Run("mapping", () => rows.GroupBy(x => x.MovementKey).ToDictionary(x => x.Key, x =>
        {
            var movement = StockMovementReader.Summarize(x.ToArray(), context);
            if (movement.BusinessType.Code == "Voucher" && movement.NetKg != 0)
                movement = movement with { BusinessType = movement.NetKg > 0
                    ? new("Receipt", "Nhập kho theo phiếu", "Voucher receipt", "success")
                    : new("Issue", "Xuất kho theo phiếu", "Voucher issue", "warning") };
            var lines = x.Select(line => new StockMovementLineDto(line.LedgerId, line.CreatedAt, line.ShelfStockCode,
                line.Scope, line.ProductCode, line.LotNumber, line.StockType, line.UnitName, line.BeforeKg, line.DeltaKg,
                line.AfterKg, line.CreatedBy, line.CreatedBy.HasValue ? context.Employees.GetValueOrDefault(line.CreatedBy.Value) : null,
                line.RequestCode, line.AppSource, line.VoucherId,
                line.VoucherId.HasValue ? context.Vouchers.GetValueOrDefault(line.VoucherId.Value) : null)).ToArray();
            return new StockJourneyEventDto(movement, lines)
            {
                DestinationStatus = StockJourneyDestinationReader.Status(movement, x.ToArray(), verifiedVoucherIssues),
                VoucherBusinessType = movement.BusinessType.Code is "Receipt" or "Issue"
                    && x.All(row => row.VoucherId.HasValue) && x.Select(row => row.VoucherId).Distinct().Count() == 1
                    ? voucherBusinessTypes.GetValueOrDefault(x.First().VoucherId!.Value) : null
            };
        }));
        var locations = await timing.RunAsync("locations", () => StockJourneyCurrentReader.LocationsAsync(db, user.CompanyId.Value, code, lot, canViewMixing, ct));
        var allocations = await timing.RunAsync("allocations", () => StockJourneyCurrentReader.AllocationsAsync(db, lotRows, user.CompanyId.Value, code, lot, ct));
        return OperationResult<StockJourneyDto>.Ok(new StockJourneyDto(code, lot, movementKey, true, !canViewMixing,
            firstAt, lastAt, new PagedResult<StockJourneyEventDto>(keys.Select(x => byKey[x]).ToArray(), count, request.PageNumber, PageSize),
            locations, allocations));
    }

    private static IQueryable<WarehouseMovementRow> VisibleGroups(
        IQueryable<WarehouseMovementRow> completeGroups, bool canViewMixing)
    {
        if (canViewMixing) return completeGroups;
        var restricted = completeGroups.Where(x => x.Scope == "CT").Select(x => x.MovementKey);
        return completeGroups.Where(x => !restricted.Contains(x.MovementKey));
    }
}
