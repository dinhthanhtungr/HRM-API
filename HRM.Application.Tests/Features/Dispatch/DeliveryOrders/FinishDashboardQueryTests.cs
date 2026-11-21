using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.ExportDeliveryReport;
using HRM.Application.Features.Dispatch.DeliveryOrders.Queries.GetFinishDashboard;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class FinishDashboardQueryTests
{
    [Fact]
    public void RevenueUsesSharedCurrencyRulesAndBothCustomerScopes()
    {
        var company = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var day = new DateTime(2026, 11, 5);
        var detail = new DeliveryOrderDetail
        {
            IsActive = true, Quantity = 2, MerchandiseOrderDetailId = Guid.NewGuid(),
            DeliveryOrder = new() { IsActive = true, CompanyId = company, CustomerId = customer,
                CreatedDate = day, CustomerExternalIdSnapShot = "KH_1", Status = "Pending" },
            MerchandiseOrderDetail = new() { UnitPriceAgreed = 3,
                MerchandiseOrder = new() { CompanyId = company, CustomerId = customer, Currency = "USD", ExchangeRate = 25000 } }
        };
        var customers = new[] { customer }.AsQueryable();
        IQueryable<HRM.Application.Commons.Reporting.DeliveryRevenueLine> Query() =>
            GetFinishDashboardQueryHandler.Revenue(new[] { detail }.AsQueryable(), customers, company, day, day.AddDays(1));
        Assert.Equal(150000m, Query().Sum(x => x.RevenueAmountVnd));
        detail.DeliveryOrder.Status = "Canceled";
        Assert.Empty(Query());
        detail.DeliveryOrder.Status = "Pending";
        detail.DeliveryOrder.CustomerId = Guid.NewGuid();
        Assert.Empty(Query());
        detail.DeliveryOrder.CustomerId = customer;
        detail.MerchandiseOrderDetail.MerchandiseOrder.CustomerId = Guid.NewGuid();
        Assert.Empty(Query());
        detail.MerchandiseOrderDetail.MerchandiseOrder.CustomerId = customer;
        detail.MerchandiseOrderDetail.MerchandiseOrder.CompanyId = Guid.NewGuid();
        Assert.Empty(Query());
    }

    [Fact]
    public void RevenueScopeTranslatesToSqlWithoutCosts()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = GetFinishDashboardQueryHandler.Revenue(db.DeliveryOrderDetails,
            db.DeliveryOrders.Select(x => x.CustomerId), Guid.NewGuid(), new(2026, 11, 5), new(2026, 11, 6))
            .GroupBy(_ => 1).Select(g => g.Sum(x => x.RevenueAmountVnd)).ToQueryString();
        Assert.Contains("SUM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UnitPriceAgreed", sql);
        Assert.DoesNotContain("BaseCostSnapshot", sql);
        Assert.DoesNotContain("TotalCostSnapshot", sql);
    }

    [Fact]
    public void SameExcelSourceCountsEachDetailOnceNotEachLotOrHeader()
    {
        var company = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var date = new DateTime(2026, 11, 5);
        var order = new DeliveryOrder { Id = Guid.NewGuid(), CompanyId = company, CustomerId = customer, CreatedDate = date,
            Status = "Pending", IsActive = true, Details = [
                new() { Id = Guid.NewGuid(), Quantity = 12, NumOfBags = 2, LotConsumptions = [new() { LotNo = "A" }, new() { LotNo = "B" }] },
                new() { Id = Guid.NewGuid(), Quantity = 3, NumOfBags = 1, IsAttach = true },
                new() { Quantity = 100, IsActive = false }
            ] };
        var rows = DeliveryFinishReportQuery.Project(new[] { order }.AsQueryable(), company, date, date);
        var result = GetFinishDashboardQueryHandler.Summary(rows).Single();
        Assert.Equal(15, result.QuantityKg);
        Assert.Equal(3, result.Bags);
        Assert.Equal(2, result.LineCount);
        Assert.Equal(1, result.DeliveryCount);
        Assert.Equal(1, result.CustomerCount);
        Assert.Equal(0, result.OrderCount);
        order.Status = "Canceled";
        Assert.Equal(15, GetFinishDashboardQueryHandler.Summary(rows).Single().QuantityKg);
    }

    [Fact]
    public void DateBoundsAndStablePagingMatchExcel()
    {
        var company = Guid.NewGuid();
        var day = new DateTime(2026, 11, 5);
        var orders = new[] { day.AddTicks(-1), day, day.AddHours(23), day.AddDays(1) }.Select(date => new DeliveryOrder
        { Id = Guid.NewGuid(), CompanyId = company, CreatedDate = date, Details = [new() { Id = Guid.NewGuid(), Quantity = 1 }] }).AsQueryable();
        var rows = DeliveryFinishReportQuery.Project(orders, company, day, day).OrderBy(x => x.CreatedDate).ThenBy(x => x.DeliveryOrderId).ThenBy(x => x.DeliveryLineId);
        Assert.Equal(2, rows.Count());
        Assert.NotEqual(rows.Take(1).Single().DeliveryLineId, rows.Skip(1).Take(1).Single().DeliveryLineId);
        Assert.Empty(DeliveryFinishReportQuery.Project(orders, Guid.NewGuid(), day, day));
    }

    [Fact]
    public void AggregationsAndPageTranslateToSqlWithoutCost()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var query = DeliveryFinishReportQuery.Project(db.DeliveryOrders.AsNoTracking(), Guid.NewGuid(), new(2026, 11, 1), new(2026, 11, 7));
        var sql = GetFinishDashboardQueryHandler.Summary(query).ToQueryString();
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("DISTINCT", sql);
        Assert.DoesNotContain("UnitCostSnapshot", sql);
        Assert.DoesNotContain("TotalCostSnapshot", sql);
        var pageSql = query.OrderBy(x => x.CreatedDate).ThenBy(x => x.DeliveryOrderId).ThenBy(x => x.DeliveryLineId).Skip(15).Take(15).ToQueryString();
        Assert.Contains("LIMIT", pageSql);
        Assert.Contains("OFFSET", pageSql);
        var topSql = query.GroupBy(x => x.CustomerId).OrderByDescending(g => g.Sum(x => x.Row.QuantityKg)).ThenBy(g => g.Key).Take(10)
            .Select(g => new FinishDashboardRanking(g.Key, g.Max(x => x.Row.CustomerName), g.Sum(x => x.Row.QuantityKg), g.Sum(x => (long)x.Row.NumOfBags))).ToQueryString();
        Assert.Contains("GROUP BY", topSql);
        Assert.Contains("LIMIT", topSql);
        var dailySql = query.GroupBy(x => x.CreatedDate.Date).OrderBy(g => g.Key)
            .Select(g => new FinishDashboardDay(g.Key, g.Sum(x => x.Row.QuantityKg), g.Sum(x => (long)x.Row.NumOfBags), g.Select(x => x.DeliveryOrderId).Distinct().Count())).ToQueryString();
        Assert.Contains("GROUP BY", dailySql);
        var productSql = query.GroupBy(x => x.ProductId).OrderByDescending(g => g.Sum(x => x.Row.QuantityKg)).ThenBy(g => g.Key).Take(10)
            .Select(g => new FinishDashboardRanking(g.Key, g.Key.HasValue ? g.Max(x => x.Row.ProductDisplay) : null, g.Sum(x => x.Row.QuantityKg), g.Sum(x => (long)x.Row.NumOfBags))).ToQueryString();
        Assert.Contains("GROUP BY", productSql);
    }

    [Fact]
    public void RejectsInvalidRangeAndPageOverflow()
    {
        Assert.False(GetFinishDashboardQueryHandler.Valid(new()));
        Assert.False(GetFinishDashboardQueryHandler.Valid(new() { From = new(2026, 11, 7), To = new(2026, 11, 1) }));
        Assert.False(GetFinishDashboardQueryHandler.Valid(new() { From = new(2025, 1, 1), To = new(2026, 11, 1) }));
        Assert.False(GetFinishDashboardQueryHandler.Valid(new() { From = new(2026, 11, 1), To = new(2026, 11, 7), PageNumber = int.MaxValue }));
        Assert.True(GetFinishDashboardQueryHandler.Valid(new() { From = new(2026, 11, 1), To = new(2026, 11, 7) }));
    }
}
