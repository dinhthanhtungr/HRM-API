using System.Reflection;
using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Purchasing.Catalog.Dtos;
using HRM.Application.Features.Purchasing.Catalog.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRM.Application.Tests.Features.Purchasing;

public sealed partial class PurchasingCatalogManagementTests
{
    [Fact]
    public async Task CreateSupplier_UsesServerCompanyAuditAndCode_AndCanReadBack()
    {
        var f = new Fixture();
        var result = await f.Service.SaveSupplierAsync(null, new() { SupplierName = " Supplier ", Note = "Note", Priority = SupplierPriority.High }, default);
        Assert.True(result.Success, result.Message);
        var supplier = Assert.Single(f.Store.Added.OfType<Supplier>());
        Assert.Equal(f.User.CompanyId, supplier.CompanyId);
        Assert.Equal(f.User.EmployeeId, supplier.CreatedBy);
        Assert.Equal("NCC_1", supplier.ExternalId);
        Assert.Equal(0, result.Data!.UpdatedDate.Ticks % 10);
        Assert.Equal("Supplier", supplier.SupplierName);
        Assert.Equal(1, f.Store.Locks);
        var detail = await f.Service.GetSupplierAsync(supplier.SupplierId, false, default);
        Assert.Equal(SupplierPriority.High, detail!.Priority);
        Assert.Equal("Note", detail.Note);
        Assert.Single((await f.Service.GetSuppliersAsync(new(), default)).Items);
    }

    [Fact]
    public async Task SupplierPatch_RequiresVersion_KeepsNull_AndClearsOnlyWhitelist()
    {
        var f = new Fixture();
        var supplier = f.Supplier();
        supplier.UpdatedDate = new(2026, 10, 18);
        supplier.Phone = "123"; supplier.Note = "Old";
        Assert.False((await f.Service.SaveSupplierAsync(supplier.SupplierId, new() { Note = "New" }, default)).Success);
        Assert.Equal("Old", supplier.Note);
        var result = await f.Service.SaveSupplierAsync(supplier.SupplierId, new() { Note = "New", ExpectedUpdatedDate = supplier.UpdatedDate }, default);
        Assert.True(result.Success);
        Assert.Equal("123", supplier.Phone);
        Assert.False((await f.Service.SaveSupplierAsync(supplier.SupplierId, new() { ExternalId = "NCC_NEW", ExpectedUpdatedDate = supplier.UpdatedDate }, default)).Success);
        Assert.False((await f.Service.SaveSupplierAsync(supplier.SupplierId, new() { Phone = "456", ClearFields = ["phone"], ExpectedUpdatedDate = supplier.UpdatedDate }, default)).Success);
        Assert.False((await f.Service.SaveSupplierAsync(supplier.SupplierId, new() { ClearFields = ["companyId"], ExpectedUpdatedDate = supplier.UpdatedDate }, default)).Success);
        result = await f.Service.SaveSupplierAsync(supplier.SupplierId, new() { ClearFields = ["phone"], ExpectedUpdatedDate = supplier.UpdatedDate }, default);
        Assert.True(result.Success);
        Assert.Null(supplier.Phone);
        Assert.Equal("New", supplier.Note);
    }

    [Fact]
    public async Task DeleteRestore_KeepPurchaseHistoryAndChildren_AndAdvanceVersion()
    {
        var f = new Fixture();
        var supplier = f.Supplier();
        supplier.SupplierContacts = [new() { ContactId = Guid.NewGuid(), IsActive = true, IsPrimary = true }];
        supplier.PurchaseOrders = [new() { PurchaseOrderId = Guid.NewGuid(), IsActive = true, ExternalId = "DDH_OLD" }];
        Assert.True((await f.Service.SetSupplierActiveAsync(supplier.SupplierId, false, supplier.UpdatedDate, default)).Success);
        var deletedAt = supplier.UpdatedDate;
        Assert.False(supplier.IsActive);
        Assert.Null(await f.Service.GetSupplierAsync(supplier.SupplierId, false, default));
        Assert.NotNull(await f.Service.GetSupplierAsync(supplier.SupplierId, true, default));
        Assert.Empty((await f.Service.GetSuppliersAsync(new(), default)).Items);
        Assert.Single((await f.Service.GetSuppliersAsync(new() { IncludeInactive = true }, default)).Items);
        Assert.True((await f.Service.SetSupplierActiveAsync(supplier.SupplierId, true, deletedAt, default)).Success);
        Assert.True(supplier.UpdatedDate > deletedAt);
        Assert.True(Assert.Single(supplier.SupplierContacts).IsPrimary);
        Assert.Equal("DDH_OLD", Assert.Single(supplier.PurchaseOrders).ExternalId);
        Assert.True(supplier.PurchaseOrders.First().IsActive);
    }

    [Fact]
    public async Task CreateMaterial_ValidatesCategoryAndDuplicates_ThenPreservesSnapshotsOnEdit()
    {
        var f = new Fixture();
        var category = f.Category();
        Assert.False((await f.Service.SaveMaterialAsync(null, new() { Name = "Resin", CategoryId = Guid.NewGuid() }, default)).Success);
        Assert.False((await f.Service.SaveMaterialAsync(null, new() { Name = "Resin", CategoryId = category.CategoryId, Weight = -1 }, default)).Success);
        var result = await f.Service.SaveMaterialAsync(null, new() { Name = "Resin", CategoryId = category.CategoryId, Barcode = "B1", Weight = 25 }, default);
        Assert.True(result.Success, result.Message);
        var material = Assert.Single(f.Store.Added.OfType<Material>());
        Assert.Equal("NVL_NH_1", material.ExternalId);
        Assert.Equal(f.User.CompanyId, material.CompanyId);
        Assert.False((await f.Service.SaveMaterialAsync(null, new() { Name = "Other", CategoryId = category.CategoryId, Barcode = "b1" }, default)).Success);
        material.PurchaseOrderDetails = [new() { MaterialNameSnapshot = "Resin", MaterialExternalIDSnapshot = material.ExternalId, IsActive = true }];
        result = await f.Service.SaveMaterialAsync(material.MaterialId, new() { Name = "New Resin", ClearFields = ["weight"], ExpectedUpdatedDate = material.UpdatedDate }, default);
        Assert.True(result.Success, result.Message);
        Assert.Null(material.Weight);
        Assert.Equal("Resin", Assert.Single(material.PurchaseOrderDetails).MaterialNameSnapshot);
        Assert.True((await f.Service.SetMaterialActiveAsync(material.MaterialId, false, material.UpdatedDate, default)).Success);
        Assert.False(material.IsActive);
        Assert.True((await f.Service.SetMaterialActiveAsync(material.MaterialId, true, material.UpdatedDate, default)).Success);
        Assert.True(material.IsActive);
    }

