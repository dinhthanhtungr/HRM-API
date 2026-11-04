using HRM.Application.Features.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;
using HRM.Application.Features.Purchasing.PurchaseOrders.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Domain.Entities.OrderSchema;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace HRM.Application.Tests.Features.Purchasing;

public sealed class PurchaseOrderSqlTests
{
    [Fact]
    public void DetailProjection_TranslatesToPostgres_WithStoredQcQuantityAndSnapshot()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = new GetPurchaseOrderByIdQueryHandler(db, null!, new PurchaseOrderReceiptReader(db)).BuildQuery(Guid.NewGuid(), Guid.NewGuid()).ToQueryString();
        Assert.Contains("RealQuantity", sql);
        Assert.Contains("PurchaseOrderSnapshot", sql);
        Assert.Contains("CompanyId", sql);
        Assert.DoesNotContain("WarehouseShelfLedger", sql);
    }
    [Fact]
    public void DocumentsModel_UsesReviewedTableAndExistingParentKeys()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var entity = db.Model.FindEntityType(typeof(PurchaseOrderDocument))!;
        Assert.Equal("Orders", entity.GetSchema());
        Assert.Equal("PurchaseOrderDocuments", entity.GetTableName());
        Assert.DoesNotContain(entity.GetProperties(), x => x.IsShadowProperty());
        Assert.Equal(5, entity.GetForeignKeys().Count());
    }
}
