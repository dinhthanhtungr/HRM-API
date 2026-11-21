using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Reports.CompanyOverview;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Infrastructure.Services.Reports;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HRM.Application.Tests.Features.Reports;

public sealed class CompanyOverviewTests
{
    [Theory]
    [InlineData("President", true)]
    [InlineData("Developer", true)]
    [InlineData("PLPUUser", false)]
    [InlineData("ACCUser", false)]
    [InlineData("SaleUser", false)]
    [InlineData("Admin", false)]
    public void Access_RequiresExecutiveRoleAndPurchaseCapability(string role, bool allowed)
    {
        var user = new User { Roles = [role] };
        var permissions = new CurrentUserPermissionService(user);
        Assert.Equal(allowed, CompanyOverviewAccess.CanRead(user, permissions, user.CompanyId));
        Assert.False(CompanyOverviewAccess.CanRead(user, permissions, Guid.NewGuid()));
    }

    [Fact]
    public void ExplicitMissingPermissionAndUnauthenticatedUser_FailClosed()
    {
        var explicitUser = new User { Roles = ["President"], HasExplicitPermissionSet = true };
        Assert.False(CompanyOverviewAccess.CanRead(explicitUser, new CurrentUserPermissionService(explicitUser), null));
        var anonymous = new User { Roles = ["President"], IsAuthenticated = false };
        Assert.False(CompanyOverviewAccess.CanRead(anonymous, new CurrentUserPermissionService(anonymous), null));
        var noCompany = new User { Roles = ["President"], CompanyId = null };
        Assert.False(CompanyOverviewAccess.CanRead(noCompany, new CurrentUserPermissionService(noCompany), null));
    }

    [Fact]
    public void Purchases_KeepPartialMoneyUnitsAndNonSupplyGroupsSeparate()
    {
        var po = Guid.NewGuid();
        var facts = new CompanyPurchaseFact[]
        {
            new() { Source = "PurchaseOrder", Kind = "Supplies", PurchaseOrderId = po, Amount = 1000 },
            new() { Source = "MRO", Kind = "Supplies", DocumentKey = "1", Quantity = 2, Unit = "Cái", Amount = 200 },
            new() { Source = "MRO", Kind = "Supplies", DocumentKey = "1", Quantity = 3, Unit = "Bộ", Amount = null, Issue = "MissingPrice" },
            new() { Source = "Warehouse", Kind = "Supplies", DocumentKey = "2", Quantity = 9, Unit = "Cái", Amount = null, Issue = "PossibleDuplicateSource", IncludeQuantity = false },
            new() { Source = "MRO", Kind = "Services", Quantity = 1, Unit = "Lần", Amount = 500 },
            new() { Source = "MRO", Kind = "Supplies", DocumentKey = "3", Quantity = 1, Unit = "Cái", Amount = 0, Issue = "ZeroPrice" }
        };
        var report = CompanyPurchaseCalculator.Build(facts);
        var supplies = Assert.Single(report.Groups, x => x.Kind == "Supplies");
        Assert.Equal(1000m, supplies.KnownOrderedAmount);
        Assert.Equal(200m, supplies.KnownReceivedAmount);
        Assert.False(supplies.IsReceivedAmountComplete);
        Assert.Equal(2, supplies.ReceivedAmountIssueCount);
        Assert.Equal(3, supplies.ReceiptCount);
        Assert.Equal(3m, Assert.Single(supplies.ReceivedQuantities, x => x.Unit == "Cái").Quantity);
        Assert.Equal(3m, Assert.Single(supplies.ReceivedQuantities, x => x.Unit == "Bộ").Quantity);
        Assert.Equal(500m, Assert.Single(report.Groups, x => x.Kind == "Services").KnownReceivedAmount);
        Assert.Equal(3, report.IssueCount);
    }

