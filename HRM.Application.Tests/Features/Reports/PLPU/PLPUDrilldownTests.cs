using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Reports.PLPU;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUDrilldown;

namespace HRM.Application.Tests.Features.Reports.PLPU;

public sealed class PLPUDrilldownTests
{
    public static TheoryData<string, string, string> Metrics => new()
    {
        { "totalPo", "TotalPO", "order" }, { "activePo", "ActivePO", "order" },
        { "cancelledPo", "CancelledPO", "order" }, { "closedPo", "ClosedPO", "order" },
        { "completedPo", "CompletedPO", "order" }, { "incompletePo", "IncompletePO", "order" },
        { "needActionPo", "NeedActionPO", "order" }, { "overduePo", "OverduePO", "order" },
        { "qcFailPo", "QCFailPO", "order" }, { "pendingQcPo", "PendingQCPO", "order" },
        { "overDeliveryPo", "OverDeliveryPO", "order" },
        { "orderedQuantity", "OrderedQuantity", "line" },
        { "warehouseReceivedQuantity", "WarehouseReceivedQuantity", "line" },
        { "acceptedQuantity", "AcceptedQuantity", "line" },
        { "pendingQcQuantity", "PendingQcQuantity", "line" },
        { "rejectedQuantity", "RejectedQuantity", "line" },
        { "totalPurchaseValue", "TotalPurchaseValue", "line" },
        { "receivedPurchaseValue", "ReceivedPurchaseValue", "line" }
    };

    [Theory]
    [MemberData(nameof(Metrics))]
    public async Task EveryMetricMatchesHeaderAndAllPages(string metric, string headerField, string rowType)
    {
        var repo = new PLPUReportParityTests.FixtureRepository();
        var service = new PLPUPurchaseOverviewReportService(repo);
        var summary = await service.GetSummaryAsync(new());
        var expected = headerField == "IncompletePO" ? summary.TotalPO - summary.CompletedPO
            : Convert.ToDecimal(typeof(PLPUPurchaseOverviewSummaryDto).GetProperty(headerField)!.GetValue(summary));
        var all = new List<PLPUDrilldownRowDto>();
        var page = 1;
        PLPUDrilldownDto result;
        do
        {
            result = await service.GetDrilldownAsync(new()
            {
                CompanyId = PLPUReportParityTests.FixtureRepository.CompanyId,
                PageNumber = page, PageSize = 3, OnlyNeedAction = true
            }, metric);
            Assert.Equal(expected, result.MetricValue);
            Assert.Equal(rowType, result.RowType);
            Assert.Equal(result.Items.Sum(r => r.Contribution), result.PageValue);
            Assert.All(result.Items, r =>
            {
                Assert.Equal(result.CompanyId, r.Order.CompanyId);
                Assert.Equal(rowType == "line", r.Line != null);
                if (r.Line != null) Assert.Equal(r.Order.PurchaseOrderId, r.Line.PurchaseOrderId);
                if (metric == "completedPo") Assert.Equal("Hoàn thành", r.Order.ReportStatus);
                if (metric == "overduePo") Assert.True(r.Order.IsOverdue);
                if (metric == "needActionPo") Assert.True(r.Order.NeedBuyerAction);
                if (metric == "acceptedQuantity") Assert.Equal(r.Line!.AcceptedQuantity, r.Contribution);
                if (metric == "pendingQcQuantity") Assert.Equal(r.Line!.PendingQcQuantity, r.Contribution);
                if (metric == "rejectedQuantity") Assert.Equal(r.Line!.RejectedQuantity, r.Contribution);
            });
            all.AddRange(result.Items);
            page++;
        } while (page <= result.TotalPages);
        Assert.Equal(result.TotalCount, all.Count);
        Assert.Equal(expected, all.Sum(r => r.Contribution));
        Assert.Equal(all.Count, all.Select(r => (r.Order.PurchaseOrderId, r.Line?.PurchaseOrderDetailId)).Distinct().Count());
    }

    [Fact]
    public async Task SharedOrderIsLocalizedOnceAndPastLastPageKeepsGlobalValue()
    {
        var repo = new PLPUReportParityTests.FixtureRepository();
        var service = new PLPUPurchaseOverviewReportService(repo);
        var query = new PLPUPurchaseOverviewQuery { CompanyId = PLPUReportParityTests.FixtureRepository.CompanyId, PageSize = 100 };
        var result = await service.GetDrilldownAsync(query, "orderedQuantity");
        var shared = result.Items.Where(r => r.Order.PurchaseOrderId == PLPUReportParityTests.FixtureRepository.OrderId).ToArray();
        Assert.Equal(2, shared.Length);
        Assert.Same(shared[0].Order, shared[1].Order);
        Assert.DoesNotContain("Overdue", shared[0].Order.AlertFlags);
        query.PageNumber = 1000;
        var emptyPage = await service.GetDrilldownAsync(query, "orderedQuantity");
        Assert.Empty(emptyPage.Items);
        Assert.Equal(result.MetricValue, emptyPage.MetricValue);
        Assert.Equal(result.TotalCount, emptyPage.TotalCount);
        Assert.Equal(0m, emptyPage.PageValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CompletedPo")]
    [InlineData("completionPercent")]
    public async Task UnsupportedMetricRejectedBeforeReading(string? metric)
    {
        var (handler, repo) = CreateHandler();
        Assert.False((await handler.Handle(new() { Metric = metric }, default)).Success);
        Assert.Equal(0, repo.Calls);
    }

    [Fact]
    public async Task HandlerScopesQueryAndRejectsCrossCompany()
    {
        var (handler, repo) = CreateHandler();
        Assert.False((await handler.Handle(new() { Metric = "totalPo", CompanyId = Guid.NewGuid() }, default)).Success);
        Assert.Equal(0, repo.Calls);
        var result = await handler.Handle(new() { Metric = "totalPo" }, default);
        Assert.True(result.Success);
        Assert.Equal(PLPUReportParityTests.FixtureRepository.CompanyId, repo.LastQuery!.CompanyId);
        Assert.Equal(result.Data!.TotalCount, result.Data.MetricValue);
    }

    [Fact]
    public async Task EmptyCohortReturnsZerosAndNormalizedPagination()
    {
        var service = new PLPUPurchaseOverviewReportService(new PLPUReportParityTests.FixtureRepository(empty: true));
        var data = await service.GetDrilldownAsync(new()
        {
            CompanyId = PLPUReportParityTests.FixtureRepository.CompanyId, PageNumber = 0, PageSize = 0
        }, "rejectedQuantity");
        Assert.Empty(data.Items);
        Assert.Equal(0, data.MetricValue);
        Assert.Equal(0, data.TotalCount);
        Assert.Equal(0, data.TotalPages);
        Assert.Equal(1, data.Page);
        Assert.Equal(15, data.PageSize);
    }

    private static (GetPLPUDrilldownQueryHandler Handler, PLPUReportParityTests.FixtureRepository Repo) CreateHandler()
    {
        var repo = new PLPUReportParityTests.FixtureRepository();
        var user = new PLPUReportParityTests.ReportUser();
        return (new(new(repo), user, new CurrentUserPermissionService(user)), repo);
    }
}
