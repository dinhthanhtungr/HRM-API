using HRM.Application.Abstractions.Persistence.PLM.DeliverySchedules;
using HRM.Application.Features.PLM.SaleOrders.DeliverySchedules;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.DeliverySchema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryPlan;

internal sealed class DeliverySchedulePlanService(IDeliveryScheduleDbContext db, IOptions<DeliveryScheduleOptions> options)
{
    public bool Enabled => options.Value.CanShowMultiScheduleUi;
    internal IQueryable<DeliveryPlanSource> Filter(IQueryable<DeliveryPlanSource> query, Guid company, DateOnly date, bool overdue, bool all)
    {
        if (!Enabled || all) return GetDeliveryPlanQueryHandler.ForDate(query, date, overdue, all);
        return FilterWithSchedules(query, Schedules(company), Allocations(company), db.MerchandiseOrderDetails.AsNoTracking()
            .Where(x => x.IsActive && x.MerchandiseOrder.CompanyId == company), date, overdue);
    }
    internal static IQueryable<DeliveryPlanSource> FilterWithSchedules(IQueryable<DeliveryPlanSource> query,
        IQueryable<MerchandiseOrderDeliverySchedule> schedules, IQueryable<DeliveryOrderScheduleAllocation> allocations,
        IQueryable<MerchandiseOrderDetail> details, DateOnly date, bool overdue)
    {
        var start = date.ToDateTime(TimeOnly.MinValue); var end = start.AddDays(1);
        return query.Where(order => schedules.Any(s => s.MerchandiseOrderDetail.MerchandiseOrderId == order.Id &&
                (((s.PromisedDate ?? s.RequestedDate) >= start && (s.PromisedDate ?? s.RequestedDate) < end)
                || overdue && (s.PromisedDate ?? s.RequestedDate) < start && s.Quantity >
                    (allocations.Where(a => a.MerchandiseOrderDeliveryScheduleId == s.MerchandiseOrderDeliveryScheduleId).Sum(a => (decimal?)a.Quantity) ?? 0)))
            || (!schedules.Any(s => s.MerchandiseOrderDetail.MerchandiseOrderId == order.Id)
                || details.Any(d => d.MerchandiseOrderId == order.Id && !schedules.Any(s => s.MerchandiseOrderDetailId == d.MerchandiseOrderDetailId)))
                && order.PromisedDate.HasValue && (order.PromisedDate >= start && order.PromisedDate < end
                    || overdue && order.PromisedDate < start && order.RemainingQuantity > 0));
    }
    public async Task<Dictionary<Guid, DateOnly?>> NextPromisesAsync(Guid[] ids, Guid company, CancellationToken ct)
    {
        if (!Enabled || ids.Length == 0) return [];
        var allocations = Allocations(company);
        var schedules = Schedules(company);
        var mixedOrders = await db.MerchandiseOrderDetails.AsNoTracking().Where(d => d.IsActive
            && d.MerchandiseOrder.CompanyId == company && ids.Contains(d.MerchandiseOrderId)
            && !schedules.Any(s => s.MerchandiseOrderDetailId == d.MerchandiseOrderDetailId))
            .Select(d => d.MerchandiseOrderId).Distinct().ToArrayAsync(ct);
        var rows = await Schedules(company).Where(s => ids.Contains(s.MerchandiseOrderDetail.MerchandiseOrderId))
            .Select(s => new { OrderId = s.MerchandiseOrderDetail.MerchandiseOrderId, s.PromisedDate,
                Remaining = s.Quantity - (allocations.Where(a => a.MerchandiseOrderDeliveryScheduleId == s.MerchandiseOrderDeliveryScheduleId).Sum(a => (decimal?)a.Quantity) ?? 0) }).ToListAsync(ct);
        return rows.Where(x => !mixedOrders.Contains(x.OrderId)).GroupBy(x => x.OrderId).ToDictionary(g => g.Key, g => g.Where(x => x.Remaining > 0 && x.PromisedDate.HasValue)
            .OrderBy(x => x.PromisedDate).Select(x => (DateOnly?)DateOnly.FromDateTime(x.PromisedDate!.Value)).FirstOrDefault());
    }
    private IQueryable<MerchandiseOrderDeliverySchedule> Schedules(Guid company) => db.MerchandiseOrderDeliverySchedules.AsNoTracking()
        .Where(s => s.IsActive && s.MerchandiseOrderDetail.IsActive && s.MerchandiseOrderDetail.MerchandiseOrder.IsActive
            && s.MerchandiseOrderDetail.MerchandiseOrder.CompanyId == company);
    private IQueryable<DeliveryOrderScheduleAllocation> Allocations(Guid company)
    {
        var counted = db.DeliveryOrderDetails.AsNoTracking().CountedForAllocation(company);
        return db.DeliveryOrderScheduleAllocations.AsNoTracking().Where(a => a.IsActive && a.Schedule != null && a.MerchandiseOrderDeliveryScheduleId.HasValue
            && counted.Any(d => d.Id == a.DeliveryOrderDetailId && d.MerchandiseOrderDetailId == a.Schedule.MerchandiseOrderDetailId));
    }
}
