using HRM.Application.Features.Timeline.Dtos;
using HRM.Application.Features.Timeline.Queries.GetSaleOrderTimelineDetail;

namespace HRM.Application.Tests.Features.Timeline;

public sealed class SaleOrderWarehouseReceiptTimelineRulesTests
{
    [Theory]
    [InlineData("PGH260900123", true)]
    [InlineData(" pgh260900123 ", true)]
    [InlineData("XUẤT DO NHẬP TRÙNG", false)]
    [InlineData("XUẤT KHO SANG BAO", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsDeliveryExportCode_OnlyExemptsPghPrefix(string? code, bool expected)
        => Assert.Equal(expected, SaleOrderWarehouseReceiptTimelineRules.IsDeliveryExportCode(code));

    [Theory]
    [InlineData(0, 500, null)]
    [InlineData(200, 500, "Stock")]
    [InlineData(500, 500, "Stocked")]
    [InlineData(600, 500, "Stocked")]
    public void ResolveStatus_UsesTotalImportedQuantity(decimal imported, decimal requested, string? expected)
        => Assert.Equal(expected, SaleOrderWarehouseReceiptTimelineRules.ResolveStatus(imported, requested));

    [Fact]
    public void BuildEvents_AccumulatesBeforeApplyingStatusFilter()
    {
        var receipts = new[]
        {
            new TimelineItemDto { ReceiptQuantity = 300, CreatedDate = new DateTime(2026, 10, 4), CreatedByName = "Người nhập" },
            new TimelineItemDto { ReceiptQuantity = 200, CreatedDate = new DateTime(2026, 10, 3) }
        };
        var all = SaleOrderWarehouseReceiptTimelineRules.BuildEvents(receipts, 500, null);
        Assert.Equal(new[] { "Stock", "Stocked" }, all.Select(x => x.Status));
        Assert.Equal(200m, all[0].ImportedQuantity);
        var completed = Assert.Single(SaleOrderWarehouseReceiptTimelineRules.BuildEvents(receipts, 500, "Stocked"));
        Assert.Equal(500m, completed.ImportedQuantity);
        Assert.Equal(300m, completed.ReceiptQuantity);
        Assert.Equal("Người nhập", completed.CreatedByName);
    }
    [Fact]
    public void BuildEvents_DeductsSignedExportsBeforeFiltering()
    {
        var events = new[]
        {
            new TimelineItemDto { ReceiptQuantity = 1000, CreatedDate = new DateTime(2026, 9, 24) },
            new TimelineItemDto { ReceiptQuantity = 1954, CreatedDate = new DateTime(2026, 9, 25) },
            new TimelineItemDto { ReceiptQuantity = -1000, CreatedDate = new DateTime(2026, 9, 28) }
        };
        var all = SaleOrderWarehouseReceiptTimelineRules.BuildEvents(events, 2000, null);
        Assert.Equal(1954m, all.Last().ImportedQuantity);
        Assert.Equal("Stock", all.Last().Status);
        Assert.Equal(-1000m, all.Last().ReceiptQuantity);
        var partial = SaleOrderWarehouseReceiptTimelineRules.BuildEvents(events, 2000, "Stock");
        Assert.Equal(1954m, partial.Last().ImportedQuantity);
    }

    [Fact]
    public void BuildEvents_FullDeductionDoesNotLeaveStockedAsLatestStatus()
    {
        var events = new[]
        {
            new TimelineItemDto { ReceiptQuantity = 100, CreatedDate = new DateTime(2026, 9, 24) },
            new TimelineItemDto { ReceiptQuantity = -100, CreatedDate = new DateTime(2026, 9, 25) }
        };
        var last = SaleOrderWarehouseReceiptTimelineRules.BuildEvents(events, 100, null).Last();
        Assert.Equal(0m, last.ImportedQuantity);
        Assert.Equal("Stock", last.Status);
    }
}
