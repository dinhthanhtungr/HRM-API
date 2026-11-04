using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.Dispatch.DeliveryOrders;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryPlan;
using HRM.Domain.Enums.Deliveries;
using HRM.Domain.Enums.Merchadises;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.DeliveryOtd;

internal static class DeliveryOtdReader
{
    internal static readonly DateTime OrderStartDate = new(2026, 3, 1);
    internal static bool IsValid(DeliveryOtdFilter filter, ICurrentUser user, DateTime today)
        => DeliveryOrderAccessRules.CanRead(user) && user.CompanyId is { } company && company != Guid.Empty
        && Enum.IsDefined(filter.DateBasis) && filter.From != default && filter.To != default
        && filter.From.Date <= filter.To.Date && (filter.To.Date - filter.From.Date).Days <= 1095
        && filter.To.Date < DateTime.MaxValue.Date
        && (!filter.AsOf.HasValue || (filter.AsOf.Value != default && filter.AsOf.Value.Date <= today.Date))
        && filter.CustomerId != Guid.Empty && filter.ProductId != Guid.Empty && filter.ManagerId != Guid.Empty;

    internal static async Task<List<DeliveryOtdDetailDto>> ReadAsync(IDispatchReadDbContext db,
        DeliveryOtdFilter filter, Guid company, DateTime asOf, CancellationToken ct)
    {
        var end = filter.To.Date.AddDays(1);
        var query = db.MerchandiseOrderDetails.AsNoTracking().Where(x => x.IsActive && x.ExpectedQuantity > 0
            && x.MerchandiseOrder.IsActive && x.MerchandiseOrder.CompanyId == company
            && x.MerchandiseOrder.CreateDate >= OrderStartDate
            && x.MerchandiseOrder.OrderType == OrderType.Merchandise
            && x.Status != nameof(MerchadiseStatus.Cancelled)
            && x.MerchandiseOrder.Status != nameof(MerchadiseStatus.Cancelled));
        if (filter.CustomerId.HasValue) query = query.Where(x => x.MerchandiseOrder.CustomerId == filter.CustomerId);
        if (filter.ProductId.HasValue) query = query.Where(x => x.ProductId == filter.ProductId);
        if (filter.ManagerId.HasValue) query = query.Where(x => x.MerchandiseOrder.ManagerById == filter.ManagerId);
        var activeLinks = DeliveryPlanSourceQuery.ActiveLinks(db.MfgOrderPOs.AsNoTracking(), company);
        var datedLines = query.Select(x => new
        {
            Detail = x,
            PromisedDate = activeLinks.Where(link => link.MerchandiseOrderDetailId == x.MerchandiseOrderDetailId)
                .Max(link => link.ProductionOrder.ExpectedDate)
        });
        // Missing MFG promise dates use requested date only to locate diagnostic rows in the period.
        datedLines = filter.DateBasis == DeliveryOtdDateBasis.Requested
            ? datedLines.Where(x => x.Detail.DeliveryRequestDate >= filter.From.Date && x.Detail.DeliveryRequestDate < end)
            : datedLines.Where(x => (x.PromisedDate ?? x.Detail.DeliveryRequestDate) >= filter.From.Date
                && (x.PromisedDate ?? x.Detail.DeliveryRequestDate) < end);
        var lines = await datedLines.Select(x => new DeliveryOtdDetailDto(
            x.Detail.MerchandiseOrderDetailId, x.Detail.MerchandiseOrderId, x.Detail.MerchandiseOrder.ExternalId,
            x.Detail.MerchandiseOrder.CustomerId, x.Detail.MerchandiseOrder.CustomerNameSnapshot,
            x.Detail.ProductId, x.Detail.ProductNameSnapshot, x.Detail.DeliveryRequestDate, x.PromisedDate,
            filter.DateBasis == DeliveryOtdDateBasis.Requested ? (DateTime?)x.Detail.DeliveryRequestDate : x.PromisedDate,
            x.Detail.ExpectedQuantity, 0, 0, 0, null, null, 0, x.Detail.MerchandiseOrder.IsDeliveryPaused,
            DeliveryOtdStatus.NotDue)).ToListAsync(ct);
        if (lines.Count == 0) return lines;
        // Use a subquery rather than a large client-generated IN list; never load unrelated company deliveries.
        var ids = datedLines.Select(x => x.Detail.MerchandiseOrderDetailId);
        var cutoff = asOf.Date.AddDays(1);
        var receipts = await db.DeliveryOrderDetails.AsNoTracking().Where(x => x.IsActive && !x.IsAttach
            && x.MerchandiseOrderDetailId.HasValue && ids.Contains(x.MerchandiseOrderDetailId.Value)
            && x.DeliveryOrder.IsActive && x.DeliveryOrder.CompanyId == company
            && x.DeliveryOrder.Status != nameof(DeliveryOrderStatus.Canceled)
            && x.DeliveryOrder.CreatedDate.HasValue && x.DeliveryOrder.CreatedDate < cutoff && x.Quantity > 0)
            .Select(x => new { Id = x.MerchandiseOrderDetailId!.Value, Date = x.DeliveryOrder.CreatedDate!.Value, x.Quantity })
            .ToListAsync(ct);
        var lookup = receipts.ToLookup(x => x.Id, x => new DeliveryOtdReceipt(x.Date, x.Quantity));
        return lines.Select(x => DeliveryOtdCalculator.Evaluate(x, lookup[x.DetailId], asOf)).ToList();
    }
    internal static async Task<DeliveryOtdPoSummary> ReadPoSummaryAsync(IDispatchReadDbContext db,
        IReadOnlyCollection<DeliveryOtdDetailDto> selectedLines, Guid company, DateTime asOf, CancellationToken ct)
    {
        var orderIds = selectedLines.Select(x => x.OrderId).Distinct().ToArray();
        if (orderIds.Length == 0) return DeliveryOtdPoCalculator.Summarize([], asOf);
        var links = DeliveryPlanSourceQuery.ActiveLinks(db.MfgOrderPOs.AsNoTracking(), company)
            .Where(x => x.Detail.ExpectedQuantity > 0 && x.Detail.Status != nameof(MerchadiseStatus.Cancelled));
        var cutoff = asOf.Date.AddDays(1);
        var deliveries = db.DeliveryOrderDetails.AsNoTracking().Where(x => x.IsActive && !x.IsAttach && x.Quantity > 0
            && x.MerchandiseOrderDetail != null && x.MerchandiseOrderDetail.IsActive
            && x.MerchandiseOrderDetail.ExpectedQuantity > 0
            && x.MerchandiseOrderDetail.Status != nameof(MerchadiseStatus.Cancelled)
            && x.MerchandiseOrderDetail.MerchandiseOrder.CompanyId == company
            && x.DeliveryOrder.IsActive && x.DeliveryOrder.CompanyId == company
            && x.DeliveryOrder.Status != nameof(DeliveryOrderStatus.Canceled)
            && x.DeliveryOrder.CreatedDate.HasValue && x.DeliveryOrder.CreatedDate < cutoff);
        var dates = await db.MerchandiseOrders.AsNoTracking()
            .Where(x => x.IsActive && x.CompanyId == company && x.OrderType == OrderType.Merchandise
                && x.CreateDate >= OrderStartDate
                && x.Status != nameof(MerchadiseStatus.Cancelled) && orderIds.Contains(x.MerchandiseOrderId))
            .Select(x => new DeliveryOtdPoDates(x.MerchandiseOrderId,
                links.Where(link => link.Detail.MerchandiseOrderId == x.MerchandiseOrderId)
                    .Max(link => link.ProductionOrder.ExpectedDate),
                deliveries.Where(d => d.MerchandiseOrderDetail!.MerchandiseOrderId == x.MerchandiseOrderId)
                    .Max(d => d.DeliveryOrder.CreatedDate),
                x.IsDeliveryPaused || x.DeliveryPausedFrom.HasValue || x.DeliveryPausedTo.HasValue
                    || x.DeliveryPausedBy.HasValue
                    || (x.DeliveryPauseReason != null && x.DeliveryPauseReason.Trim() != "")
                    || (x.DeliveryPauseType != null && x.DeliveryPauseType.Trim() != "")))
            .ToListAsync(ct);
        return DeliveryOtdPoCalculator.Summarize(dates, asOf);
    }

}
