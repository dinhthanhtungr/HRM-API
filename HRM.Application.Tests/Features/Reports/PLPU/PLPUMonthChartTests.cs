using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Features.Reports.PLPU;
using HRM.Application.Features.Reports.PLPU.Queries.GetPLPUBody;

namespace HRM.Application.Tests.Features.Reports.PLPU;

public sealed class PLPUMonthChartTests
{
    [Theory]
    [InlineData(2026, 10, 6, "2026-05")]
    [InlineData(2026, 1, 12, "2026-01")]
    [InlineData(2025, 11, 14, "2025-11")]
    public void Window_ReturnsAtLeastSixMonths_OrEntireSelectedRange(int year, int month, int count, string first)
    {
        var query = new PLPUPurchaseOverviewQuery { From = new(year, month, 15), To = new(2026, month == 10 ? 10 : 12, 20) };
        var window = PLPUMonthChartWindow.Resolve(query, new(2026, 10, 14));
        var points = PLPUMonthChartWindow.Fill([], window.From, window.To);
        Assert.Equal(count, points.Count);
        Assert.Equal(first, points[0].Label);
        Assert.All(points, x => Assert.Equal(0, x.ReceivedPurchaseValue));
    }

    [Fact]
    public void MissingDates_UseCurrentMonth_AndPreserveValuesAndFilters()
    {
        var query = new PLPUPurchaseOverviewQuery { SupplierId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), Keyword = "NVL" };
        var window = PLPUMonthChartWindow.Resolve(query, new(2026, 10, 14));
        Assert.Equal(new DateTime(2026, 5, 1), window.From);
        Assert.Equal(new DateTime(2026, 10, 31), window.To);
        var point = new PLPUPurchaseReportChartPointDto { Label = "2026-10", PeriodStart = new(2026, 10, 1), ReceivedPurchaseValue = 500, WarehouseReceivedQuantity = 20 };
        var filled = PLPUMonthChartWindow.Fill([point], window.From, window.To);
        Assert.Same(point, filled[^1]);
        Assert.Equal(500, filled.Sum(x => x.ReceivedPurchaseValue));
        var expanded = PLPUMonthChartWindow.CreateQuery(query, window.From, window.To);
        Assert.Equal(query.SupplierId, expanded.SupplierId);
        Assert.Equal(query.CompanyId, expanded.CompanyId);
        Assert.Equal(query.Keyword, expanded.Keyword);
        Assert.Null(query.From);
    }

    internal sealed class Clock : IDateTimeProvider
    {
        public DateTime Now => new(2026, 10, 14);
    }
}
