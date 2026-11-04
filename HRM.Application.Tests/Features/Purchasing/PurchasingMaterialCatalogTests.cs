using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.Materials.Queries.GetPurchasingMaterials;
using HRM.Application.Features.Purchasing.Materials.Queries.GetPurchasingMaterialDetail;
using HRM.Application.Features.Purchasing.Materials.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Materials;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed class PurchasingMaterialCatalogTests
{
    [Fact]
    public async Task ListAndDetail_KeepCompanyAndActiveScope_WithSupplierPrices()
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        var permissions = new CurrentUserPermissionService(user);
        var companyId = user.CompanyId!.Value;
        var supplier = new Supplier { SupplierId = Guid.NewGuid(), CompanyId = companyId, IsActive = true, ExternalId = "NCC01", SupplierName = "Supplier" };
        var material = new Material
        {
            MaterialId = Guid.NewGuid(), CompanyId = companyId, IsActive = true, ExternalId = "NVL01", Name = "Resin",
            Category = new() { Name = "NVL" }, Barcode = "123", Comment = "Note", Unit = "kg",
            MaterialsSuppliers =
            [
                new() { MaterialsSuppliersId = Guid.NewGuid(), SupplierId = supplier.SupplierId, Supplier = supplier, IsActive = true, CurrentPrice = 125m, Currency = "VND", IsPreferred = true },
                new() { Supplier = new() { CompanyId = Guid.NewGuid(), IsActive = true }, IsActive = true, CurrentPrice = 999m },
                new() { Supplier = supplier, IsActive = false, CurrentPrice = 888m }
            ],
            MaterialGroupNames = [new() { IsActive = true, MaterialGroupNameText = "Resins" }, new() { IsActive = false, MaterialGroupNameText = "Hidden" }]
        };
        var foreign = new Material { MaterialId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true };
        var inactive = new Material { MaterialId = Guid.NewGuid(), CompanyId = companyId, IsActive = false };
        store.Seed(nameof(db.Materials), material, foreign, inactive);
        var listHandler = new GetPurchasingMaterialsQueryHandler(db, user, permissions, new MaterialReviewClock());
        var detailHandler = new GetPurchasingMaterialDetailQueryHandler(db, user, permissions);
        var list = await listHandler.Handle(new() { SupplierId = supplier.SupplierId, PageSize = 500 }, default);
        Assert.Equal(100, list.PageSize);
        Assert.Equal(1, list.TotalCount);
        var row = Assert.Single(list.Items);
        Assert.True(row.PurchaseAvailability.IsPurchaseAvailable);
        Assert.Equal(MaterialPurchaseStatus.Available, row.PurchaseAvailability.Status);
        Assert.Equal(1, row.SupplierCount);
        var detail = await detailHandler.Handle(new(material.MaterialId), default);
        Assert.NotNull(detail);
        Assert.Equal("123", detail.Barcode);
        Assert.Equal("Note", detail.Comment);
        Assert.Equal(125m, Assert.Single(detail.Suppliers).CurrentPrice);
        Assert.Equal("Resins", Assert.Single(detail.GroupNames));
        Assert.Null(await detailHandler.Handle(new(foreign.MaterialId), default));
        Assert.Null(await detailHandler.Handle(new(inactive.MaterialId), default));
        Assert.Null(await detailHandler.Handle(new(Guid.Empty), default));
        material.PurchaseAvailability = new() { Status = MaterialPurchaseStatus.Unavailable, Reason = "Stopped" };
        Assert.Empty((await listHandler.Handle(new() { PurchaseStatus = MaterialPurchaseStatus.Available }, default)).Items);
        var unavailable = Assert.Single((await listHandler.Handle(new() { PurchaseStatus = MaterialPurchaseStatus.Unavailable }, default)).Items);
        Assert.False(unavailable.PurchaseAvailability.IsPurchaseAvailable);
        Assert.Equal("Stopped", unavailable.PurchaseAvailability.Reason);
    }

    [Theory]
    [InlineData("SaleUser", true, true)]
    [InlineData("QCUser", true, true)]
    [InlineData("Purchaser", false, true)]
    [InlineData("Purchaser", true, false)]
    public async Task ForbiddenUsers_CannotReadListOrDetail(string role, bool authenticated, bool hasCompany)
    {
        var (db, _) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { Roles = [role], IsAuthenticated = authenticated, CompanyId = hasCompany ? Guid.NewGuid() : null };
        var permissions = new CurrentUserPermissionService(user);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetPurchasingMaterialsQueryHandler(db, user, permissions, new MaterialReviewClock()).Handle(new(), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new GetPurchasingMaterialDetailQueryHandler(db, user, permissions).Handle(new(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task PagingAndCategoryFilter_CountBeforePaging_AndUseStableOrder()
    {
        var (db, store) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { Roles = ["Purchaser"] };
        var category = Guid.NewGuid();
        var first = new Material { MaterialId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value, CategoryId = category, Category = new(), ExternalId = "A", IsActive = true };
        var second = new Material { MaterialId = Guid.NewGuid(), CompanyId = user.CompanyId.Value, CategoryId = category, Category = new(), ExternalId = "B", IsActive = true };
        store.Seed(nameof(db.Materials), second, first,
            new Material { CompanyId = user.CompanyId.Value, CategoryId = Guid.NewGuid(), IsActive = true });
        var handler = new GetPurchasingMaterialsQueryHandler(db, user, new CurrentUserPermissionService(user), new MaterialReviewClock());
        var page = await handler.Handle(new() { CategoryId = category, PageNumber = 2, PageSize = 1 }, default);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.True(page.HasPreviousPage);
        Assert.False(page.HasNextPage);
        Assert.Equal(second.MaterialId, Assert.Single(page.Items).MaterialId);
        Assert.Empty((await handler.Handle(new() { SupplierId = Guid.NewGuid() }, default)).Items);
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(new() { PurchaseStatus = (MaterialPurchaseStatus)999 }, default));
    }

    [Theory]
    [InlineData("PLPUUser")]
    [InlineData("Admin")]
    [InlineData("President")]
    public async Task ExistingPurchasingRoles_CanOpenCatalog(string role)
    {
        var (db, _) = IqcTestStore.CreateContext<IPurchaseOrderDbContext>();
        var user = new IqcWorkflowTests.User { Roles = [role] };
        var result = await new GetPurchasingMaterialsQueryHandler(db, user, new CurrentUserPermissionService(user), new MaterialReviewClock()).Handle(new(), default);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void ProjectionsAndLiteralSearch_TranslateToPostgres()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var companyId = Guid.NewGuid();
        var request = new GetPurchasingMaterialsQuery { Keyword = "NVL_%", SupplierId = Guid.NewGuid(), PurchaseStatus = MaterialPurchaseStatus.Available };
        var query = PurchasingMaterialReadModel.Filter(db.Materials, companyId, request);
        var listSql = PurchasingMaterialReadModel.Order(query, request).Take(15).Select(PurchasingMaterialReadModel.Summary(companyId)).ToQueryString();
        Assert.Contains("ILIKE", listSql);
        Assert.Contains("CompanyId", listSql);
        Assert.Contains(@"\_\%", listSql);
        Assert.DoesNotContain("CurrentPrice", listSql);
        var detailSql = db.Materials.Where(x => x.CompanyId == companyId && x.IsActive == true && x.MaterialId == Guid.NewGuid())
            .Select(PurchasingMaterialReadModel.Detail(companyId, true)).ToQueryString();
        Assert.Contains("CurrentPrice", detailSql);
        Assert.Contains("Supplier", detailSql);
        var maskedSql = db.Materials.Where(x => x.CompanyId == companyId && x.IsActive == true)
            .Select(PurchasingMaterialReadModel.Detail(companyId, false)).ToQueryString();
        Assert.Contains("NULL AS \"CurrentPrice\"", maskedSql);
        // Outer SELECT đọc alias NULL; không có tham chiếu thứ hai tới cột giá của bảng gốc.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(maskedSql, "\\.\"CurrentPrice\"").Cast<System.Text.RegularExpressions.Match>());
    }
}
