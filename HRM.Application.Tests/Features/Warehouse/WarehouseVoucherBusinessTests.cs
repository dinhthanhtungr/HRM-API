using HRM.Application.Abstractions.Persistence.Warehouse;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Warehouse.Services;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.WarehouseSchema;
using HRM.Domain.Entities.DeliverySchema;
using HRM.Domain.Entities.ManufacturingSchema;
using Xunit;

namespace HRM.Application.Tests.Features.Warehouse;

public sealed class WarehouseVoucherBusinessTests
{
    [Fact]
    public void ImsScopeKeepsOnlyUnambiguousCompanyPurchaseAndDeliveryVouchers()
    {
        var company = Guid.NewGuid();
        var otherCompany = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        store.Seed(nameof(db.PurchaseOrders),
            new PurchaseOrder { CompanyId = company, ExternalId = "PO" },
            new PurchaseOrder { CompanyId = otherCompany, ExternalId = "FOREIGN" },
            new PurchaseOrder { CompanyId = company, ExternalId = "DUP" },
            new PurchaseOrder { CompanyId = company, ExternalId = "DUP" });
        store.Seed(nameof(db.DeliveryOrders), new DeliveryOrder { CompanyId = company, ExternalId = "DO" });
        var fixtures = new[]
        {
            (Id: 1, Type: 5, Code: "PO", Company: company),
            (Id: 2, Type: 2, Code: "DO", Company: company),
            (Id: 3, Type: 6, Code: "PO", Company: company),
            (Id: 4, Type: 5, Code: "FOREIGN", Company: company),
            (Id: 5, Type: 5, Code: "DUP", Company: company),
            (Id: 6, Type: 5, Code: "PO", Company: otherCompany),
            (Id: 7, Type: 5, Code: "PO", Company: company)
        };
        store.Seed(nameof(db.WarehouseRequests), fixtures.Select(x => new WarehouseRequest
        {
            RequestId = x.Id, CompanyId = x.Company, codeFromRequest = x.Code,
            ReqType = (HRM.Domain.Enums.WareHouses.WareHouseRequestType)x.Type
        }).ToArray());
        store.Seed(nameof(db.WarehouseVouchers), fixtures.Select(x => new WarehouseVoucher
        {
            VoucherId = x.Id, RequestId = x.Id, CompanyId = x.Company, VoucherType = x.Type
        }).ToArray());
        store.Seed(nameof(db.OperationMaterialBuffers), new OperationMaterialBuffer { CompanyId = company, VoucherId = 7 });

        var visible = ImsWarehouseVoucherScope.Apply(db, db.WarehouseVouchers, company)
            .Select(x => x.VoucherId).Order().ToArray();

        Assert.Equal(new long[] { 1, 2 }, visible);
    }

    [Theory]
    [InlineData(1, "ImportFinishedGood")]
    [InlineData(2, "ExportFinishedGood")]
    [InlineData(3, "ImportDefectiveFinishedGood")]
    [InlineData(4, "ExportDefectiveFinishedGood")]
    [InlineData(5, "ImportRawMaterial")]
    [InlineData(6, "ExportRawMaterial")]
    [InlineData(7, "ImportDefectiveRawMaterial")]
    [InlineData(8, "ExportDefectiveRawMaterial")]
    [InlineData(9, "ImportOther")]
    [InlineData(10, "ExportOther")]
    [InlineData(11, "ImportReturn")]
    [InlineData(12, "ExportReturn")]
    [InlineData(13, "ImportMaterial")]
    [InlineData(14, "ExportMaterial")]
    [InlineData(99, "Unknown")]
    public async Task AllBaseTypesRemainVisibleWithoutSource(int type, string code)
    {
        var company = Guid.NewGuid();
        var (db, _) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var result = await CreateReader(db, company).ReadAsync(company, [new(1, type, "")], default);
        Assert.Equal(code, result[1].BusinessType.Code);
        Assert.NotEmpty(result[1].BusinessType.Name);
        Assert.NotEmpty(result[1].BusinessType.NameEn);
        Assert.Null(result[1].PurchaseOrderCode);
    }

    [Theory]
    [InlineData(5, "PurchaseRawMaterialReceipt")]
    [InlineData(9, "PurchaseGoodsReceipt")]
    [InlineData(13, "PurchaseSuppliesReceipt")]
    [InlineData(12, "PurchaseReturn")]
    public async Task PurchaseReceiptAndReturnResolveSupplierOnlyInsideCompany(int type, string code)
    {
        var company = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        store.Seed(nameof(db.PurchaseOrders), new PurchaseOrder
        {
            CompanyId = company, ExternalId = "PO-A", Comment = "note",
            PurchaseOrderSnapshot = new PurchaseOrderSnapshot { SupplierNameSnapshot = "Supplier A", SupplierExternalIdSnapshot = "SUP-A" }
        }, new PurchaseOrder
        {
            CompanyId = Guid.NewGuid(), ExternalId = "PO-FOREIGN",
            PurchaseOrderSnapshot = new PurchaseOrderSnapshot { SupplierNameSnapshot = "Hidden" }
        });
        var result = await CreateReader(db, company).ReadAsync(company,
            [new(1, type, " PO-A "), new(2, type, "PO-FOREIGN"), new(3, 2, "PO-A")], default);
        Assert.Equal(code, result[1].BusinessType.Code);
        Assert.Equal("PO-A", result[1].PurchaseOrderCode);
        Assert.Equal("Supplier A", result[1].SupplierName);
        Assert.Equal("SUP-A", result[1].SupplierExternalId);
        Assert.Null(result[2].PurchaseOrderCode);
        Assert.Null(result[2].SupplierName);
        Assert.Null(result[3].PurchaseOrderCode);
    }

