using HRM.Application.Features.Reports.DeliveryOtd;
using HRM.Application.Features.Reports.DeliveryOtd.Queries.GetDeliveryOtdCharts;

namespace HRM.Application.Tests.Features.Reports;

public sealed class DeliveryOtdMonthlyTrendTests
{
    [Theory]
    [InlineData(10, 10, 6, "2026-05")]
    [InlineData(7, 9, 6, "2026-04")]
    [InlineData(1, 12, 12, "2026-01")]
    public void MonthlyWindow_ExpandsMonthAndQuarter_AndKeepsWholeYear(int from, int to, int count, string first)
    {
        var source = new DeliveryOtdFilter { From = new(2026, from, 1), To = new(2026, to, 28),
            DateBasis = DeliveryOtdDateBasis.Promised, CustomerId = Guid.NewGuid(), ProductId = Guid.NewGuid(),
            ManagerId = Guid.NewGuid(), AsOf = new(2026, 10, 15) };
        var expanded = DeliveryOtdMonthlyTrend.Expand(source);
        var points = DeliveryOtdMonthlyTrend.Fill([], expanded, source.AsOf.Value);
        Assert.Equal(count, points.Count);
        Assert.Equal(first, points[0].Key);
        Assert.Equal($"2026-{to:00}", points[^1].Key);
        Assert.Equal(source.CustomerId, expanded.CustomerId);
        Assert.Equal(source.ProductId, expanded.ProductId);
        Assert.Equal(source.ManagerId, expanded.ManagerId);
        Assert.Equal(source.AsOf, expanded.AsOf);
        Assert.Equal(source.DateBasis, expanded.DateBasis);
        Assert.All(points, x => { Assert.Equal(0, x.Summary.TotalLines); Assert.Null(x.Summary.OtdPercent); });
    }

    [Fact]
    public void Fill_PreservesExistingMonth_AndCrossesYearBoundary()
    {
        var window = DeliveryOtdMonthlyTrend.Expand(new() { From = new(2026, 2, 1), To = new(2026, 2, 28) });
        var point = new DeliveryOtdChartPoint("2026-02", "2026-02", DeliveryOtdCalculator.Summarize([], new(2026, 2, 28)));
        var points = DeliveryOtdMonthlyTrend.Fill([point], window, new(2026, 2, 28));
        Assert.Equal("2025-09", points[0].Key);
        Assert.Same(point, points[^1]);
        Assert.Equal(6, points.Count);
    }
}
