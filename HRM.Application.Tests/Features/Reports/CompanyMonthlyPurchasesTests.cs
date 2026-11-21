using HRM.Application.Features.Reports.CompanyOverview;
using HRM.Application.Features.Reports.PLPU;

namespace HRM.Application.Tests.Features.Reports;

public sealed class CompanyMonthlyPurchasesTests
{
    [Fact]
    public async Task SharedRawRead_KeepsSelectedSummaryEquivalentToExistingService()
    {
        var repo = new FilteredRepository();
        var service = new PLPUPurchaseOverviewReportService(repo);
        var from = new DateTime(2026, 7, 1);
        var to = new DateTime(2026, 9, 11);
        var expected = await service.GetSummaryAsync(new() { CompanyId = PLPU.PLPUReportParityTests.FixtureRepository.CompanyId, From = from, To = to });
        repo.Calls = 0;
        var actual = await service.GetSummaryWithMonthlyAsync(PLPU.PLPUReportParityTests.FixtureRepository.CompanyId,
            from, to, new DateTime(2026, 4, 1), default);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(expected), System.Text.Json.JsonSerializer.Serialize(actual.Summary));
        Assert.Equal(1, repo.Calls);
        Assert.All(actual.Monthly, point => Assert.True(point.PeriodStart >= new DateTime(2026, 4, 1)));
    }

    private sealed class FilteredRepository : IPLPUPurchaseOverviewReportRepository
    {
        public int Calls;
        public async Task<PLPUPurchaseReportRawData> GetRawDataAsync(PLPUPurchaseOverviewQuery query, CancellationToken ct)
        {
            Calls++;
            var raw = await new PLPU.PLPUReportParityTests.FixtureRepository().GetRawDataAsync(query, ct);
            raw.OrderLines.RemoveAll(x => x.CreatedDate < query.From || x.CreatedDate >= query.To!.Value.AddDays(1));
            var ids = raw.OrderLines.Select(x => x.PurchaseOrderId).ToHashSet();
            raw.Receipts.RemoveAll(x => !ids.Contains(x.PurchaseOrderId));
            return raw;
        }
    }

    [Fact]
    public void MonthlySeries_PreservesZeroAndIncompleteValues_ExcludesReceiptsOtherKindsAndFutureDates()
    {
        var from = new DateTime(2026, 10, 1);
        var to = new DateTime(2026, 11, 11);
        var facts = new[] {
            Fact("PurchaseOrder", "Supplies", to, 50),
            Fact("PurchaseOrder", "Supplies", to, null),
            Fact("Receipt", "Supplies", to, 900),
            Fact("PurchaseOrder", "Services", to, 800),
            Fact("PurchaseOrder", "Supplies", to.AddDays(1), 700),
            Fact("PurchaseOrder", "Supplies", from.AddDays(-1), 600),
        };
        var result = CompanyPurchaseCalculator.BuildMonthly(from, to,
            [new() { PeriodStart = new DateTime(2026, 11, 1), ReceivedPurchaseValue = 100 }], facts);
        Assert.Equal(2, result.Count);
        Assert.Equal(0, result[0].KnownSuppliesOrderedValue);
        Assert.Equal(0, result[0].SuppliesAmountIssueCount);
        Assert.Equal(50, result[1].KnownSuppliesOrderedValue);
        Assert.Equal(1, result[1].SuppliesAmountIssueCount);
        Assert.Equal(100, result[1].RawMaterialsReceivedValue);
    }

    private static CompanyPurchaseFact Fact(string source, string kind, DateTime date, decimal? value)
        => new() { Source = source, Kind = kind, Date = date, Amount = value };
}
