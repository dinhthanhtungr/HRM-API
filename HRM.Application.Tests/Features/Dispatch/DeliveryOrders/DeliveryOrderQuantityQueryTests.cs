using HRM.Application.Features.Dispatch.DeliveryOrders.Queries;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Dispatch.DeliveryOrders;

public sealed class DeliveryOrderQuantityQueryTests
{
    [Theory]
    [InlineData("Pending", true)]
    [InlineData("InProgress", true)]
    [InlineData("Completed", true)]
    [InlineData("Delivered", true)]
    [InlineData("Canceled", false)]
    [InlineData("Cancelled", false)]
    [InlineData(" cancelled ", false)]
    [InlineData("CANCELED", false)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void Allocation_ReleasesOnlyCanceledOrders(string? status, bool counted)
    {
        var companyId = Guid.NewGuid();
        var row = Row(companyId, status);
        var result = new[] { row }.AsQueryable().CountedForAllocation(companyId).ToList();
        Assert.Equal(counted ? 1 : 0, result.Count);
    }

    [Fact]
    public void Allocation_ExcludesOtherCompanyInactiveAttachmentsAndUnlinkedRows()
    {
        var companyId = Guid.NewGuid();
        var valid = Row(companyId);
        var inactiveDetail = Row(companyId);
        inactiveDetail.IsActive = false;
        var inactiveOrder = Row(companyId);
        inactiveOrder.DeliveryOrder.IsActive = false;
        var attachment = Row(companyId);
        attachment.IsAttach = true;
        var unlinked = Row(companyId);
        unlinked.MerchandiseOrderDetailId = null;
        var rows = new[] { valid, Row(Guid.NewGuid()), inactiveDetail, inactiveOrder, attachment, unlinked };

        Assert.Same(valid, Assert.Single(rows.AsQueryable().CountedForAllocation(companyId)));
        Assert.Empty(rows.AsQueryable().CountedForAllocation(Guid.Empty));
    }

    [Fact]
    public void Update_ExcludesCurrentOrderWhileKeepingOtherAllocatedQuantity()
    {
        var companyId = Guid.NewGuid();
        var current = Row(companyId);
        var other = Row(companyId);
        other.Quantity = 7m;
        var canceled = Row(companyId, "Canceled");
        var rows = new[] { current, other, canceled }.AsQueryable();

        Assert.Equal(7m, rows.CountedForAllocation(companyId, current.DeliveryOrderId).Sum(x => x.Quantity));
    }

    [Fact]
    public void Cancel_RestoresQuantityForAnotherDeliveryOrder()
    {
        var companyId = Guid.NewGuid();
        var row = Row(companyId);
        var rows = new[] { row }.AsQueryable();
        Assert.Equal(10m, rows.CountedForAllocation(companyId).Sum(x => x.Quantity));

        row.DeliveryOrder.Status = "Canceled";

        Assert.Equal(0m, rows.CountedForAllocation(companyId).Sum(x => x.Quantity));
    }

    [Fact]
    public void AllocationQuery_TranslatesToPostgresWithoutDatabaseConnection()
    {
        using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = db.DeliveryOrderDetails
            .CountedForAllocation(Guid.NewGuid(), Guid.NewGuid())
            .GroupBy(x => x.MerchandiseOrderDetailId!.Value)
            .Select(g => new { Id = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToQueryString();

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("CompanyId", sql);
        Assert.Contains("cancelled", sql);
    }

    private static DeliveryOrderDetail Row(Guid companyId, string? status = "Pending")
    {
        var id = Guid.NewGuid();
        return new DeliveryOrderDetail
        {
            Id = Guid.NewGuid(),
            DeliveryOrderId = id,
            MerchandiseOrderDetailId = Guid.NewGuid(),
            Quantity = 10m,
            DeliveryOrder = new DeliveryOrder { Id = id, CompanyId = companyId, Status = status! }
        };
    }
}
