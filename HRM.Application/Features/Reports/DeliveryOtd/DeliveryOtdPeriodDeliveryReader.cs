using HRM.Application.Abstractions.Persistence.Dispatch;
using HRM.Application.Commons.Deliveries;
using HRM.Domain.Enums.Merchadises;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.Reports.DeliveryOtd;

internal static class DeliveryOtdPeriodDeliveryReader
{
    internal static async Task<decimal> ReadAsync(IDispatchReadDbContext db, DeliveryOtdFilter filter,
        Guid company, DateTime asOf, CancellationToken ct, bool restricted = false)
    {
        var start = filter.From.Date;
        var last = filter.To.Date < asOf.Date ? filter.To.Date : asOf.Date;
        if (start > last) return 0m;
        var end = last.AddDays(1);
        // Delivery-period KPI includes orders whose requested/promised dates fall outside the period.
        var query = db.DeliveryOrderDetails.AsNoTracking().EffectiveDeliveryLines(company).Where(x => x.Quantity > 0
            && x.DeliveryOrder.CreatedDate >= start && x.DeliveryOrder.CreatedDate < end
            && x.MerchandiseOrderDetail != null && x.MerchandiseOrderDetail.IsActive
            && x.MerchandiseOrderDetail.ExpectedQuantity > 0
            && x.MerchandiseOrderDetail.Status != nameof(MerchadiseStatus.Cancelled)
            && x.MerchandiseOrderDetail.MerchandiseOrder.IsActive
            && x.MerchandiseOrderDetail.MerchandiseOrder.CompanyId == company
            && x.MerchandiseOrderDetail.MerchandiseOrder.OrderType == OrderType.Merchandise
            && x.MerchandiseOrderDetail.MerchandiseOrder.CreateDate >= DeliveryOtdReader.OrderStartDate
            && x.MerchandiseOrderDetail.MerchandiseOrder.Status != nameof(MerchadiseStatus.Cancelled));
        if (restricted)
        {
            var visible = DeliveryOtdAccess.VisibleOrderIds(db.MerchandiseOrders.AsNoTracking(), company);
            query = query.Where(line => visible.Contains(line.MerchandiseOrderDetail!.MerchandiseOrderId) &&
                line.DeliveryOrder.CustomerId == line.MerchandiseOrderDetail.MerchandiseOrder.CustomerId);
        }
        if (filter.CustomerId.HasValue)
            query = query.Where(x => x.MerchandiseOrderDetail!.MerchandiseOrder.CustomerId == filter.CustomerId);
        if (filter.ProductId.HasValue)
            query = query.Where(x => x.MerchandiseOrderDetail!.ProductId == filter.ProductId);
        if (filter.ManagerId.HasValue)
            query = query.Where(x => x.MerchandiseOrderDetail!.MerchandiseOrder.ManagerById == filter.ManagerId);
        return await query.SumAsync(x => (decimal?)x.Quantity, ct) ?? 0m;
    }
}
