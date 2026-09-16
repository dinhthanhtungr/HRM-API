using HRM.Application.Features.Pricing.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Rules;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestTrialPricingVisibilityTests
{
    [Fact]
    public void Apply_SaleKeepsApprovedPriceAndHidesSystemCalculatedPrice()
    {
        var target = new SampleRequestSampleTrialReportDto();
        var approvedAt = new DateTime(2026, 9, 12, 9, 34, 0);
        var access = new PricingAccessDecision(
            CanViewWorkbench: true,
            CanViewApprovedSellingPrice: true,
            CanViewSystemCalculatedPrice: false,
            CanViewMaterialCost: false,
            CanViewManufacturingCost: false,
            CanViewMargin: false,
            CanViewHistory: false,
            CanManage: false,
            CanApprove: false);

        SampleRequestTrialPricingVisibility.Apply(
            target,
            approvedStandardSellingPrice: 29_500m,
            publisherNote: "Giá đã duyệt",
            approvedAt,
            systemCalculatedStandardSellingPrice: 36_225m,
            access);

        Assert.Equal(29_500m, target.ApprovedStandardSellingPrice);
        Assert.Equal("Giá đã duyệt", target.PublisherNote);
        Assert.Equal(approvedAt, target.StandardSellingPriceApprovedAt);
        Assert.Null(target.SystemCalculatedStandardSellingPrice);
    }

    [Fact]
    public void Apply_PricingManagerKeepsApprovedAndSystemCalculatedPrices()
    {
        var target = new SampleRequestSampleTrialReportDto();
        var access = new PricingAccessDecision(
            CanViewWorkbench: true,
            CanViewApprovedSellingPrice: true,
            CanViewSystemCalculatedPrice: true,
            CanViewMaterialCost: true,
            CanViewManufacturingCost: true,
            CanViewMargin: true,
            CanViewHistory: true,
            CanManage: true,
            CanApprove: true);

        SampleRequestTrialPricingVisibility.Apply(
            target,
            approvedStandardSellingPrice: 70_000m,
            publisherNote: null,
            approvedAt: null,
            systemCalculatedStandardSellingPrice: 105_000m,
            access);

        Assert.Equal(70_000m, target.ApprovedStandardSellingPrice);
        Assert.Equal(105_000m, target.SystemCalculatedStandardSellingPrice);
    }
}