    [Fact]
    public void SupplyOrders_UseViewAmountsWithoutChangingReceiptsOrOtherPurchases()
    {
        var po = Guid.NewGuid();
        var receipt = new CompanyPurchaseFact { Source = "MRO", Kind = "Supplies", PurchaseOrderId = po, ItemCode = "VTSX_A", Amount = null, Issue = "UnitMismatch" };
        var service = new CompanyPurchaseFact { Source = "PurchaseOrder", Kind = "Services", Amount = 50 };
        CompanyPurchaseFact[] existing = [
            new() { Source = "PurchaseOrder", Kind = "Supplies", PurchaseOrderId = po, ItemCode = "VTSX_A", Amount = 100 },
            new() { Source = "PurchaseOrder", Kind = "Unclassified", PurchaseOrderId = po, ItemCode = "vtsx_b", Amount = 20 },
            receipt, service
        ];
        CompanyPurchaseFact[] fromView = [
            new() { Source = "PurchaseOrder", Kind = "Supplies", PurchaseOrderId = po, ItemCode = "VTSX_A", Amount = 120 },
            new() { Source = "PurchaseOrder", Kind = "Supplies", PurchaseOrderId = po, ItemCode = "VTSX_B", Amount = 30 }
        ];
        var merged = CompanyPurchaseCalculator.ReplaceSupplyOrders(existing, fromView);
        Assert.Contains(receipt, merged);
        Assert.Contains(service, merged);
        var report = CompanyPurchaseCalculator.Build(merged);
        var supplies = Assert.Single(report.Groups, x => x.Kind == "Supplies");
        Assert.Equal(150m, supplies.KnownOrderedAmount);
        Assert.Equal(1, supplies.OrderCount);
        Assert.True(supplies.IsOrderedAmountComplete);
        Assert.Equal(1, supplies.ReceivedAmountIssueCount);
        Assert.Equal(50m, Assert.Single(report.Groups, x => x.Kind == "Services").KnownOrderedAmount);
        Assert.Equal(0m, Assert.Single(report.Groups, x => x.Kind == "Unclassified").KnownOrderedAmount);
        Assert.Equal("purchase.v_po_ap_invoice_item_reconcile.po_amount_excl_vat", supplies.OrderedAmountSource);
    }

    [Fact]
    public void SupplyOrders_NoViewRows_DoNotFallbackToOldLineAmounts()
    {
        CompanyPurchaseFact[] existing = [new() { Source = "PurchaseOrder", Kind = "Supplies", Amount = 500 }];
        var merged = CompanyPurchaseCalculator.ReplaceSupplyOrders(existing, []);
        Assert.Empty(merged);
        Assert.Equal(0m, CompanyPurchaseCalculator.Build(merged).Groups[0].KnownOrderedAmount);
    }

    [Fact]
    public void SupplyOrderView_IsParameterizedAndDoesNotRequireInvoicesOrRepriceQuantities()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var sql = db.Database.SqlQueryRaw<CompanyPurchaseFact>(CompanySupplyPurchaseSql.Facts,
            new NpgsqlParameter("company", Guid.NewGuid()),
            new NpgsqlParameter("from", new DateTime(2026, 1, 1)),
            new NpgsqlParameter("until", new DateTime(2026, 2, 1))).ToQueryString();
        Assert.Contains("purchase.v_po_ap_invoice_item_reconcile", sql);
        Assert.Contains("po_amount_excl_vat", sql);
        Assert.Contains("@company", sql);
        Assert.Contains("@from", sql);
        Assert.Contains("@until", sql);
        Assert.DoesNotContain("invoice_count", sql);
        Assert.DoesNotContain("ap_amount_excl_vat", sql);
        Assert.DoesNotContain("UnitPriceAgreed", sql);
    }

    [Fact]
    public void WarningPreview_IsBoundedButTotalsAndIssueCountRemainComplete()
    {
        var facts = Enumerable.Range(0, 150).Select(i => new CompanyPurchaseFact
        {
            Source = "MRO", Kind = "Supplies", Quantity = 1, Unit = "Cái",
            Issue = "MissingPrice", Date = new DateTime(2026, 1, 1).AddDays(i)
        }).ToArray();
        var report = CompanyPurchaseCalculator.Build(facts);
        Assert.Equal(150, report.IssueCount);
        Assert.Equal(100, report.Issues.Count);
        Assert.Equal(facts.Last().Date, report.Issues[0].Date);
        Assert.Equal(150, report.Groups[0].ReceivedAmountIssueCount);
    }

    [Fact]
    public void UnmappedPurchaseFacts_AreTranslatableWithoutSchemaChanges()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var query = db.Database.SqlQueryRaw<CompanyPurchaseFact>(CompanyPurchaseSql.Facts,
            new NpgsqlParameter("company", Guid.NewGuid()),
            new NpgsqlParameter("from", new DateTime(2026, 1, 1)),
            new NpgsqlParameter("until", new DateTime(2026, 2, 1)));
        var sql = query.ToQueryString();
        Assert.Contains("@company", sql);
        Assert.Contains("CONFIRMED", sql);
        Assert.Contains("PO_ALLOC", sql);
        Assert.Contains("APPLIED", sql);
        Assert.DoesNotContain("INSERT INTO", sql);
    }

    private sealed class User : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId => null;
        public Guid? CompanyId { get; init; } = Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = [];
        public IReadOnlyCollection<string> Permissions => [];
        public bool HasExplicitPermissionSet { get; init; }
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }
}
