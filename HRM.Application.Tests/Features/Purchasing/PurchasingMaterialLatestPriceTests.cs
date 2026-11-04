using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.Materials.Queries.GetPurchasingMaterials;
using HRM.Application.Features.Purchasing.Materials.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class PurchasingMaterialLatestPriceTests
{
    [Fact]
    public async Task ListPicksPriceAcrossSuppliers_WithCorrectCurrencyDateAndPage()
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        var company = user.CompanyId!.Value;
        var at = new DateTime(2026, 10, 21);
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            ExternalId = "NVL_A", CreatedDate = at, Category = new() { CompanyId = company } };
        var empty = new Material { MaterialId = Guid.NewGuid(), CompanyId = company, IsActive = true,
            ExternalId = "NVL_B", CreatedDate = at.AddDays(-1), Category = new() { CompanyId = company } };
        MaterialsSupplier Link(decimal price, DateTime date, Guid scope, string currency = "VND") => new()
        {
            MaterialsSuppliersId = Guid.NewGuid(), IsActive = true, CurrentPrice = price,
            UpdatedDate = date, Currency = currency, Supplier = new() { CompanyId = scope, IsActive = true }
        };
        material.MaterialsSuppliers = [Link(100, at.AddDays(-2), company), Link(200, at, company, "USD"),
            Link(99999, at.AddDays(10), Guid.NewGuid())];
        PurchaseOrderDetail Po(decimal? price, DateTime date, Guid scope, string? status = null) => new()
        {
            PurchaseOrderDetailId = Guid.NewGuid(), MaterialId = material.MaterialId, Material = material,
            UnitPriceAgreed = price, IsActive = true,
            PurchaseOrder = new() { CompanyId = scope, IsActive = true, CreateDate = date, Status = status,
                Supplier = new() { CompanyId = scope } }
        };
        var po = Po(150, at, company);
        material.PurchaseOrderDetails = [po, Po(88888, at.AddDays(10), company, "Canceled"), Po(77777, at.AddDays(11), Guid.NewGuid())];
        store.Seed(nameof(db.Materials), material, empty);
        var handler = new GetPurchasingMaterialsQueryHandler(db, user, new CurrentUserPermissionService(user), new MaterialReviewClock());
        var first = await handler.Handle(new() { SortBy = "createdDate", SortDirection = "desc", PageSize = 1 }, default);
        Assert.Equal(2, first.TotalCount);
        Assert.True(first.HasNextPage);
        var latest = Assert.Single(first.Items).LatestPrice!;
        Assert.Equal("PurchaseOrder", latest.Source);
        Assert.Equal(150m, latest.Amount);
        Assert.Equal("VND", latest.Currency);
        Assert.Equal(at, latest.PriceDate);
        po.PurchaseOrder.CreateDate = at.AddDays(-1);
        latest = Assert.Single((await handler.Handle(new() { PageSize = 1, SortBy = "createdDate", SortDirection = "desc" }, default)).Items).LatestPrice!;
        Assert.Equal("MaterialSupplier", latest.Source);
        Assert.Equal(200m, latest.Amount);
        Assert.Equal("USD", latest.Currency);
        var second = await handler.Handle(new() { PageNumber = 2, PageSize = 1, SortBy = "createdDate", SortDirection = "desc" }, default);
        latest = Assert.Single(second.Items).LatestPrice!;
        Assert.Equal("Unknown", latest.Source);
        Assert.Null(latest.Amount);
        Assert.Null(latest.Currency);
        Assert.Null(latest.PriceDate);
    }

    [Fact]
    public async Task CatalogOnlyCallerReceivesNoPrice()
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { HasExplicitPermissionSet = true, Permissions = [ApplicationPermissions.Purchasing.ManageCatalog] };
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, IsActive = true,
            Category = new() { CompanyId = user.CompanyId.Value },
            MaterialsSuppliers = [new() { IsActive = true, CurrentPrice = 999, Supplier = new() { CompanyId = user.CompanyId, IsActive = true } }] };
        store.Seed(nameof(db.Materials), material);
        var result = await new GetPurchasingMaterialsQueryHandler(db, user, new CurrentUserPermissionService(user), new MaterialReviewClock()).Handle(new(), default);
        Assert.Null(Assert.Single(result.Items).LatestPrice);
    }

    [Fact]
    public void BothBatchQueriesTranslateToTopOnePerMaterial()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var company = Guid.NewGuid();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        foreach (var query in new[] { PurchasingMaterialLatestPriceQuery.PurchaseOrders(db, company, ids),
                     PurchasingMaterialLatestPriceQuery.Suppliers(db, company, ids) })
        {
            var sql = query.ToQueryString();
            Assert.Contains("LATERAL", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("LIMIT 1", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CompanyId", sql);
        }
    }
}
