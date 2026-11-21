using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Queries.GetWarehouseVoucherById;
using HRM.Application.Features.Warehouse.Queries.GetWarehouseVouchers;
using HRM.Application.Features.Warehouse.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Entities.OrderSchema;
using Xunit;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class WarehouseVoucherLabAccessTests
{
    [Theory]
    [InlineData(ApplicationRoles.Lab.LabUser, true, true)]
    [InlineData(ApplicationRoles.Sales.SaleUser, true, false)]
    [InlineData(ApplicationRoles.Quality.QCUser, false, false)]
    [InlineData(ApplicationRoles.Warehouse.KHOUser, true, true)]
    [InlineData(ApplicationRoles.Accounting.ACCUser, true, true)]
    public void ReadGate_PreservesRoleBoundaries(string role, bool canRead, bool canReadAll)
    {
        var user = new User { Roles = [role] };
        Assert.Equal(canRead, WarehouseVoucherAccessRules.CanRead(user));
        Assert.Equal(canReadAll, WarehouseVoucherAccessRules.CanReadAll(user));
        Assert.DoesNotContain(ApplicationRoles.Lab.LabUser, ApplicationRoleSets.Modules.Warehouse);
    }

    [Theory]
    [InlineData(false, "valid")]
    [InlineData(true, "null")]
    [InlineData(true, "empty")]
    public void Lab_RequiresAuthenticationAndCompany(bool authenticated, string company)
    {
        var user = new User
        {
            IsAuthenticated = authenticated,
            CompanyId = company == "null" ? null : company == "empty" ? Guid.Empty : Guid.NewGuid()
        };
        Assert.False(WarehouseVoucherAccessRules.CanRead(user));
        Assert.False(WarehouseVoucherAccessRules.CanReadAll(user));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lab_ListAndDetailStayCompanyScoped_EvenWithSaleRole(bool alsoSale)
    {
        var user = new User
        {
            Roles = alsoSale ? [ApplicationRoles.Lab.LabUser, ApplicationRoles.Sales.SaleUser] : [ApplicationRoles.Lab.LabUser]
        };
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        store.Seed(nameof(db.WarehouseVouchers),
            new WarehouseVoucher { VoucherId = 1, VoucherCode = "LOCAL", VoucherType = 9, CompanyId = user.CompanyId!.Value, RequestId = 1 },
            new WarehouseVoucher { VoucherId = 2, VoucherCode = "FOREIGN", CompanyId = Guid.NewGuid(), RequestId = 2 });
        store.Seed(nameof(db.WarehouseVoucherDetails),
            new WarehouseVoucherDetail { VoucherDetailId = 1, VoucherId = 1, ProductCode = "ITEM", QtyKg = 7 });
        store.Seed(nameof(db.WarehouseRequests), new WarehouseRequest
            { RequestId = 1, CompanyId = user.CompanyId!.Value, codeFromRequest = "PO-A" });
        store.Seed(nameof(db.PurchaseOrders), new PurchaseOrder
        {
            CompanyId = user.CompanyId!.Value, ExternalId = "PO-A",
            PurchaseOrderSnapshot = new() { SupplierNameSnapshot = "Supplier A", SupplierExternalIdSnapshot = "SUP-A" }
        });
        // Full-history readers must not ask the customer visibility service for a Sale scope.
        var visibility = new WarehouseStockVisibilityService(db, user, null!);
        var businessReader = new WarehouseVoucherBusinessReader(db, user, new CurrentUserPermissionService(user));
        var list = await new GetWarehouseVouchersQueryHandler(db, user, visibility, businessReader).Handle(new(), default);
        Assert.True(list.Success);
        var local = Assert.Single(list.Data!.Items);
        Assert.Equal(1L, local.VoucherId);
        Assert.Equal(7m, Assert.Single(local.Details).QtyKg);

        var detailHandler = new GetWarehouseVoucherByIdQueryHandler(db, user, visibility, businessReader);
        var detail = await detailHandler.Handle(new(1), default);
        Assert.True(detail.Success);
        Assert.Equal(7m, Assert.Single(detail.Data!.Details).QtyKg);
        Assert.Equal("PurchaseGoodsReceipt", local.BusinessType.Code);
        Assert.Equal(local.BusinessType, detail.Data.BusinessType);
        Assert.Equal("PO-A", detail.Data.PurchaseOrderCode);
        Assert.Equal(local.PurchaseOrderCode, detail.Data.PurchaseOrderCode);
        Assert.False(detail.Data.CanViewSupplier);
        Assert.Null(detail.Data.SupplierName);
        Assert.Equal(local.SupplierName, detail.Data.SupplierName);
        Assert.False((await detailHandler.Handle(new(2), default)).Success);
    }

    private sealed class User : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId => null;
        public Guid? CompanyId { get; init; } = Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [ApplicationRoles.Lab.LabUser];
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