    [Fact]
    public async Task ForeignCategoryAndInactiveCategory_CannotBeUsedOrRestored()
    {
        var f = new Fixture();
        var category = f.Category();
        category.CompanyId = Guid.NewGuid();
        Assert.False((await f.Service.SaveMaterialAsync(null, new() { Name = "Resin", CategoryId = category.CategoryId }, default)).Success);
        category.CompanyId = f.User.CompanyId!.Value;
        category.IsActive = false;
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = f.User.CompanyId.Value, CategoryId = category.CategoryId, IsActive = false };
        f.Store.Seed(nameof(f.Db.Materials), material);
        Assert.False((await f.Service.SetMaterialActiveAsync(material.MaterialId, true, null, default)).Success);
        Assert.False(material.IsActive);
    }

    [Fact]
    public async Task Contacts_PrimaryIsUnique_AndRestoreDoesNotStealPrimary()
    {
        var f = new Fixture();
        var supplier = f.Supplier();
        var first = await f.Service.SaveContactAsync(supplier.SupplierId, null, new() { FirstName = "A", IsPrimary = true }, default);
        Assert.True(first.Success, first.Message);
        Assert.Contains(f.Store.Added, x => x is SupplierContact c && c.ContactId == first.Data!.Id);
        var oldVersion = supplier.UpdatedDate;
        var second = await f.Service.SaveContactAsync(supplier.SupplierId, null, new() { FirstName = "B", IsPrimary = true, ExpectedUpdatedDate = supplier.UpdatedDate }, default);
        Assert.True(second.Success, second.Message);
        Assert.Single(supplier.SupplierContacts, x => x.IsPrimary == true);
        Assert.False((await f.Service.SetContactActiveAsync(supplier.SupplierId, first.Data!.Id, false, oldVersion, default)).Success);
        Assert.True((await f.Service.SetContactActiveAsync(supplier.SupplierId, first.Data.Id, false, supplier.UpdatedDate, default)).Success);
        Assert.True((await f.Service.SetContactActiveAsync(supplier.SupplierId, first.Data.Id, true, supplier.UpdatedDate, default)).Success);
        Assert.False(supplier.SupplierContacts.First(x => x.ContactId == first.Data.Id).IsPrimary);
        Assert.Equal(second.Data!.Id, supplier.SupplierContacts.Single(x => x.IsPrimary == true).ContactId);
    }

    [Fact]
    public async Task Addresses_RequireLine_AndCannotModifyChildOfAnotherSupplier()
    {
        var f = new Fixture();
        var supplier = f.Supplier();
        Assert.False((await f.Service.SaveAddressAsync(supplier.SupplierId, null, new() { City = "HCM" }, default)).Success);
        var result = await f.Service.SaveAddressAsync(supplier.SupplierId, null, new() { AddressLine = "Address", IsPrimary = true }, default);
        Assert.True(result.Success, result.Message);
        Assert.Contains(f.Store.Added, x => x is SupplierAddress a && a.AddressId == result.Data!.Id);
        Assert.False((await f.Service.SaveAddressAsync(supplier.SupplierId, Guid.NewGuid(), new() { AddressLine = "Forged", ExpectedUpdatedDate = supplier.UpdatedDate }, default)).Success);
        Assert.False((await f.Service.SetAddressActiveAsync(supplier.SupplierId, Guid.NewGuid(), false, supplier.UpdatedDate, default)).Success);
        Assert.Equal("Address", Assert.Single(supplier.SupplierAddresses).AddressLine);
        Assert.True((await f.Service.SetAddressActiveAsync(supplier.SupplierId, result.Data!.Id, false, supplier.UpdatedDate, default)).Success);
        Assert.True((await f.Service.SetAddressActiveAsync(supplier.SupplierId, result.Data.Id, true, supplier.UpdatedDate, default)).Success);
        Assert.False(Assert.Single(supplier.SupplierAddresses).IsPrimary);
    }

    [Fact]
    public async Task OtherCompany_IsInvisibleAndCannotBeMutated()
    {
        var f = new Fixture();
        var supplier = f.Supplier(); supplier.CompanyId = Guid.NewGuid();
        Assert.Null(await f.Service.GetSupplierAsync(supplier.SupplierId, true, default));
        Assert.False((await f.Service.SaveSupplierAsync(supplier.SupplierId, new() { SupplierName = "Forged" }, default)).Success);
        Assert.False((await f.Service.SetSupplierActiveAsync(supplier.SupplierId, false, null, default)).Success);
        Assert.False((await f.Service.SaveContactAsync(supplier.SupplierId, null, new() { FirstName = "Forged" }, default)).Success);
        Assert.Equal(0, f.Store.Saves);
    }

    [Theory]
    [InlineData("SaleUser", true)]
    [InlineData("QCUser", true)]
    [InlineData("Purchaser", false)]
    public async Task ForbiddenRolesAndAnonymous_FailBeforeWrite(string role, bool authenticated)
    {
        var f = new Fixture(new() { Roles = [role], IsAuthenticated = authenticated });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SaveSupplierAsync(null, new() { SupplierName = "Blocked" }, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetSuppliersAsync(new(), default));
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task ForeignEmployee_CannotMutate_AndInactiveCodeCannotBeReused()
    {
        var f = new Fixture();
        f.Store.Seed(nameof(f.Db.Employees), new Employee { EmployeeId = f.User.EmployeeId!.Value, CompanyId = Guid.NewGuid(), IsActive = true });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SaveSupplierAsync(null, new() { SupplierName = "Blocked" }, default));
        f = new Fixture();
        var supplier = f.Supplier(); supplier.IsActive = false; supplier.ExternalId = "OLD";
        Assert.False((await f.Service.SaveSupplierAsync(null, new() { SupplierName = "Duplicate", ExternalId = "old" }, default)).Success);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task SupplierLinks_IncludeInactiveOnlyWhenRequested_AndNeverOtherCompany()
    {
        var f = new Fixture();
        var supplier = f.Supplier();
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = f.User.CompanyId!.Value, IsActive = true };
        f.Store.Seed(nameof(f.Db.Materials), material);
        f.Store.Seed(nameof(f.Db.MaterialsSuppliers),
            new MaterialsSupplier { MaterialsSuppliersId = Guid.NewGuid(), MaterialId = material.MaterialId, Material = material, SupplierId = supplier.SupplierId, Supplier = supplier, IsActive = false },
            new MaterialsSupplier { MaterialsSuppliersId = Guid.NewGuid(), MaterialId = material.MaterialId, Material = material, Supplier = new Supplier { CompanyId = Guid.NewGuid(), IsActive = true }, IsActive = true });
        Assert.Empty((await f.Service.GetMaterialSupplierLinksAsync(material.MaterialId, new(), default))!.Items);
        Assert.Single((await f.Service.GetMaterialSupplierLinksAsync(material.MaterialId, new() { IncludeInactive = true }, default))!.Items);
        material.CompanyId = Guid.NewGuid();
        Assert.Null(await f.Service.GetMaterialSupplierLinksAsync(material.MaterialId, new() { IncludeInactive = true }, default));
    }

    [Theory]
    [InlineData("Purchaser")]
    [InlineData("PLPUUser")]
    public async Task ExplicitPurchaseOrderPermission_DoesNotGrantCatalogWrites(string role)
    {
        var f = new Fixture(new() { Roles = [role], HasExplicitPermissionSet = true, Permissions = [ApplicationPermissions.Purchasing.ManagePurchaseOrders] });
        Assert.Empty((await f.Service.GetSuppliersAsync(new(), default)).Items);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SaveSupplierAsync(null, new() { SupplierName = "Blocked" }, default));
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public async Task ExplicitCatalogPermission_CanWriteMaster_ButCannotReadSupplierPrices()
    {
        var f = new Fixture(new() { Roles = ["Purchaser"], HasExplicitPermissionSet = true, Permissions = [ApplicationPermissions.Purchasing.ManageCatalog] });
        Assert.True((await f.Service.SaveSupplierAsync(null, new() { SupplierName = "Allowed" }, default)).Success);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.GetMaterialSupplierLinksAsync(Guid.NewGuid(), new(), default));
        Assert.False(new CurrentUserPermissionService(f.User).HasPermission("purchasing.catalog.unknown"));
    }

    [Fact]
    public async Task Options_AreActiveAndScoped_AndRestoreRejectsLegacyDuplicateCode()
    {
        var f = new Fixture();
        var category = f.Category();
        f.Store.Seed(nameof(f.Db.Categories), category, new Category { CategoryId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true });
        Assert.Single(await f.Service.GetCategoriesAsync(default));
        f.Store.Seed(nameof(f.Db.Units), new HRM.Domain.Entities.Unit { UnitId = Guid.NewGuid(), Name = "Kilogram", Symbol = "kg", IsActive = true }, new HRM.Domain.Entities.Unit { IsActive = false });
        Assert.Equal("kg", Assert.Single(await f.Service.GetUnitsAsync(default)).Symbol);
        var old = new Supplier { SupplierId = Guid.NewGuid(), CompanyId = f.User.CompanyId, ExternalId = "NCC_DUP", IsActive = false };
        var active = new Supplier { SupplierId = Guid.NewGuid(), CompanyId = f.User.CompanyId, ExternalId = "ncc_dup", IsActive = true };
        f.Store.Seed(nameof(f.Db.Suppliers), old, active);
        Assert.False((await f.Service.SetSupplierActiveAsync(old.SupplierId, true, null, default)).Success);
        Assert.False(old.IsActive);
    }

    [Fact]
    public async Task PriceHistory_AllSuppliersAndSingleLink_HaveSharedStablePaginationAndScope()
    {
        var f = new Fixture();
        var supplier = f.Supplier();
        var secondSupplier = f.Supplier();
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = f.User.CompanyId!.Value, IsActive = true };
        f.Store.Seed(nameof(f.Db.Materials), material);
        MaterialsSupplier Link(Supplier s) => new()
        {
            MaterialsSuppliersId = Guid.NewGuid(), MaterialId = material.MaterialId, Material = material,
            SupplierId = s.SupplierId, Supplier = s, IsActive = true
        };
        var first = Link(supplier);
        var second = Link(secondSupplier);
        var foreign = Link(new Supplier { SupplierId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), IsActive = true });
        f.Store.Seed(nameof(f.Db.MaterialsSuppliers), first, second, foreign);
        var at = new DateTime(2026, 10, 20);
        PriceHistory History(MaterialsSupplier link, string id, decimal? price, DateTime? date) => new()
        {
            PriceHistoryId = Guid.Parse(id), MaterialsSuppliersId = link.MaterialsSuppliersId,
            MaterialsSuppliers = link, OldPrice = price, Currency = null, CreateDate = date,
            CreatedBy = Guid.NewGuid(),
            CreatedByNavigation = new Employee { CompanyId = Guid.NewGuid(), FullName = "Must not leak" }
        };
        f.Store.Seed(nameof(f.Db.PriceHistories),
            History(first, "00000000-0000-0000-0000-000000000001", 0, at),
            History(second, "00000000-0000-0000-0000-000000000002", 200, at),
            History(first, "00000000-0000-0000-0000-000000000003", null, null),
            History(foreign, "00000000-0000-0000-0000-000000000004", 999, at));
        var page1 = (await f.Service.GetMaterialPriceHistoryAsync(material.MaterialId, null, new() { PageSize = 1 }, default))!;
        var page2 = (await f.Service.GetMaterialPriceHistoryAsync(material.MaterialId, null, new() { PageSize = 1, PageNumber = 2 }, default))!;
        Assert.Equal(3, page1.TotalCount);
        Assert.True(page1.HasNextPage);
        Assert.Equal(second.MaterialsSuppliersId, Assert.Single(page1.Items).MaterialsSupplierId);
        var row = Assert.Single(page2.Items);
        Assert.Equal(first.MaterialsSuppliersId, row.MaterialsSupplierId);
        Assert.Equal(0m, row.OldPrice);
        Assert.Null(row.Currency);
        Assert.Null(row.RecordedByEmployeeId);
        Assert.Null(row.RecordedByName);
        var single = (await f.Service.GetMaterialPriceHistoryAsync(material.MaterialId, first.MaterialsSuppliersId, new(), default))!;
        Assert.Equal(2, single.TotalCount);
        Assert.Null(single.Items.Last().OldPrice);
        Assert.Null(single.Items.Last().RecordedAt);
        Assert.Null(await f.Service.GetMaterialPriceHistoryAsync(material.MaterialId, foreign.MaterialsSuppliersId, new(), default));
        Assert.Null(await f.Service.GetMaterialPriceHistoryAsync(Guid.NewGuid(), first.MaterialsSuppliersId, new(), default));
        second.IsActive = false;
        Assert.Null(await f.Service.GetMaterialPriceHistoryAsync(material.MaterialId, second.MaterialsSuppliersId, new(), default));
        Assert.Single((await f.Service.GetMaterialPriceHistoryAsync(material.MaterialId, second.MaterialsSuppliersId,
            new() { IncludeInactive = true }, default))!.Items);
        material.CompanyId = Guid.NewGuid();
        Assert.Null(await f.Service.GetMaterialPriceHistoryAsync(material.MaterialId, null, new() { IncludeInactive = true }, default));
    }

    [Theory]
    [InlineData("Sale")]
    [InlineData("QC")]
    public async Task PriceHistory_DeniesRolesWithoutPurchaseOrderCapability(string role)
    {
        var f = new Fixture(new() { Roles = [role] });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.GetMaterialPriceHistoryAsync(Guid.NewGuid(), null, new(), default));
    }

    [Fact]
    public async Task PriceHistory_CatalogOnlyPermissionCannotReadPrices()
    {
        var f = new Fixture(new() { HasExplicitPermissionSet = true, Permissions = [ApplicationPermissions.Purchasing.ManageCatalog] });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Service.GetMaterialPriceHistoryAsync(Guid.NewGuid(), null, new(), default));
    }

    [Fact]
    public async Task PriceHistory_UnauthenticatedCallerIsDenied_AndEmptyHistoryIsAnEmptyPage()
    {
        var denied = new Fixture(new() { IsAuthenticated = false, Roles = ["Purchaser"] });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            denied.Service.GetMaterialPriceHistoryAsync(Guid.NewGuid(), null, new(), default));
        var f = new Fixture();
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = f.User.CompanyId!.Value, IsActive = true };
        f.Store.Seed(nameof(f.Db.Materials), material);
        var result = await f.Service.GetMaterialPriceHistoryAsync(material.MaterialId, null, new(), default);
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public async Task SupplierLinks_LatestPriceUsesSharedSelectorAndMatchingSourceDateAndCurrency()
    {
        var f = new Fixture();
        var supplier = f.Supplier();
        var company = f.User.CompanyId!.Value;
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = company, IsActive = true };
        f.Store.Seed(nameof(f.Db.Materials), material);
        var at = new DateTime(2026, 10, 20);
        var link = new MaterialsSupplier
        {
            MaterialsSuppliersId = Guid.NewGuid(), MaterialId = material.MaterialId, Material = material,
            SupplierId = supplier.SupplierId, Supplier = supplier, IsActive = true,
            CurrentPrice = 14000, Currency = "USD", CreateDate = at.AddDays(-1), UpdatedDate = at
        };
        f.Store.Seed(nameof(f.Db.MaterialsSuppliers), link);
        PurchaseOrder Order(Guid scope, DateTime date, decimal? price, string? status = null) => new()
        {
            PurchaseOrderId = Guid.NewGuid(), SupplierId = supplier.SupplierId, CompanyId = scope,
            IsActive = true, CreateDate = date, Status = status,
            PurchaseOrderDetails = [new() { PurchaseOrderDetailId = Guid.NewGuid(), MaterialId = material.MaterialId,
                Material = material, IsActive = true, UnitPriceAgreed = price }]
        };
        var po = Order(company, at, 12000);
        supplier.PurchaseOrders = [po, Order(Guid.NewGuid(), at.AddDays(10), 99999),
            Order(company, at.AddDays(9), 88888, "Cancelled")];
        var result = Assert.Single((await f.Service.GetMaterialSupplierLinksAsync(material.MaterialId, new(), default))!.Items);
        Assert.Equal(14000m, result.CurrentPrice);
        Assert.Equal("PurchaseOrder", result.LatestPrice.Source);
        Assert.Equal(12000m, result.LatestPrice.Amount);
        Assert.Equal("VND", result.LatestPrice.Currency);
        Assert.Equal(at, result.LatestPrice.PriceDate);
        link.UpdatedDate = at.AddDays(1);
        result = Assert.Single((await f.Service.GetMaterialSupplierLinksAsync(material.MaterialId, new(), default))!.Items);
        Assert.Equal("MaterialSupplier", result.LatestPrice.Source);
        Assert.Equal("USD", result.LatestPrice.Currency);
        Assert.Equal(link.UpdatedDate, result.LatestPrice.PriceDate);
        link.CurrentPrice = null;
        po.PurchaseOrderDetails.Single().UnitPriceAgreed = 0;
        result = Assert.Single((await f.Service.GetMaterialSupplierLinksAsync(material.MaterialId, new(), default))!.Items);
        Assert.Equal(0m, result.LatestPrice.Amount);
        Assert.Equal("PurchaseOrder", result.LatestPrice.Source);
        po.PurchaseOrderDetails.Single().UnitPriceAgreed = null;
        result = Assert.Single((await f.Service.GetMaterialSupplierLinksAsync(material.MaterialId, new(), default))!.Items);
        Assert.Equal("Unknown", result.LatestPrice.Source);
        Assert.Null(result.LatestPrice.Amount);
        Assert.Null(result.LatestPrice.Currency);
        Assert.Null(result.LatestPrice.PriceDate);
    }

    [Fact]
    public void SupplierLinks_BatchPriceQueryTranslatesToPostgresTopOne()
    {
        using var db = new HRM.Infrastructure.DatabaseContext.ApplicationDbs.ApplicationDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<HRM.Infrastructure.DatabaseContext.ApplicationDbs.ApplicationDbContext>()
                .UseNpgsql().Options);
        var sql = CatalogLatestPurchasePriceQuery.Build(db, Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()]).ToQueryString();
        Assert.Contains("LATERAL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT 1", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CompanyId", sql);
    }

    [Fact]
    public async Task Aggregate_CreatesMasterTwoSuppliersAndHistory_InSingleSave()
    {
        var f = new Fixture();
        var category = f.Category();
        var suppliers = new[] { new Supplier { SupplierId = Guid.NewGuid(), CompanyId = f.User.CompanyId, IsActive = true },
            new Supplier { SupplierId = Guid.NewGuid(), CompanyId = f.User.CompanyId, IsActive = true } };
        f.Store.Seed(nameof(f.Db.Suppliers), suppliers);
        var saved = await f.Service.SaveMaterialWithSupplierPricesAsync(null, new()
        {
            Material = new() { Name = "New material", CategoryId = category.CategoryId },
            SupplierPrices = suppliers.Select(s => new CatalogSupplierPriceWriteRequest { SupplierId = s.SupplierId, NewPrice = 0, Currency = "VND" }).ToArray()
        }, default);
        Assert.True(saved.Success, saved.Message);
        Assert.Equal(1, f.Store.Saves);
        Assert.True(f.Store.Transaction.Committed);
        Assert.Equal(2, saved.Data!.SupplierPrices.Count);
        Assert.Equal(2, f.Store.Added.OfType<MaterialsSupplier>().Count());
        Assert.Equal(2, f.Store.Added.OfType<PriceHistory>().Count());
        Assert.All(f.Store.Added.OfType<PriceHistory>(), x => Assert.Null(x.OldPrice));
        Assert.All(saved.Data.SupplierPrices, x => Assert.Equal(0m, x.CurrentPrice));
    }

    [Fact]
    public async Task Aggregate_InvalidSecondSupplierRollsBackBeforeAnySave()
    {
        var f = new Fixture();
        var supplier = f.Supplier();
        var category = f.Category();
        var result = await f.Service.SaveMaterialWithSupplierPricesAsync(null, new()
        {
            Material = new() { Name = "Must not persist", CategoryId = category.CategoryId },
            SupplierPrices = [new() { SupplierId = supplier.SupplierId, NewPrice = 10 }, new() { SupplierId = Guid.NewGuid(), NewPrice = 20 }]
        }, default);
        Assert.False(result.Success);
        Assert.Equal(0, f.Store.Saves);
        Assert.True(f.Store.Transaction.RolledBack);
        Assert.False(f.Store.Transaction.Committed);
        Assert.Empty(f.Store.Added.OfType<PriceHistory>());
    }

    [Fact]
    public async Task Aggregate_ExistingPriceUsesStrictBaselines_AndSamePriceCreatesConfirmation()
    {
        var f = new Fixture();
        var supplier = f.Supplier();
        var category = f.Category();
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = f.User.CompanyId!.Value,
            IsActive = true, Name = "Old", CategoryId = category.CategoryId, UpdatedDate = new(2026, 10, 19) };
        var link = new MaterialsSupplier { MaterialsSuppliersId = Guid.NewGuid(), MaterialId = material.MaterialId,
            Material = material, SupplierId = supplier.SupplierId, Supplier = supplier, CurrentPrice = 10, Currency = "VND",
            IsActive = true, UpdatedDate = new(2026, 10, 19) };
        f.Store.Seed(nameof(f.Db.Materials), material);
        f.Store.Seed(nameof(f.Db.MaterialsSuppliers), link);
        var token = material.UpdatedDate;
        CatalogMaterialAggregateRequest Request(DateTime? linkToken) => new()
        {
            Material = new() { Name = "Updated", ExpectedUpdatedDate = token },
            SupplierPrices = [new() { MaterialsSupplierId = link.MaterialsSuppliersId, NewPrice = 10,
                ExpectedUpdatedDate = linkToken, ExpectedCurrentPrice = 10, ExpectedCurrency = "VND" }]
        };
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException>(() =>
            f.Service.SaveMaterialWithSupplierPricesAsync(material.MaterialId, Request(null), default));
        Assert.Equal(0, f.Store.Saves);
        // Fake store does not undo tracked mutations on rollback; restore baseline for second transaction.
        material.UpdatedDate = token;
        var saved = await f.Service.SaveMaterialWithSupplierPricesAsync(material.MaterialId, Request(link.UpdatedDate), default);
        Assert.True(saved.Success, saved.Message);
        Assert.False(Assert.Single(saved.Data!.SupplierPrices).PriceChanged);
        Assert.Equal(10m, Assert.Single(f.Store.Added.OfType<PriceHistory>()).OldPrice);
        Assert.Equal(1, f.Store.Saves);
    }

    [Fact]
    public async Task Aggregate_SaveFailureDoesNotCommit_AndCatalogOnlyCannotWritePrices()
    {
        var f = new Fixture();
        var category = f.Category();
        f.Store.ThrowOnSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SaveMaterialWithSupplierPricesAsync(null,
            new() { Material = new() { Name = "Fail", CategoryId = category.CategoryId } }, default));
        Assert.True(f.Store.Transaction.RolledBack);
        Assert.False(f.Store.Transaction.Committed);
        var denied = new Fixture(new() { HasExplicitPermissionSet = true, Permissions = [ApplicationPermissions.Purchasing.ManageCatalog] });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => denied.Service.SaveMaterialWithSupplierPricesAsync(null,
            new() { Material = new() { Name = "Denied" } }, default));
        Assert.Equal(0, denied.Store.Saves);
    }

    [Fact]
    public async Task Aggregate_RejectsForeignLinkDuplicateSupplierAndStaleMaster()
    {
        var f = new Fixture();
        var category = f.Category();
        var supplier = f.Supplier();
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = f.User.CompanyId!.Value,
            IsActive = true, CategoryId = category.CategoryId, UpdatedDate = new(2026, 10, 19) };
        f.Store.Seed(nameof(f.Db.Materials), material);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => f.Service.SaveMaterialWithSupplierPricesAsync(material.MaterialId,
            new() { Material = new() { Name = "Stale", ExpectedUpdatedDate = null } }, default));
        Assert.Equal(0, f.Store.Saves);
        var duplicate = await f.Service.SaveMaterialWithSupplierPricesAsync(null,
            new() { Material = new() { Name = "Duplicate", CategoryId = category.CategoryId },
                SupplierPrices = [new() { SupplierId = supplier.SupplierId, NewPrice = 10 }, new() { SupplierId = supplier.SupplierId, NewPrice = 20 }] }, default);
        Assert.False(duplicate.Success);
        Assert.Equal(0, f.Store.Saves);
        var foreignLink = new MaterialsSupplier { MaterialsSuppliersId = Guid.NewGuid(), MaterialId = Guid.NewGuid(),
            SupplierId = supplier.SupplierId, IsActive = true };
        f.Store.Seed(nameof(f.Db.MaterialsSuppliers), foreignLink);
        var foreign = await f.Service.SaveMaterialWithSupplierPricesAsync(material.MaterialId,
            new() { Material = new() { Name = "Foreign", ExpectedUpdatedDate = material.UpdatedDate },
                SupplierPrices = [new() { MaterialsSupplierId = foreignLink.MaterialsSuppliersId, NewPrice = 10 }] }, default);
        Assert.False(foreign.Success);
        Assert.Equal(0, f.Store.Saves);
        Assert.Empty(f.Store.Added.OfType<PriceHistory>());
    }

    [Fact]
    public async Task MaterialWrite_FlatPayloadCreatesPrices_AndKeepsOriginalSavedResponse()
    {
        var f = new Fixture();
        var category = f.Category();
        var supplier = f.Supplier();
        var saved = await f.Service.SaveMaterialAsync(null, new()
        {
            Name = "Flat API", CategoryId = category.CategoryId,
            SupplierPrices = [new() { SupplierId = supplier.SupplierId, NewPrice = 100, Currency = "VND", IsPreferred = true, MinDeliveryDays = 3 }]
        }, default);
        Assert.True(saved.Success, saved.Message);
        Assert.Equal(Assert.Single(f.Store.Added.OfType<Material>()).MaterialId, saved.Data!.Id);
        var createdLink = Assert.Single(f.Store.Added.OfType<MaterialsSupplier>());
        Assert.True(createdLink.IsPreferred);
        Assert.Equal(3, createdLink.MinDeliveryDays);
        Assert.Single(f.Store.Added.OfType<PriceHistory>());
        Assert.Equal(1, f.Store.Saves);
    }

    [Fact]
    public async Task MaterialWrite_PriceOnlyPatchUsesAtomicSave_AndBadSupplierCannotSaveMaster()
    {
        var f = new Fixture();
        var category = f.Category();
        var supplier = f.Supplier();
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = f.User.CompanyId!.Value,
            Name = "Unchanged name", IsActive = true, CategoryId = category.CategoryId, UpdatedDate = new(2026, 10, 19) };
        f.Store.Seed(nameof(f.Db.Materials), material);
        var saved = await f.Service.SaveMaterialAsync(material.MaterialId, new()
        {
            ExpectedUpdatedDate = material.UpdatedDate,
            SupplierPrices = [new() { SupplierId = supplier.SupplierId, NewPrice = 0 }]
        }, default);
        Assert.True(saved.Success, saved.Message);
        Assert.Equal("Unchanged name", material.Name);
        Assert.Equal(0m, Assert.Single(f.Store.Added.OfType<MaterialsSupplier>()).CurrentPrice);
        f = new Fixture();
        category = f.Category();
        var failed = await f.Service.SaveMaterialAsync(null, new()
        {
            Name = "Must rollback", CategoryId = category.CategoryId,
            SupplierPrices = [new() { SupplierId = Guid.NewGuid(), NewPrice = 100 }]
        }, default);
        Assert.False(failed.Success);
        Assert.Equal(0, f.Store.Saves);
        Assert.True(f.Store.Transaction.RolledBack);
    }

    [Fact]
    public async Task MaterialWrite_NoPriceRowsNeedsOnlyCatalog_AndEmptyPatchStaysInvalid()
    {
        var f = new Fixture(new() { HasExplicitPermissionSet = true, Permissions = [ApplicationPermissions.Purchasing.ManageCatalog] });
        var category = f.Category();
        Assert.True((await f.Service.SaveMaterialAsync(null, new() { Name = "Master only", CategoryId = category.CategoryId }, default)).Success);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.SaveMaterialAsync(null,
            new() { Name = "Forbidden price", CategoryId = category.CategoryId,
                SupplierPrices = [new() { SupplierId = Guid.NewGuid(), NewPrice = 100 }] }, default));
        Assert.NotNull(CatalogPatch.Validate(new CatalogMaterialWriteRequest()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MaterialWrite_MetadataOnlySwitchesPreferred_InsideTransaction(bool failSecondSave)
    {
        var f = new Fixture();
        var category = f.Category();
        var supplier = f.Supplier();
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = f.User.CompanyId!.Value,
            Name = "Material", IsActive = true, CategoryId = category.CategoryId, UpdatedDate = new(2026, 10, 19) };
        var link = new MaterialsSupplier { MaterialsSuppliersId = Guid.NewGuid(), MaterialId = material.MaterialId,
            Material = material, SupplierId = supplier.SupplierId, Supplier = supplier, CurrentPrice = 10,
            Currency = "VND", IsActive = true, UpdatedDate = new(2026, 10, 19) };
        var oldPreferred = new MaterialsSupplier { MaterialsSuppliersId = Guid.NewGuid(), MaterialId = material.MaterialId,
            SupplierId = Guid.NewGuid(), Supplier = supplier, IsPreferred = true, IsActive = false };
        f.Store.Seed(nameof(f.Db.Materials), material);
        f.Store.Seed(nameof(f.Db.MaterialsSuppliers), link, oldPreferred);
        f.Store.FailOnSaveNumber = failSecondSave ? 2 : null;
        var token = link.UpdatedDate;
        var request = new CatalogMaterialWriteRequest { ExpectedUpdatedDate = material.UpdatedDate,
            SupplierPrices = [new() { MaterialsSupplierId = link.MaterialsSuppliersId, IsPreferred = true,
                MinDeliveryDays = 3, ExpectedUpdatedDate = token, ExpectedCurrentPrice = 10, ExpectedCurrency = "VND" }] };
        if (failSecondSave)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SaveMaterialAsync(material.MaterialId, request, default));
            Assert.True(f.Store.Transaction.RolledBack);
            Assert.False(f.Store.Transaction.Committed);
            return;
        }
        var saved = await f.Service.SaveMaterialAsync(material.MaterialId, request, default);
        Assert.True(saved.Success, saved.Message);
        Assert.True(link.IsPreferred);
        Assert.False(oldPreferred.IsPreferred);
        Assert.Equal(3, link.MinDeliveryDays);
        Assert.Equal(10m, link.CurrentPrice);
        Assert.Equal("VND", link.Currency);
        Assert.True(link.UpdatedDate > token);
        Assert.Empty(f.Store.Added.OfType<PriceHistory>());
        Assert.Equal(2, f.Store.Saves);
        Assert.True(f.Store.Transaction.Committed);
    }

    [Theory]
    [InlineData(null, true, null)]
    [InlineData(0, false, 0)]
    [InlineData(null, false, 5)]
    public async Task MaterialWrite_DeliveryClearZeroAndOmittedAreDistinct(int? days, bool clear, int? expected)
    {
        var f = new Fixture();
        var category = f.Category();
        var supplier = f.Supplier();
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = f.User.CompanyId!.Value,
            Name = "Material", IsActive = true, CategoryId = category.CategoryId, UpdatedDate = new(2026, 10, 19) };
        var link = new MaterialsSupplier { MaterialsSuppliersId = Guid.NewGuid(), MaterialId = material.MaterialId,
            SupplierId = supplier.SupplierId, CurrentPrice = 10, Currency = "VND", IsActive = true,
            MinDeliveryDays = 5, IsPreferred = true, UpdatedDate = new(2026, 10, 19) };
        f.Store.Seed(nameof(f.Db.Materials), material);
        f.Store.Seed(nameof(f.Db.MaterialsSuppliers), link);
        var result = await f.Service.SaveMaterialAsync(material.MaterialId, new() { ExpectedUpdatedDate = material.UpdatedDate,
            SupplierPrices = [new() { MaterialsSupplierId = link.MaterialsSuppliersId, IsPreferred = false,
                MinDeliveryDays = days, ClearMinDeliveryDays = clear, ExpectedUpdatedDate = link.UpdatedDate,
                ExpectedCurrentPrice = 10, ExpectedCurrency = "VND" }] }, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(expected, link.MinDeliveryDays);
        Assert.False(link.IsPreferred);
        Assert.Empty(f.Store.Added.OfType<PriceHistory>());
    }

    [Fact]
    public async Task MaterialWrite_RejectsInvalidSupplierMetadataBeforeSaving()
    {
        CatalogSupplierPriceWriteRequest[][] cases = [
            [new() { SupplierId = Guid.NewGuid(), NewPrice = 1, MinDeliveryDays = -1 }],
            [new() { SupplierId = Guid.NewGuid(), NewPrice = 1, MinDeliveryDays = 3651 }],
            [new() { SupplierId = Guid.NewGuid(), NewPrice = 1, MinDeliveryDays = 3, ClearMinDeliveryDays = true }],
            [new() { SupplierId = Guid.NewGuid(), IsPreferred = true }],
            [new() { SupplierId = Guid.NewGuid(), NewPrice = 1, IsPreferred = true },
             new() { SupplierId = Guid.NewGuid(), NewPrice = 2, IsPreferred = true }]
        ];
        foreach (var rows in cases)
        {
            var f = new Fixture();
            var result = await f.Service.SaveMaterialAsync(null, new() { Name = "Invalid", SupplierPrices = rows }, default);
            Assert.False(result.Success);
            Assert.Equal(0, f.Store.Saves);
        }
    }

    [Theory]
    [InlineData("success")]
    [InlineData("only")]
    [InlineData("timestamp")]
    [InlineData("price")]
    [InlineData("currency")]
    [InlineData("foreign")]
    [InlineData("inactive")]
    [InlineData("saveFailure")]
    public async Task MaterialWrite_DeactivationIsAtomicWithMasterAndOtherSupplier(string scenario)
    {
        var f = new Fixture();
        var category = f.Category();
        var supplier = f.Supplier();
        var otherSupplier = new Supplier { SupplierId = Guid.NewGuid(), CompanyId = f.User.CompanyId, IsActive = true };
        f.Store.Seed(nameof(f.Db.Suppliers), supplier, otherSupplier);
        var material = new Material { MaterialId = Guid.NewGuid(), CompanyId = f.User.CompanyId!.Value,
            Name = "Original", IsActive = true, CategoryId = category.CategoryId, UpdatedDate = new(2026, 10, 19) };
        var old = new MaterialsSupplier { MaterialsSuppliersId = Guid.NewGuid(), MaterialId = material.MaterialId,
            SupplierId = supplier.SupplierId, Supplier = supplier, Material = material, CurrentPrice = 10, Currency = "VND",
            MinDeliveryDays = 5, IsPreferred = true, IsActive = scenario != "inactive", UpdatedDate = new(2026, 10, 19) };
        var other = new MaterialsSupplier { MaterialsSuppliersId = Guid.NewGuid(), MaterialId = material.MaterialId,
            SupplierId = otherSupplier.SupplierId, Supplier = otherSupplier, Material = material,
            CurrentPrice = 20, Currency = "VND", IsActive = true, UpdatedDate = new(2026, 10, 19) };
        if (scenario == "foreign") supplier.CompanyId = Guid.NewGuid();
        f.Store.Seed(nameof(f.Db.Materials), material);
        f.Store.Seed(nameof(f.Db.MaterialsSuppliers), old, other);
        var history = new PriceHistory { PriceHistoryId = Guid.NewGuid(), MaterialsSuppliersId = old.MaterialsSuppliersId, OldPrice = 7 };
        f.Store.Seed(nameof(f.Db.PriceHistories), history);
        // Choosing a replacement preferred link requires two flushes. Failure in the second must not commit the first.
        f.Store.FailOnSaveNumber = scenario == "saveFailure" ? 2 : null;
        var token = old.UpdatedDate;
        var request = new CatalogMaterialWriteRequest { Name = "Changed", ExpectedUpdatedDate = material.UpdatedDate,
            SupplierPrices = [
                new() { MaterialsSupplierId = old.MaterialsSuppliersId, Operation = "deactivate",
                    ExpectedUpdatedDate = scenario == "timestamp" ? null : token,
                    ExpectedCurrentPrice = scenario == "price" ? 99 : 10,
                    ExpectedCurrency = scenario == "currency" ? "USD" : "VND" },
                new() { MaterialsSupplierId = other.MaterialsSuppliersId, NewPrice = 25, IsPreferred = true,
                    ExpectedUpdatedDate = other.UpdatedDate, ExpectedCurrentPrice = 20, ExpectedCurrency = "VND" }
            ] };
        if (scenario == "only") request = new() { ExpectedUpdatedDate = material.UpdatedDate,
            SupplierPrices = [request.SupplierPrices![0]] };
        if (scenario is "timestamp" or "price" or "currency")
        {
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => f.Service.SaveMaterialAsync(material.MaterialId, request, default));
            Assert.Equal(0, f.Store.Saves);
        }
        else if (scenario == "saveFailure")
            await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.SaveMaterialAsync(material.MaterialId, request, default));
        else
        {
            var result = await f.Service.SaveMaterialAsync(material.MaterialId, request, default);
            Assert.Equal(scenario is "success" or "only", result.Success);
        }
        if (scenario is not ("success" or "only"))
        {
            Assert.False(f.Store.Transaction.Committed);
            Assert.True(f.Store.Transaction.RolledBack);
            if (scenario != "saveFailure") Assert.Empty(f.Store.Added.OfType<PriceHistory>());
            return; // Fake store does not restore tracked entities when a transaction rolls back.
        }
        Assert.True(f.Store.Transaction.Committed);
        Assert.Equal(scenario == "only" ? "Original" : "Changed", material.Name);
        Assert.False(old.IsActive);
        Assert.False(old.IsPreferred);
        Assert.Equal(10m, old.CurrentPrice);
        Assert.Equal("VND", old.Currency);
        Assert.Equal(5, old.MinDeliveryDays);
        Assert.True(old.UpdatedDate > token);
        Assert.True(supplier.IsActive);
        if (scenario == "only")
        {
            Assert.NotEqual(true, other.IsPreferred);
            Assert.True(other.IsActive);
            Assert.Equal(20m, other.CurrentPrice);
            Assert.Empty(f.Store.Added.OfType<PriceHistory>());
            Assert.Equal(1, f.Store.Saves);
            return;
        }
        Assert.True(other.IsPreferred);
        Assert.Equal(25m, other.CurrentPrice);
        Assert.Equal(other.MaterialsSuppliersId, Assert.Single(f.Store.Added.OfType<PriceHistory>()).MaterialsSuppliersId);
        Assert.Contains(history, await f.Db.PriceHistories.ToListAsync());
        Assert.Contains(old, await f.Db.MaterialsSuppliers.ToListAsync());
    }

    [Fact]
    public async Task MaterialWrite_DeactivationRejectsAmbiguousOperationsBeforeSave()
    {
        CatalogSupplierPriceWriteRequest[] invalid = [
            new() { MaterialsSupplierId = Guid.NewGuid(), Operation = "delete" },
            new() { SupplierId = Guid.NewGuid(), Operation = "deactivate" },
            new() { MaterialsSupplierId = Guid.NewGuid(), Operation = "deactivate", NewPrice = 0 },
            new() { MaterialsSupplierId = Guid.NewGuid(), Operation = "deactivate", Currency = "VND" },
            new() { MaterialsSupplierId = Guid.NewGuid(), Operation = "deactivate", IsPreferred = false },
            new() { MaterialsSupplierId = Guid.NewGuid(), Operation = "deactivate", MinDeliveryDays = 0 },
            new() { MaterialsSupplierId = Guid.NewGuid(), Operation = "deactivate", ClearMinDeliveryDays = true }
        ];
        foreach (var row in invalid)
        {
            var f = new Fixture();
            var result = await f.Service.SaveMaterialAsync(Guid.NewGuid(), new() { SupplierPrices = [row] }, default);
            Assert.False(result.Success);
            Assert.Equal(0, f.Store.Saves);
        }
        var create = new Fixture();
        Assert.False((await create.Service.SaveMaterialAsync(null, new() {
            SupplierPrices = [new() { MaterialsSupplierId = Guid.NewGuid(), Operation = "deactivate" }] }, default)).Success);
        Assert.Equal(0, create.Store.Saves);
    }

    private sealed class Fixture
    {
        internal readonly IPLMWriteDbContext Db;
        internal readonly CatalogStore Store;
        internal readonly IqcWorkflowTests.User User;
        internal readonly PurchasingCatalogService Service;
        internal Fixture(IqcWorkflowTests.User? user = null)
        {
            Db = DispatchProxy.Create<IPLMWriteDbContext, CatalogStore>(); Store = (CatalogStore)Db;
            Store.ReflectAddedOnSave = true;
            User = user ?? new() { Roles = ["Purchaser"] };
            Store.Seed(nameof(Db.Employees), new Employee { EmployeeId = User.EmployeeId!.Value, CompanyId = User.CompanyId!.Value, IsActive = true });
            Service = new(Db, User, new CurrentUserPermissionService(User), new Clock(), new Codes());
        }
        internal Supplier Supplier()
        {
            var supplier = new Supplier { SupplierId = Guid.NewGuid(), CompanyId = User.CompanyId, IsActive = true, ExternalId = "NCC_OLD", SupplierName = "Supplier" };
            Store.Seed(nameof(Db.Suppliers), supplier); return supplier;
        }
        internal Category Category()
        {
            var category = new Category { CategoryId = Guid.NewGuid(), CompanyId = User.CompanyId!.Value, IsActive = true, ExternalId = "NH" };
            Store.Seed(nameof(Db.Categories), category); return category;
        }
    }

    public class CatalogStore : IqcTestStore
    {
        public int Locks { get; private set; }
        public int? FailOnSaveNumber { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == "SaveChangesAsync" && Saves + 1 == FailOnSaveNumber)
                throw new InvalidOperationException("Simulated second flush failure");
            if (method?.Name == "LockPurchasingCatalogAsync") { Locks++; return Task.CompletedTask; }
            if (method?.Name == "LockMaterialSupplierPricesAsync") return Task.CompletedTask;
            if (method?.Name == "EnsurePurchasingCatalogCounterAsync") return Task.CompletedTask;
            return base.Invoke(method, args);
        }
    }
    private sealed class Clock : IDateTimeProvider
    {
        public DateTime Now => new DateTime(2026, 10, 19, 10, 0, 0).AddTicks(123);
        public DateTime UtcNow => Now;
    }
    private sealed class Codes : IExternalIdService
    {
        public Task<string> GenerateGlobalCodeAsync(Guid companyId, string prefix, CancellationToken ct = default) => Task.FromResult(prefix + "_1");
        public Task<string> GenerateMonthlyCodeAsync(Guid companyId, string prefix, CancellationToken ct = default) => Task.FromResult(prefix + "_1");
    }
}