    [Fact]
    public async Task DeliveryAndAdjustmentRequireVerifiedSource_NotJustTextOrPrefix()
    {
        var company = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var adjustmentId = Guid.NewGuid();
        store.Seed(nameof(db.DeliveryOrders), new DeliveryOrder { CompanyId = company, ExternalId = "PGH-A" });
        store.Seed(nameof(db.ManufacturingFormulaAdjustments), new ManufacturingFormulaAdjustment
            { CompanyId = company, ManufacturingFormulaAdjustmentId = adjustmentId });
        var result = await CreateReader(db, company).ReadAsync(company,
            [new(1, 2, "PGH-A"), new(2, 2, "PGH-UNKNOWN"),
             new(3, 10, $"VA: VA1; MfgFormulaAdjustment:{adjustmentId}; Issue:1"),
             new(4, 10, $"MfgFormulaAdjustment:{Guid.NewGuid()}")], default);
        Assert.Equal("DeliveryGoodsIssue", result[1].BusinessType.Code);
        Assert.Equal("ExportFinishedGood", result[2].BusinessType.Code);
        Assert.Equal("ColorAdjustmentIssue", result[3].BusinessType.Code);
        Assert.Equal("ExportOther", result[4].BusinessType.Code);
    }

    [Theory]
    [InlineData("NVL", "NVL", "PurchaseRawMaterialReceipt")]
    [InlineData("VT", "VT", "PurchaseSuppliesReceipt")]
    [InlineData("NVL", "VT", "PurchaseGoodsReceipt")]
    public async Task PurchaseOtherUsesLinkedItemCategories_NotTheLegacyLabel(string first, string second, string expected)
    {
        var company = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        store.Seed(nameof(db.PurchaseOrders), new PurchaseOrder
        {
            CompanyId = company, ExternalId = "PO-A",
            PurchaseOrderDetails = [
                new() { IsActive = true, Material = new() { Category = new() { Types = first } } },
                new() { IsActive = true, Material = new() { Category = new() { Types = second } } }
            ]
        });
        var result = await CreateReader(db, company).ReadAsync(company, [new(1, 9, "PO-A")], default);
        Assert.Equal(expected, result[1].BusinessType.Code);
        Assert.Equal("PO-A", result[1].PurchaseOrderCode);
        Assert.Equal(string.Empty, result[1].SupplierName);
    }

    [Fact]
    public async Task MixingUsesActualMovementDirection_AndIgnoresForeignOrMixedDirections()
    {
        var company = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        WarehouseShelfLedger Movement(long voucher, decimal delta, Guid? owner = null) => new()
        {
            VoucherId = voucher, CompanyId = owner ?? company, DeltaKg = delta,
            Shelf = new() { CompanyId = owner ?? company, SlotCode = "CT.0.1" }
        };
        store.Seed(nameof(db.WarehouseShelfLedgers), Movement(1, 7), Movement(2, -7),
            Movement(3, 7), Movement(3, -7), Movement(4, -7, Guid.NewGuid()), Movement(5, -7));
        store.Seed(nameof(db.OperationMaterialBuffers), new OperationMaterialBuffer { CompanyId = company, VoucherId = 5 });
        var result = await CreateReader(db, company).ReadAsync(company,
            [new(1, 9, ""), new(2, 10, ""), new(3, 10, ""), new(4, 10, ""), new(5, 6, "")], default);
        Assert.Equal("MixingReceipt", result[1].BusinessType.Code);
        Assert.Equal("MixingIssue", result[2].BusinessType.Code);
        Assert.Equal("ExportOther", result[3].BusinessType.Code);
        Assert.Equal("ExportOther", result[4].BusinessType.Code);
        Assert.Equal("MixingProductionIssue", result[5].BusinessType.Code);
    }

    [Fact]
    public async Task ProductionReceiptRequiresCompanyScopedOutputLink()
    {
        var company = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        store.Seed(nameof(db.ProductionOutputReceiptLedgers), new ProductionOutputReceiptLedger
        {
            CompanyId = company, VoucherId = 1, ProductionOutput = new() { CompanyId = company }
        }, new ProductionOutputReceiptLedger
        {
            CompanyId = company, VoucherId = 2, ProductionOutput = new() { CompanyId = Guid.NewGuid() }
        });
        var result = await CreateReader(db, company).ReadAsync(company, [new(1, 1, ""), new(2, 1, "")], default);
        Assert.Equal("ProductionReceipt", result[1].BusinessType.Code);
        Assert.Equal("ImportFinishedGood", result[2].BusinessType.Code);
    }

