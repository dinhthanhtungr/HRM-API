using System.Text.Json;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Reports.PLPU;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUHeader;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUBody;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUOrders;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUOrderDetail;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;
using LegacyService = HRM.Application.Tests.Features.Reports.PLPU.Reference.PLPUPurchaseOverviewReportService;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.Purchasing;
using HRM.Application.Abstractions.Persistence.Reports;
using HRM.Application.Abstractions.Persistence.PLM.Manufacturing;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HRM.Application.Tests.Features.Reports.PLPU;

public sealed class PLPUReportParityTests
{
    [Theory]
    [InlineData(false, 1, 15)]
    [InlineData(true, 1, 3)]
    [InlineData(false, 2, 4)]
    [InlineData(false, 0, 0)]
    [InlineData(false, 1, 150)]
    public async Task EveryPurchaseResponseMatchesFrozenSource(bool onlyAction, int page, int size)
    {
        var repo = new FixtureRepository();
        var legacy = new LegacyService(repo);
        var port = new PLPUPurchaseOverviewReportService(repo);
        var query = new PLPUPurchaseOverviewQuery { OnlyNeedAction = onlyAction, PageNumber = page, PageSize = size };
        Equal(await legacy.GetOverviewAsync(query), await port.GetOverviewAsync(query));
        var dashboard = await legacy.GetHeaderReportAsync(query);
        Equal(dashboard, await port.GetHeaderReportAsync(query));
        Equal<PLPUPurchaseOverviewSummaryDto>(dashboard, await port.GetSummaryAsync(query));
        Equal(await legacy.GetRowsAsync(query), await port.GetRowsAsync(query));
        Equal(await legacy.GetDetailLinesAsync(query), await port.GetDetailLinesAsync(query));
        Equal(await legacy.GetReceiptsAsync(query), await port.GetReceiptsAsync(query));
        Equal(await legacy.GetQcAsync(query), await port.GetQcAsync(query));
        Equal(await legacy.GetSupplierPerformanceAsync(query), await port.GetSupplierPerformanceAsync(query));
        Equal(await legacy.GetOrderDetailAsync(FixtureRepository.OrderId), await port.GetOrderDetailAsync(FixtureRepository.OrderId, FixtureRepository.CompanyId));
    }

    [Fact]
    public async Task EmptyDataMatchesSourceIncludingDetailAndZeroSummary()
    {
        var repo = new FixtureRepository(empty: true);
        var legacy = new LegacyService(repo);
        var port = new PLPUPurchaseOverviewReportService(repo);
        Equal(await legacy.GetHeaderReportAsync(new()), await port.GetHeaderReportAsync(new()));
        Equal(await legacy.GetOrderDetailAsync(Guid.NewGuid()), await port.GetOrderDetailAsync(Guid.NewGuid(), FixtureRepository.CompanyId));
        Assert.Equal(0, (await port.GetSummaryAsync(new(), default)).TotalPO);
    }

