using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class ProductPricingWorkbenchVisibilityTests
{
    [Theory]
    [InlineData(ApplicationRoles.Sales.SaleUser, true, false)]
    [InlineData(ApplicationRoles.President, true, true)]
    [InlineData(ApplicationRoles.Developer, true, true)]
    [InlineData(ApplicationRoles.Leader, false, false)]
    public void AccessRules_SeparateViewAndManagePermissions(
        string role,
        bool canView,
        bool canManage)
    {
        var currentUser = new TestCurrentUser(role);

        Assert.Equal(canView, ProductPricingAccessRules.CanViewWorkbench(currentUser));
        Assert.Equal(canManage, ProductPricingAccessRules.CanManage(currentUser));
    }

    [Fact]
    public void ApplyToSummary_SaleKeepsSellingPriceAndWorkflowFieldsOnly()
    {
        var source = FullSummary();

        var result = ProductPricingWorkbenchVisibility.ApplyToSummary(
            source,
            canManagePricing: false);

        Assert.False(result.CanManagePricing);
        Assert.True(result.CanOpenPricingDetail);
        Assert.Equal(source.StandardSellingPrice, result.StandardSellingPrice);
        Assert.Equal(source.PricingStatus, result.PricingStatus);
        Assert.Equal(source.WaitingQuotationCount, result.WaitingQuotationCount);
        Assert.Null(result.CurrentMaterialCost);
        Assert.Null(result.ManufacturingCost);
        Assert.Null(result.ProfitMarginRate);
        Assert.Equal(source.SourceExternalId, result.SourceExternalId);
        Assert.Equal(source.SourceName, result.SourceName);
        Assert.Equal(source.SourceStatus, result.SourceStatus);
        Assert.True(result.SourceIsEligible);
        Assert.Null(result.ApprovedPricingVersionId);
    }

    [Fact]
    public void ApplyToDetail_SaleKeepsSellingPriceSourceAndSanitizedTiers()
    {
        var detail = new ProductPricingWorkbenchDetailDto
        {
            Summary = FullSummary(),
            ManufacturingCost = 10_000m,
            StandardSellingPrice = 200_000m,
            ProfitMarginRate = 20m,
            SelectedSource = new ProductPricingSourceOptionDto
            {
                SourceType = ProductPricingSourceType.Formula,
                SourceId = Guid.NewGuid(),
                ExternalId = "VU260800009",
                Name = "F001",
                CurrentMaterialCost = 150_000m,
                Materials = [new QuotationProductPricingMaterialDto()]
            },
            DisplayPriceTiers =
            [
                new QuotationPricingWorkspaceTierDto
                {
                    QuantityRangeLabel = "< 50 kg",
                    MaxQuantity = 50m,
                    UnitPrice = 316_697m,
                    MarginVsMaterialPercent = 25m,
                    MarginVsCostPercent = 15m,
                    RequiresManualPrice = false,
                    IsStored = true,
                    SortOrder = 0
                }
            ],
            PricingHistory = [new ProductPricingVersionDto()],
            RelatedQuotations = [new ProductPricingRelatedQuotationDto()]
        };

        var result = ProductPricingWorkbenchVisibility.ApplyToDetail(
            detail,
            canManagePricing: false);

        Assert.Equal(200_000m, result.StandardSellingPrice);
        Assert.Null(result.ManufacturingCost);
        Assert.Null(result.ProfitMarginRate);
        Assert.Equal("VU260800009", result.SelectedSource?.ExternalId);
        Assert.Equal("F001", result.SelectedSource?.Name);
        Assert.Null(result.SelectedSource?.CurrentMaterialCost);
        Assert.Empty(result.SelectedSource?.Materials ?? []);
        Assert.Null(result.DraftPricing);
        Assert.Null(result.ApprovedPricing);
        var saleTier = Assert.Single(result.DisplayPriceTiers);
        Assert.Equal("< 50 kg", saleTier.QuantityRangeLabel);
        Assert.Equal(50m, saleTier.MaxQuantity);
        Assert.Equal(316_697m, saleTier.UnitPrice);
        Assert.True(saleTier.IsStored);
        Assert.Null(saleTier.MarginVsMaterialPercent);
        Assert.Null(saleTier.MarginVsCostPercent);
        Assert.Empty(result.PricingHistory);
        Assert.Empty(result.RelatedQuotations);
    }

    [Fact]
    public void ApplyToDetail_ManagerKeepsFullPayload()
    {
        var detail = new ProductPricingWorkbenchDetailDto
        {
            Summary = FullSummary(),
            ManufacturingCost = 10_000m,
            StandardSellingPrice = 200_000m,
            ProfitMarginRate = 20m
        };

        var result = ProductPricingWorkbenchVisibility.ApplyToDetail(
            detail,
            canManagePricing: true);

        Assert.True(result.Summary.CanManagePricing);
        Assert.Equal(detail.ManufacturingCost, result.ManufacturingCost);
        Assert.Equal(detail.StandardSellingPrice, result.StandardSellingPrice);
        Assert.Equal(detail.ProfitMarginRate, result.ProfitMarginRate);
    }

    private static ProductPricingWorkbenchItemDto FullSummary()
        => new()
        {
            ProductId = Guid.NewGuid(),
            ProductCode = "TP4909",
            ProductName = "Test product",
            Currency = "VND",
            PricingStatus = ProductPricingLookupStatus.Approved,
            WaitingQuotationCount = 2,
            SourceType = ProductPricingSourceType.Formula,
            SourceId = Guid.NewGuid(),
            SourceExternalId = "VU260800009",
            SourceName = "F001",
            SourceStatus = "Approved",
            SourceIsEligible = true,
            CurrentMaterialCost = 150_000m,
            ManufacturingCost = 10_000m,
            StandardSellingPrice = 200_000m,
            ProfitMarginRate = 20m,
            ApprovedPricingVersionId = Guid.NewGuid()
        };

    private sealed class TestCurrentUser(string role) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid? EmployeeId { get; } = Guid.NewGuid();
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public string? UserName => "test";
        public string? Email => "test@example.com";
        public IReadOnlyCollection<string> Roles { get; } = [role];
        public bool IsInRole(string expectedRole) =>
            Roles.Contains(expectedRole, StringComparer.OrdinalIgnoreCase);
    }
}
