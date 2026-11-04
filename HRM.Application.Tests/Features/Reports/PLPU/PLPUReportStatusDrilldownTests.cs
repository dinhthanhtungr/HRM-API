using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Reports.PLPU;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUDrilldown;

namespace HRM.Application.Tests.Features.Reports.PLPU;

public sealed class PLPUReportStatusDrilldownTests
{
    public static IEnumerable<object[]> Codes => PLPUReportStatuses.Labels.Keys.Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Codes))]
    public async Task EveryPrimaryStatusMatchesBodyAcrossPages(string code)
    {
        var service = new PLPUPurchaseOverviewReportService(new StatusRepository());
        var query = new PLPUPurchaseOverviewQuery
        {
            CompanyId = PLPUReportParityTests.FixtureRepository.CompanyId,
            From = new(2026, 10, 1), To = new(2026, 10, 31), PageSize = 1
        };
        var body = await service.GetHeaderReportAsync(query);
        var group = Assert.Single(body.StatusDistribution, g => g.Label == PLPUReportStatuses.Labels[code]);
        Assert.Equal(2, group.PurchaseOrderCount);
        var ids = new HashSet<Guid>(); decimal contributions = 0;
        for (var page = 1; page <= group.PurchaseOrderCount; page++)
        {
            query.PageNumber = page;
            var drawer = await service.GetDrilldownAsync(query, "totalPo", reportStatus: code);
            Assert.Equal(group.PurchaseOrderCount, drawer.TotalCount);
            Assert.Equal(group.PurchaseOrderCount, drawer.MetricValue);
            var row = Assert.Single(drawer.Items);
            Assert.Equal(code, row.ReportStatusCode);
            Assert.Equal(group.Label, row.Order.ReportStatus);
            Assert.True(ids.Add(row.Order.PurchaseOrderId));
            contributions += drawer.PageValue;
        }
        Assert.Equal(group.PurchaseOrderCount, contributions);
        query.PageNumber = 3;
        Assert.Empty((await service.GetDrilldownAsync(query, "totalPo", reportStatus: code)).Items);
    }

    [Fact]
    public async Task StatusIsPrimaryGroup_NotOverlappingFlags()
    {
        var service = new PLPUPurchaseOverviewReportService(new StatusRepository());
        var q = new PLPUPurchaseOverviewQuery { CompanyId = PLPUReportParityTests.FixtureRepository.CompanyId, PageSize = 100 };
        var failedFlags = await service.GetDrilldownAsync(q, "qcFailPo");
        var failedGroup = await service.GetDrilldownAsync(q, "totalPo", reportStatus: "HasQCFail");
        Assert.True(failedFlags.TotalCount > failedGroup.TotalCount);
        var overdueFlags = await service.GetDrilldownAsync(q, "overduePo");
        var overdueGroup = await service.GetDrilldownAsync(q, "totalPo", reportStatus: "OverdueWithShortage");
        Assert.True(overdueFlags.TotalCount > overdueGroup.TotalCount);
        Assert.All(failedGroup.Items, i => Assert.Equal("HasQCFail", i.ReportStatusCode));
        Assert.All(overdueGroup.Items, i => Assert.Equal("OverdueWithShortage", i.ReportStatusCode));
    }

    [Fact]
    public async Task PersistedStatusAndCombinedFiltersRemainSeparate()
    {
        var service = new PLPUPurchaseOverviewReportService(new StatusRepository());
        var q = new PLPUPurchaseOverviewQuery
        {
            CompanyId = PLPUReportParityTests.FixtureRepository.CompanyId,
            From = new(2026, 10, 1), To = new(2026, 10, 31), Status = "Approved", OrderType = "NCC",
            SupplierId = StatusRepository.SupplierId, MaterialId = StatusRepository.MaterialId, Keyword = "NVL_TEST"
        };
        var body = await service.GetHeaderReportAsync(q);
        var completed = await service.GetDrilldownAsync(q, "totalPo", reportStatus: "Completed");
        Assert.Equal(Assert.Single(body.StatusDistribution, g => g.Label == "Hoàn thành").PurchaseOrderCount, completed.TotalCount);
        Assert.All(completed.Items, i => Assert.Equal("Approved", i.Order.POStatus));
        q.Status = "Closed";
        var empty = await service.GetDrilldownAsync(q, "totalPo", reportStatus: "Completed");
        Assert.Empty(empty.Items); Assert.Equal(0, empty.MetricValue); Assert.Equal(0, empty.TotalCount);
        Assert.DoesNotContain((await service.GetHeaderReportAsync(q)).StatusDistribution, g => g.Label == "Hoàn thành");
    }

    [Theory]
    [InlineData("totalPo", "")]
    [InlineData("totalPo", "completed")]
    [InlineData("totalPo", "Hoàn thành")]
    [InlineData("totalPo", "AcceptedEnough")]
    [InlineData("qcFailPo", "HasQCFail")]
    [InlineData("acceptedQuantity", "Completed")]
    public async Task InvalidOrUnsupportedFilterFailsBeforeReading(string metric, string status)
    {
        var repo = new PLPUReportParityTests.FixtureRepository();
        var user = new PLPUReportParityTests.ReportUser();
        var handler = new GetPLPUDrilldownQueryHandler(new(repo), user, new CurrentUserPermissionService(user));
        var result = await handler.Handle(new() { Metric = metric, ReportStatus = status }, default);
        Assert.False(result.Success); Assert.Equal(0, repo.Calls);
    }

    [Fact]
    public async Task ReportStatusCannotBypassCompanyOrPermission()
    {
        var repo = new PLPUReportParityTests.FixtureRepository();
        foreach (var role in new[] { "Admin", "Purchaser", "SaleUser", "QCUser" })
        {
            // Even a stale/granted read claim cannot bypass the requested role restriction.
            var user = new PLPUReportParityTests.ReportUser { Roles = [role], HasExplicitPermissionSet = true,
                Permissions = [ApplicationPermissions.Reports.ViewPlpu, ApplicationPermissions.Purchasing.ManagePurchaseOrders] };
            var handler = new GetPLPUDrilldownQueryHandler(new(repo), user, new CurrentUserPermissionService(user));
            Assert.False((await handler.Handle(new() { Metric = "totalPo", ReportStatus = "Completed" }, default)).Success);
        }
        var allowed = new PLPUReportParityTests.ReportUser();
        var scopedHandler = new GetPLPUDrilldownQueryHandler(new(repo), allowed, new CurrentUserPermissionService(allowed));
        Assert.False((await scopedHandler.Handle(new() { Metric = "totalPo", ReportStatus = "Completed", CompanyId = Guid.NewGuid() }, default)).Success);
        Assert.Equal(0, repo.Calls);
    }

    private sealed class StatusRepository : IPLPUPurchaseOverviewReportRepository
    {
        public static readonly Guid SupplierId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        public static readonly Guid MaterialId = Guid.Parse("50000000-0000-0000-0000-000000000001");
        public Task<PLPUPurchaseReportRawData> GetRawDataAsync(PLPUPurchaseOverviewQuery q, CancellationToken ct)
        {
            var data = new PLPUPurchaseReportRawData(); var n = 0;
            foreach (var code in PLPUReportStatuses.Labels.Keys)
            for (var copy = 0; copy < 2; copy++)
            {
                var id = Guid.Parse($"00000000-0000-0000-0000-{++n:000000000000}");
                var line = new PLPUPurchaseOrderLineRaw
                {
                    PurchaseOrderId = id, PurchaseOrderDetailId = id, POExternalId = $"DDH-{n}", CompanyId = PLPUReportParityTests.FixtureRepository.CompanyId,
                    POStatus = code == "Cancelled" ? "Cancelled" : code.StartsWith("Closed") ? "Closed" : "Approved",
                    POIsActive = true, OrderedQuantity = code == "NoQuantity" ? 0 : 100,
                    MaterialId = MaterialId, MaterialCode = "NVL_TEST", SupplierId = SupplierId,
                    CreatedDate = new(2026, 10, 5), OrderType = "NCC", LineNo = 1,
                    HeaderRequestDeliveryDate = DateTime.Today.AddDays(code is "Completed" or "OverdueWithShortage" ? -10 : 10)
                };
                if (q.CompanyId.HasValue && q.CompanyId != line.CompanyId || q.Status != null && q.Status != line.POStatus
                    || q.OrderType != null && q.OrderType != line.OrderType || q.SupplierId.HasValue && q.SupplierId != line.SupplierId
                    || q.MaterialId.HasValue && q.MaterialId != line.MaterialId || q.Keyword != null && !line.MaterialCode.Contains(q.Keyword)
                    || q.From.HasValue && line.CreatedDate < q.From.Value.Date || q.To.HasValue && line.CreatedDate >= q.To.Value.Date.AddDays(1)) continue;
                data.OrderLines.Add(line);
                var qty = code switch
                {
                    "Completed" => 150m, "Closed" or "DeliveredEnough" or "DeliveredEnoughWaitingQC" => 100m,
                    "OverdueWithShortage" or "HasQCFail" or "WaitingQC" or "PartiallyAccepted" or "PartiallyDelivered" => 50m,
                    _ => 0m
                };
                if (qty == 0) continue;
                data.Receipts.Add(new()
                {
                    PurchaseOrderId = id, POExternalId = line.POExternalId, ProductCode = line.MaterialCode,
                    WarehouseVoucherDetailId = n, QtyKg = qty, ReceiptDate = DateTime.Today.AddDays(-1),
                    AcceptedQty = code is "Completed" or "Closed" ? 100 : code == "PartiallyAccepted" ? 50 : 0,
                    PendingQcQty = code == "DeliveredEnoughWaitingQC" ? 100 : code == "WaitingQC" ? 50 : 0,
                    RejectedQty = code is "Completed" or "OverdueWithShortage" or "HasQCFail" ? 50 : 0
                });
            }
            return Task.FromResult(data);
        }
    }
}