    [Theory]
    [InlineData("Purchaser", false)]
    [InlineData("PLPUUser", true)]
    [InlineData("President", true)]
    [InlineData("Developer", true)]
    [InlineData("Admin", false)]
    [InlineData("SaleUser", false)]
    [InlineData("QCUser", false)]
    public async Task CapabilityAndCompanyAreCheckedBeforeReading(string role, bool allowed)
    {
        var user = new ReportUser { Roles = [role] };
        var repo = new FixtureRepository();
        var permissions = new CurrentUserPermissionService(user);
        var handler = new GetPLPUHeaderQueryHandler(new(repo), user, permissions, NewAccountingReader());
        var result = await handler.Handle(new(), default);
        Assert.Equal(allowed, result.Success);
        Assert.Equal(allowed ? 1 : 0, repo.Calls);
        repo.Calls = 0;
        Assert.False((await handler.Handle(new() { CompanyId = Guid.NewGuid() }, default)).Success);
        Assert.Equal(0, repo.Calls);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task MissingIdentityCompanyOrExplicitPermissionFailsClosed(bool authenticated, bool explicitSet, bool noCompany)
    {
        var user = new ReportUser { IsAuthenticated = authenticated, HasExplicitPermissionSet = explicitSet,
            CompanyId = noCompany ? null : FixtureRepository.CompanyId };
        var repo = new FixtureRepository();
        var handler = new GetPLPUHeaderQueryHandler(new(repo), user, new CurrentUserPermissionService(user), NewAccountingReader());
        Assert.False((await handler.Handle(new(), default)).Success);
        Assert.Equal(0, repo.Calls);
    }

    [Fact]
    public async Task SplitHeaderAndBodyPreserveSourceAndApplyCurrentCompany()
    {
        var repo = new FixtureRepository();
        var user = new ReportUser();
        var permissions = new CurrentUserPermissionService(user);
        var port = new PLPUPurchaseOverviewReportService(repo);
        var original = await new LegacyService(repo).GetHeaderReportAsync(new());
        var header = await new GetPLPUHeaderQueryHandler(port, user, permissions, NewAccountingReader()).Handle(new(), default);
        Assert.NotNull(header.Data!.Accounting);
        original.Accounting = header.Data.Accounting;
        Equal<PLPUPurchaseOverviewSummaryDto>(original, header.Data!);
        var body = await new GetPLPUBodyQueryHandler(port, user, permissions, new PLPUMonthChartTests.Clock()).Handle(new(), default);
        Assert.Equal(6, body.Data!.PurchaseValueByMonth.Count);
        Assert.Equal(6, body.Data.QuantityByMonth.Count);
        foreach (var property in typeof(PLPUPurchaseBodyDto).GetProperties()
            .Where(x => x.Name is not "PurchaseValueByMonth" and not "QuantityByMonth"))
            Assert.Equal(JsonSerializer.Serialize(original.GetType().GetProperty(property.Name)!.GetValue(original)),
                JsonSerializer.Serialize(property.GetValue(body.Data)));
        Assert.Equal(user.CompanyId, repo.LastQuery!.CompanyId);
        Assert.DoesNotContain("purchaseValueByMonth", JsonSerializer.Serialize(header.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var detail = await new GetPLPUOrderDetailQueryHandler(port, user, permissions)
            .Handle(new() { Id = FixtureRepository.OrderId }, default);
        Assert.True(detail.Success);
        Assert.Equal(user.CompanyId, repo.LastQuery!.CompanyId);
        Assert.Equal(FixtureRepository.OrderId, repo.LastQuery.PurchaseOrderId);
        Assert.True(repo.LastQuery.IncludeInactive);
    }

    [Fact]
    public async Task InvalidInputsAreRejectedBeforeQuery()
    {
        var repo = new FixtureRepository(); var user = new ReportUser();
        var handler = new GetPLPUOrdersQueryHandler(new(repo), user, new CurrentUserPermissionService(user));
        Assert.False((await handler.Handle(new() { From = new(2026, 10, 2), To = new(2026, 10, 1) }, default)).Success);
        Assert.False((await handler.Handle(new() { To = DateTime.MaxValue }, default)).Success);
        Assert.False((await handler.Handle(new() { MaterialId = Guid.Empty }, default)).Success);
        Assert.False((await handler.Handle(new() { PageNumber = int.MaxValue, PageSize = int.MaxValue }, default)).Success);
        Assert.Equal(0, repo.Calls);
    }

    [Fact]
    public async Task AllHandlersRejectMissingCapabilityBeforeReadingOrExporting()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        var user = new ReportUser { Roles = ["QCUser"] };
        var repo = new FixtureRepository();
        services.AddSingleton<ICurrentUser>(user);
        services.AddSingleton<HRM.Application.Abstractions.Commons.Time.IDateTimeProvider>(new PLPUMonthChartTests.Clock());
        services.AddSingleton<ICurrentUserPermissionService>(new CurrentUserPermissionService(user));
        services.AddSingleton<IPLPUPurchaseOverviewReportRepository>(repo);
        services.AddSingleton(ProductionOrderTestStore.CreateContext<IReportReadDbContext>().Db);
        services.AddSingleton(ProductionOrderTestStore.CreateContext<IPurchaseOrderDbContext>().Db);
        services.AddSingleton(ProductionOrderTestStore.CreateContext<IManufacturingDbContext>().Db);
        services.AddSingleton<IPLPUFinishReportExcelRenderer>(new ForbiddenRenderer());
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var queries = typeof(GetPLPUHeaderQuery).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("HRM.Application.Features.Reports.PLPU.Queries.", StringComparison.Ordinal) == true
                && t.Name.EndsWith("Query", StringComparison.Ordinal)).ToArray();
        Assert.Equal(11, queries.Length);
        foreach (var query in queries)
        {
            var result = await sender.Send(Activator.CreateInstance(query)!);
            Assert.NotNull(result);
            Assert.False((bool)result.GetType().GetProperty("Success")!.GetValue(result)!);
        }
        Assert.Equal(0, repo.Calls);
    }

    private sealed class ForbiddenRenderer : IPLPUFinishReportExcelRenderer
    {
        public byte[] ExportFinishReportExcel(List<FinishRow> rows) => throw new InvalidOperationException("Unauthorized export was attempted.");
    }

    [Fact]
    public async Task PlpuRoleAllowsPurchasingAlongsideExplicitReportReadPermission()
    {
        var user = new ReportUser { Roles = ["PLPUUser"], HasExplicitPermissionSet = true,
            Permissions = [ApplicationPermissions.Reports.ViewPlpu] };
        var permissions = new CurrentUserPermissionService(user);
        Assert.True(permissions.HasPermission(ApplicationPermissions.Purchasing.ManagePurchaseOrders));
        Assert.True(new CurrentUserPermissionService(new ReportUser { Roles = ["PLPUUser"] })
            .HasPermission(ApplicationPermissions.Purchasing.ManagePurchaseOrders));
        var repo = new FixtureRepository();
        var result = await new GetPLPUHeaderQueryHandler(new(repo), user, permissions, NewAccountingReader()).Handle(new(), default);
        Assert.True(result.Success);
        Assert.False(PLPUReportAccess.CanRead(user, permissions, Guid.NewGuid()));
        Assert.False(PLPUReportAccess.CanRead(new ReportUser { Roles = ["PLPUUser"], HasExplicitPermissionSet = true },
            new CurrentUserPermissionService(new ReportUser { Roles = ["PLPUUser"], HasExplicitPermissionSet = true }), null));
    }

    internal static PLPUAccountingHeaderReader NewAccountingReader() => new(
        ProductionOrderTestStore.CreateContext<IReportReadDbContext>().Db,
        ProductionOrderTestStore.CreateContext<IPurchaseOrderDbContext>().Db);

    internal static void Equal<T>(T expected, T actual) =>
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));

    internal sealed class ReportUser : ICurrentUser
    {
        public bool IsAuthenticated { get; init; } = true;
        public Guid UserId => Guid.Parse("10000000-0000-0000-0000-000000000001");
        public Guid? EmployeeId => UserId;
        public Guid? CompanyId { get; init; } = FixtureRepository.CompanyId;
        public string? UserName => "plpu-test";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles { get; init; } = ["PLPUUser"];
        public bool HasExplicitPermissionSet { get; init; }
        public IReadOnlyCollection<string> Permissions { get; init; } = [];
        public bool IsInRole(string role) => Roles.Contains(role);
    }

    internal sealed class FixtureRepository(bool empty = false) : IPLPUPurchaseOverviewReportRepository
    {
        public static readonly Guid CompanyId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        public static readonly Guid OrderId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        public int Calls { get; set; }
        public PLPUPurchaseOverviewQuery? LastQuery { get; private set; }
        public Task<PLPUPurchaseReportRawData> GetRawDataAsync(PLPUPurchaseOverviewQuery query, CancellationToken ct)
        {
            Calls++; LastQuery = query;
            var data = new PLPUPurchaseReportRawData();
            if (empty) return Task.FromResult(data);
            // Duplicate material lines, partial receipts, 98/100/102% boundaries,
            // packaging auto acceptance, inactive/cancelled/closed and missing prices.
            var amounts = new[] { 0m, 50m, 98m, 100m, 102m, 103m, 150m };
            for (var i = 0; i < 28; i++)
            {
                var order = i < 2 ? OrderId : Id(i + 100);
                var line = new PLPUPurchaseOrderLineRaw
                {
                    PurchaseOrderId = order, PurchaseOrderDetailId = Id(i + 1), CompanyId = CompanyId,
                    POExternalId = i < 2 ? "NCC-SHARED" : $"NCC-{i}", OrderType = "NCC", LineNo = i < 2 ? i + 1 : 1,
                    SupplierId = Id(500 + i % 4), SupplierName = $"Supplier {i % 4}", MaterialId = Id(800 + i),
                    MaterialCode = i < 2 ? "NVL-SHARED" : $"NVL-{i}", MaterialName = $"Material {i}",
                    OrderedQuantity = i == 27 ? 0m : 100m, POIsActive = i != 25,
                    POStatus = i switch { 23 => "Closed", 24 => "Cancelled", _ => "Approved" },
                    CreatedDate = new DateTime(2026, i % 9 + 1, 1),
                    HeaderRequestDeliveryDate = DateTime.Today.AddDays(i % 3 == 0 ? 5 : -10),
                    DeliveryDate = i % 5 == 0 ? DateTime.Today.AddDays(-5) : null,
                    UnitPriceAgreed = i % 3 == 0 ? null : 15.123456m,
                    TotalPriceAgreed = i % 4 == 0 ? null : 1500m,
                    BaseCostSnapshot = i % 5 == 0 ? null : 12m,
                    CategoryId = i == 21 ? Guid.Parse("3bed94ed-da05-4e5f-ac04-c7647aaa63d6") : null
                };
                data.OrderLines.Add(line);
                var decision = (i % 4) switch { 0 => QcDecision.QCPass, 1 => QcDecision.Waiter, 2 => QcDecision.QCFail, _ => QcDecision.Special };
                var qty = amounts[i % amounts.Length];
                data.Receipts.Add(new()
                {
                    PurchaseOrderId = order, POExternalId = line.POExternalId, ProductCode = line.MaterialCode,
                    ProductName = line.MaterialName, SupplierId = line.SupplierId, SupplierName = line.SupplierName,
                    WarehouseVoucherId = i + 1, WarehouseVoucherDetailId = i + 1,
                    WarehouseRequestId = i + 1, WarehouseVoucherCode = $"IMPORT-{i}", LotNumber = $"LOT-{i}",
                    ReceiptDate = DateTime.Today.AddDays(-8 + i % 4), QCCreatedDate = DateTime.Today.AddDays(-1),
                    QtyKg = qty, QCResult = decision,
                    QCGroup = decision is QcDecision.QCPass or QcDecision.Special ? "Accepted" : decision == QcDecision.QCFail ? "Rejected" : "Pending",
                    AcceptedQty = decision is QcDecision.QCPass or QcDecision.Special ? qty : 0,
                    PendingQcQty = decision == QcDecision.Waiter ? qty : 0,
                    RejectedQty = decision == QcDecision.QCFail ? qty : 0,
                    VoucherDetailType = VoucherDetailType.Waiter
                });
            }
            return Task.FromResult(data);
        }
        private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:000000000000}");
    }
}
