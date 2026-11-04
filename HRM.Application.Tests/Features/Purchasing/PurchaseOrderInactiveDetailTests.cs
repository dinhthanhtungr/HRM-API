using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.OrderSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class PurchaseOrderInactiveDetailTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InactiveDetailIsOptInAndIncludesHistoricalLinesAndLinks(bool includeInactive)
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var company = Guid.NewGuid();
        var po = new PurchaseOrder
        {
            PurchaseOrderId = Guid.NewGuid(), CompanyId = company, IsActive = false, Status = "Canceled",
            PurchaseOrderDetails = [new() { LineNo = 1, IsActive = false, RequestQuantity = 2m,
                UnitPriceAgreed = 100m, TotalPriceAgreed = 200m, MaterialNameSnapshot = "Historical material" }],
            PurchaseOrderLinks = [new() { IsActive = false,
                MerchandiseOrder = new() { CompanyId = company, ExternalId = "DHG-OLD" } },
                new() { IsActive = false, MerchandiseOrder = new() { CompanyId = Guid.NewGuid(), ExternalId = "FOREIGN" } }]
        };
        store.Seed(nameof(db.PurchaseOrders), po);
        var handler = new GetPurchaseOrderByIdQueryHandler(db, null!, null!);
        var result = handler.BuildQuery(po.PurchaseOrderId, company, includeInactive).SingleOrDefault();
        if (!includeInactive) { Assert.Null(result); return; }
        Assert.NotNull(result);
        Assert.False(result.IsActive);
        Assert.Equal("Canceled", result.Status);
        Assert.Equal("Historical material", Assert.Single(result.Items).MaterialName);
        Assert.Same(result.Items, result.PurchaseOrderDetails);
        Assert.Equal(200m, result.TotalPrice);
        Assert.Equal("DHG-OLD", result.MerchandiseOrderCodes);
        Assert.Equal("DHG-OLD", Assert.Single(result.MerchandiseOrders).ExternalId);
        Assert.Empty(handler.BuildQuery(po.PurchaseOrderId, Guid.NewGuid(), true));
    }

    [Fact]
    public void DefaultStillReadsActiveCanceledOrdersAndExcludesInactiveLines()
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var company = Guid.NewGuid();
        var po = new PurchaseOrder
        {
            PurchaseOrderId = Guid.NewGuid(), CompanyId = company, IsActive = true, Status = "Canceled",
            PurchaseOrderDetails = [new() { IsActive = true, TotalPriceAgreed = 10m },
                new() { IsActive = false, TotalPriceAgreed = 99m }]
        };
        store.Seed(nameof(db.PurchaseOrders), po);
        var result = new GetPurchaseOrderByIdQueryHandler(db, null!, null!).BuildQuery(po.PurchaseOrderId, company).Single();
        Assert.Single(result.Items);
        Assert.Equal(10m, result.TotalPrice);
        Assert.Empty(result.MerchandiseOrders);
        Assert.False(new GetPurchaseOrderByIdQuery(po.PurchaseOrderId).IncludeInactive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HistoricalDetailQueryTranslatesToPostgresql(bool includeInactive)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var sql = new GetPurchaseOrderByIdQueryHandler(db, null!, null!).BuildQuery(Guid.NewGuid(), company, includeInactive).ToQueryString();
        Assert.Contains(company.ToString(), sql);
        Assert.Contains("PurchaseOrderDetails", sql);
    }
}
