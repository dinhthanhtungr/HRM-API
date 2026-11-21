using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetDeliveryPlan;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SaleOrders;

public sealed class DeliverySchedulePlanTests
{
    [Fact]
    public void OrderAppearsOnEachScheduledDayInsteadOfOnlyMfgDate()
    {
        var id = Guid.NewGuid(); var day = new DateOnly(2026, 11, 20);
        var detail = new MerchandiseOrderDetail { MerchandiseOrderDetailId = Guid.NewGuid(), MerchandiseOrderId = id };
        MerchandiseOrderDeliverySchedule[] schedules = [new() { MerchandiseOrderDetail = detail, Quantity = 400, RequestedDate = day.ToDateTime(TimeOnly.MinValue) },
            new() { MerchandiseOrderDetail = detail, Quantity = 600, RequestedDate = day.AddDays(5).ToDateTime(TimeOnly.MinValue) }];
        DeliveryPlanSource[] orders = [new() { Id = id, PromisedDate = day.ToDateTime(TimeOnly.MinValue), RemainingQuantity = 1000 }];
        Assert.Single(DeliverySchedulePlanService.FilterWithSchedules(orders.AsQueryable(), schedules.AsQueryable(), Array.Empty<DeliveryOrderScheduleAllocation>().AsQueryable(), new[] { detail }.AsQueryable(), day.AddDays(5), false));
        Assert.Empty(DeliverySchedulePlanService.FilterWithSchedules(orders.AsQueryable(), schedules.AsQueryable(), Array.Empty<DeliveryOrderScheduleAllocation>().AsQueryable(), new[] { detail }.AsQueryable(), day.AddDays(2), false));
        var legacy = new MerchandiseOrderDetail { MerchandiseOrderId = id, MerchandiseOrderDetailId = Guid.NewGuid() };
        Assert.Single(DeliverySchedulePlanService.FilterWithSchedules(orders.AsQueryable(), schedules.AsQueryable(), Array.Empty<DeliveryOrderScheduleAllocation>().AsQueryable(), new[] { detail, legacy }.AsQueryable(), day, false));
    }
    [Fact]
    public void ScheduleFilterTranslatesToPostgresWithoutConnectingToDatabase()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var orders = DeliveryPlanSourceQuery.Project(db.MerchandiseOrders, db.MfgOrderPOs, db.DeliveryOrderDetails, company, db.Employees);
        var schedules = db.MerchandiseOrderDeliverySchedules.Where(s => s.MerchandiseOrderDetail.MerchandiseOrder.CompanyId == company);
        var sql = DeliverySchedulePlanService.FilterWithSchedules(orders, schedules, db.DeliveryOrderScheduleAllocations, db.MerchandiseOrderDetails, new DateOnly(2026, 11, 20), true).ToQueryString();
        Assert.Contains("MerchandiseOrderDeliverySchedules", sql);
        Assert.Contains("DeliveryOrderScheduleAllocations", sql);
    }
}