    [Fact]
    public async Task MixingLedgerCanLinkThroughVoucherDetail_WhenHeaderIdIsMissing()
    {
        var company = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        store.Seed(nameof(db.WarehouseVoucherDetails), new WarehouseVoucherDetail { VoucherId = 1, VoucherDetailId = 11 });
        store.Seed(nameof(db.WarehouseShelfLedgers), new WarehouseShelfLedger
        {
            CompanyId = company, VoucherDetailId = 11, DeltaKg = -5,
            Shelf = new() { CompanyId = company, SlotCode = "CT.0.1" }
        });
        var result = await CreateReader(db, company).ReadAsync(company, [new(1, 10, "")], default);
        Assert.Equal("MixingIssue", result[1].BusinessType.Code);
    }
    [Theory]
    [InlineData(ApplicationRoles.Production.PLPUUser, true)]
    [InlineData(ApplicationRoles.Developer, false)]
    [InlineData(ApplicationRoles.President, false)]
    [InlineData(ApplicationRoles.Admin, false)]
    [InlineData(ApplicationRoles.Lab.LabUser, false)]
    [InlineData(ApplicationRoles.Warehouse.KHOUser, false)]
    [InlineData(ApplicationRoles.Accounting.ACCUser, false)]
    [InlineData(ApplicationRoles.Sales.SaleUser, false)]
    public async Task SupplierFieldsRequirePlpuRole_EvenWhenOtherRolesHaveTheClaim(string role, bool expected)
    {
        var company = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var user = new User { CompanyId = company, Roles = [role], HasExplicitPermissionSet = true,
            Permissions = [ApplicationPermissions.Warehouse.ViewVoucherSuppliers] };
        store.Seed(nameof(db.PurchaseOrders), new PurchaseOrder { CompanyId = company, ExternalId = "PO-A", Comment = "supplier note",
            PurchaseOrderSnapshot = new() { SupplierNameSnapshot = "Supplier A", SupplierExternalIdSnapshot = "SUP-A" } });
        var result = await new WarehouseVoucherBusinessReader(db, user, new CurrentUserPermissionService(user))
            .ReadAsync(company, [new(1, 9, "PO-A")], default);
        Assert.Equal("PO-A", result[1].PurchaseOrderCode);
        Assert.Equal("PurchaseGoodsReceipt", result[1].BusinessType.Code);
        Assert.Equal(expected, result[1].CanViewSupplier);
        Assert.Equal(expected ? "Supplier A" : null, result[1].SupplierName);
        Assert.Equal(expected ? "SUP-A" : null, result[1].SupplierExternalId);
        Assert.Equal(expected ? "supplier note" : null, result[1].Comments);
        if (expected) Assert.True(WarehouseVoucherAccessRules.CanRead(user));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeniedSupplierSnapshotIsNotRead(bool authenticated)
    {
        var company = Guid.NewGuid();
        var (db, store) = ProductionOrderTestStore.CreateContext<IWarehouseReadDbContext>();
        var user = new User { CompanyId = company, IsAuthenticated = authenticated, HasExplicitPermissionSet = true };
        store.Seed<PurchaseOrder>(nameof(db.PurchaseOrders), new SensitivePurchaseOrder { CompanyId = company, ExternalId = "PO-A" });
        var result = await new WarehouseVoucherBusinessReader(db, user, new CurrentUserPermissionService(user))
            .ReadAsync(company, [new(1, 9, "PO-A")], default);
        Assert.False(result[1].CanViewSupplier);
        Assert.Null(result[1].SupplierName);
        Assert.Null(result[1].SupplierExternalId);
        Assert.Null(result[1].Comments);
    }

    [Theory]
    [InlineData(1, "success")]
    [InlineData(2, "warning")]
    [InlineData(3, "danger")]
    [InlineData(99, "muted")]
    public void BusinessBadgeToneIsResolvedByBackend(int type, string tone)
        => Assert.Equal(tone, WarehouseVoucherBusinessTypes.Fallback(type).Tone);

    private sealed class SensitivePurchaseOrder : PurchaseOrder
    {
        public override PurchaseOrderSnapshot? PurchaseOrderSnapshot
        {
            get => throw new InvalidOperationException("Unauthorized supplier snapshot was read.");
            set => throw new NotSupportedException();
        }
    }

    private static WarehouseVoucherBusinessReader CreateReader(IWarehouseReadDbContext db, Guid company)
    {
        var user = new User { CompanyId = company };
        return new(db, user, new CurrentUserPermissionService(user));
    }

    private sealed class User : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId => Guid.Empty;
        public Guid? EmployeeId => null;
        public Guid? CompanyId { get; init; }
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [ApplicationRoles.Production.PLPUUser];
        public IReadOnlyCollection<string> Permissions { get; init; } = [];
        public bool HasExplicitPermissionSet { get; init; }
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

}
