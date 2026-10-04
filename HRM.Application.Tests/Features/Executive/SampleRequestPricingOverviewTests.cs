using HRM.Application.Abstractions.Security;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.Executive.SampleRequestPricingOverview;
using HRM.Application.Features.Executive.SampleRequestPricingOverview.Dtos;
using HRM.Application.Features.Executive.SampleRequestPricingOverview.Models;
using HRM.Application.Features.Executive.MerchandiseOrderPriceHistory;
using HRM.Application.Features.Executive.MerchandiseOrderPriceHistory.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Dtos;
using HRM.Application.Features.Executive.ProductPricingReview.Services;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Pricing.Authorization;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Merchadises;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Executive;

public sealed class SampleRequestPricingOverviewTests
{
    [Fact]
    public void MapRecommendedFormula_CalculatesRealtimeStandardPriceFromApprovedPricingInputs()
    {
        var productId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var result = GetSampleRequestPricingOverviewQueryHandler.MapRecommendedFormula(
            new SuggestedPricingFormulaCandidate
            {
                ProductId = productId,
                SourceType = PricingReviewSourceType.VA,
                SourceId = sourceId,
                SourceCode = "VA260900349",
                SourceName = "F001",
                Status = "Checking",
                PriorityAt = new DateTime(2026, 9, 22)
            },
            new ProductPricingSourceOptionDto
            {
                SourceId = sourceId,
                CurrentMaterialCost = 88_000m,
                IsCurrentMaterialCostComplete = true
            },
            new PricingVersionRow
            {
                ProductId = productId,
                MaterialCostSnapshot = 80_000m,
                ManufacturingCost = 16_000m,
                StandardSellingPrice = 120_000m,
                ProfitMarginRate = 20m
            },
            FullPricingAccess(),
            "VND",
            new DateTime(2026, 9, 22, 20, 32, 0));

        Assert.Equal(88_000m, result.RealtimeMaterialCost);
        Assert.Equal(16_000m, result.ManufacturingCost);
        Assert.Equal(20m, result.ProfitMarginRate);
        Assert.Equal(130_000m, result.RealtimeStandardSellingPrice);
        Assert.Equal("(88000 + 16000) / (1 - 0.2) = 130000 VND", result.PriceCalculationFormula);
    }

    [Fact]
    public void MapRecommendedFormula_WithoutApprovedPricingKeepsCalculatedSellingPriceUnavailable()
    {
        var result = GetSampleRequestPricingOverviewQueryHandler.MapRecommendedFormula(
            new SuggestedPricingFormulaCandidate
            {
                ProductId = Guid.NewGuid(),
                SourceType = PricingReviewSourceType.VU,
                SourceId = Guid.NewGuid(),
                PriorityAt = new DateTime(2026, 9, 22)
            },
            new ProductPricingSourceOptionDto
            {
                CurrentMaterialCost = 88_000m,
                IsCurrentMaterialCostComplete = true
            },
            null,
            FullPricingAccess(),
            "VND",
            new DateTime(2026, 9, 22, 20, 32, 0));

        Assert.Equal(88_000m, result.RealtimeMaterialCost);
        Assert.Null(result.ManufacturingCost);
        Assert.Null(result.ProfitMarginRate);
        Assert.Null(result.RealtimeStandardSellingPrice);
        Assert.Null(result.PriceCalculationFormula);
    }

    [Fact]
    public void MapPricing_ExposesRecommendedFormulaIndependentlyFromDisplayedFormula()
    {
        var recommended = new SampleRequestRecommendedPricingFormulaDto
        {
            SourceType = PricingReviewSourceType.VA,
            SourceId = Guid.NewGuid(),
            SourceCode = "VA260900349",
            SourceName = "F001",
            RealtimeMaterialCost = 41_481m,
            Currency = "VND",
            IsRealtimeMaterialCostComplete = true
        };

        var pricing = GetSampleRequestPricingOverviewQueryHandler.MapPricing(
            null,
            recommendedFormula: recommended);

        Assert.Same(recommended, pricing.RecommendedFormula);
        Assert.Null(pricing.DisplayedFormula);
        Assert.Equal(41_481m, pricing.RecommendedFormula!.RealtimeMaterialCost);
    }

    private static PricingAccessDecision FullPricingAccess() => new(
        CanViewWorkbench: true,
        CanViewApprovedSellingPrice: true,
        CanViewSystemCalculatedPrice: true,
        CanViewMaterialCost: true,
        CanViewManufacturingCost: true,
        CanViewMargin: true,
        CanViewHistory: true,
        CanManage: true,
        CanApprove: true);

    [Fact]
    public void MapItem_PreservesOneCardPerSampleRequestForSharedProduct()
    {
        var productId = Guid.NewGuid();
        var first = GetSampleRequestPricingOverviewQueryHandler.MapItem(
            Row(Guid.NewGuid(), productId), null, null);
        var second = GetSampleRequestPricingOverviewQueryHandler.MapItem(
            Row(Guid.NewGuid(), productId), null, null);

        Assert.NotEqual(first.SampleRequestId, second.SampleRequestId);
        Assert.Equal(productId, first.Product.ProductId);
        Assert.Equal(productId, second.Product.ProductId);
    }

    [Fact]
    public void MapItem_MapsColorFieldsFromProductColourNameLikeSampleRequestSummary()
    {
        var row = Row(
            Guid.NewGuid(),
            Guid.NewGuid(),
            colourCode: "TP-RED-01",
            colourName: "Đỏ");

        var item = GetSampleRequestPricingOverviewQueryHandler.MapItem(row, null, null);

        Assert.Equal("TP-RED-01", item.Product.ColourCode);
        Assert.Equal("Đỏ", item.Product.ColorValue);
        Assert.Equal("Đỏ", item.Product.ColorDisplayName);
    }

    [Fact]
    public void MapItem_MapsManagingSaleAndProductLab()
    {
        var saleEmployeeId = Guid.NewGuid();
        var labEmployeeId = Guid.NewGuid();
        var row = Row(
            Guid.NewGuid(),
            Guid.NewGuid(),
            saleEmployeeId: saleEmployeeId,
            saleName: "Nguyễn Sale",
            labEmployeeId: labEmployeeId,
            labName: "Trần Lab");

        var item = GetSampleRequestPricingOverviewQueryHandler.MapItem(row, null, null);

        Assert.Equal(saleEmployeeId, item.Customer.SaleEmployeeId);
        Assert.Equal("Nguyễn Sale", item.Customer.SaleName);
        Assert.Equal(labEmployeeId, item.Product.LabEmployeeId);
        Assert.Equal("Trần Lab", item.Product.LabName);
    }

    [Fact]
    public void MapItem_WithoutPricingOrConversation_ReturnsExplicitEmptyStates()
    {
        var item = GetSampleRequestPricingOverviewQueryHandler.MapItem(
            Row(Guid.NewGuid(), Guid.NewGuid()), null, null);

        Assert.Equal(ProductPricingLookupStatus.NoEligibleSource, item.Pricing.PricingStatus);
        Assert.Equal(ProductPricingHealthStatus.NoEligibleSource, item.Pricing.PricingHealthStatus);
        Assert.True(item.Pricing.RequiresPricingAction);
        Assert.Null(item.Pricing.StandardSellingPrice);
        Assert.Null(item.Conversation.ConversationId);
        Assert.Equal(0, item.Conversation.TotalMessageCount);
        Assert.Equal(0, item.Conversation.UnreadCount);
        Assert.False(item.Actions.CanOpenConversation);
        Assert.Null(item.LatestMerchandiseOrder);
    }

    [Fact]
    public void MapItem_UsesLatestMerchandiseOrderLineQuantityAndSellingPrice()
    {
        var latest = new LatestMerchandiseOrderDto
        {
            MerchandiseOrderId = Guid.NewGuid(),
            MerchandiseOrderCode = "MO-2026-000123",
            OrderType = "Merchandise",
            ItemId = Guid.NewGuid(),
            ItemCode = "TL3187",
            ItemName = "Product",
            Quantity = 1250m,
            UnitPrice = 95000m,
            Currency = "VND",
            SaleEmployeeId = Guid.NewGuid(),
            SaleName = "Nguyễn Sale",
            OrderStatus = "Completed"
        };

        var item = GetSampleRequestPricingOverviewQueryHandler.MapItem(
            Row(Guid.NewGuid(), latest.ItemId), null, null, latestMerchandiseOrder: latest);

        Assert.Same(latest, item.LatestMerchandiseOrder);
        Assert.Equal(1250m, item.LatestMerchandiseOrder!.Quantity);
        Assert.Equal(95000m, item.LatestMerchandiseOrder.UnitPrice);
        Assert.Equal("Nguyễn Sale", item.LatestMerchandiseOrder.SaleName);
        Assert.Equal("Merchandise", item.LatestMerchandiseOrder.OrderType);
    }

    [Theory]
    [InlineData("Approved", true)]
    [InlineData("Processing", true)]
    [InlineData("Delivering", true)]
    [InlineData("Delivered", true)]
    [InlineData("Completed", true)]
    [InlineData("New", false)]
    [InlineData("Pending", false)]
    [InlineData("Paused", false)]
    [InlineData("Cancelled", false)]
    public void MerchandiseOrderHistory_UsesOnlyEligibleSaleStatuses(string status, bool expected)
        => Assert.Equal(expected, GetMerchandiseOrderPriceHistoryQueryHandler.IsEligibleStatus(status));

    [Theory]
    [InlineData(OrderType.Merchandise, true)]
    [InlineData(OrderType.SampleRequest, true)]
    [InlineData(OrderType.Complaint, true)]
    [InlineData(OrderType.Internal, false)]
    public void MerchandiseOrderHistory_UsesConfiguredSaleOrderTypes(
        OrderType orderType,
        bool expected)
        => Assert.Equal(expected, GetMerchandiseOrderPriceHistoryQueryHandler.IsEligibleOrderType(orderType));

    [Fact]
    public void MerchandiseOrderHistoryQuery_RequiresProductIdAndStrictPagination()
    {
        var query = new GetMerchandiseOrderPriceHistoryQuery
        {
            ItemId = Guid.NewGuid(),
            PageNumber = 1,
            PageSize = 100,
            SortBy = "unitPrice",
            SortDirection = "asc"
        };

        Assert.Equal("unitPrice", query.NormalizedSortBy);
        Assert.False(query.SortDescending);
        Assert.Equal(1, query.NormalizedPageNumber);
        Assert.Equal(100, query.NormalizedPageSize);
        Assert.Null(typeof(GetMerchandiseOrderPriceHistoryQuery).GetProperty("CompanyId"));
    }

    [Fact]
    public void MapItem_UsesCanonicalPricingAndEmployeeConversationAggregate()
    {
        var approvedId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var item = GetSampleRequestPricingOverviewQueryHandler.MapItem(
            Row(Guid.NewGuid(), Guid.NewGuid()),
            new ProductPricingWorkbenchItemDto
            {
                Currency = "VND",
                StandardSellingPrice = 55_000m,
                PublisherNote = "Áp dụng cho đơn từ 100 kg",
                CurrentMaterialCost = 33_000m,
                ManufacturingCost = 10_000m,
                ProfitMarginRate = 21.8182m,
                PricingStatus = ProductPricingLookupStatus.Approved,
                PricingHealthStatus = ProductPricingHealthStatus.Ready,
                PricingAttentionSources =
                [
                    ProductPricingAttentionSource.SaleQuotationRequested,
                    ProductPricingAttentionSource.LabFormulaConfirmed
                ],
                ApprovedPricingVersionId = approvedId,
                SourceType = ProductPricingSourceType.Formula,
                SourceId = Guid.NewGuid(),
                SourceExternalId = "CT_1001",
                SourceName = "Formula 1001",
                SourceStatus = "Approved",
                CanOpenPricingDetail = true,
                CanManagePricing = true
            },
            new ConversationOverviewRow
            {
                SampleRequestId = Guid.NewGuid(),
                ConversationId = conversationId,
                TotalMessageCount = 12,
                UnreadCount = 2,
                LastMessageAt = new DateTime(2026, 9, 7, 11, 20, 0),
                CanOpen = true
            });

        Assert.Equal(ProductPricingLookupStatus.Approved, item.Pricing.PricingStatus);
        Assert.Equal(55_000m, item.Pricing.StandardSellingPrice);
        Assert.Equal("Áp dụng cho đơn từ 100 kg", item.Pricing.PublisherNote);
        Assert.Equal(21.8182m, item.Pricing.ProfitMarginPercent);
        Assert.Equal(21.8182m, item.Pricing.ProfitMarginRate);
        Assert.Equal(approvedId, item.Pricing.ApprovedPricingVersionId);
        Assert.Equal(
            [
                ProductPricingAttentionSource.SaleQuotationRequested,
                ProductPricingAttentionSource.LabFormulaConfirmed
            ],
            item.Pricing.PricingAttentionSources);
        Assert.Equal("Standard", item.Pricing.DisplayedFormula!.PriceKind);
        Assert.Equal("CT_1001", item.Pricing.DisplayedFormula.Code);
        Assert.Equal(conversationId, item.Conversation.ConversationId);
        Assert.Equal(2, item.Conversation.UnreadCount);
        Assert.True(item.Actions.CanManagePricing);
        Assert.True(item.Actions.CanOpenConversation);
    }

    [Fact]
    public void MapPricing_WithoutStoredVersion_LabelsSelectedFormulaAsSystemCalculated()
    {
        var sourceId = Guid.NewGuid();

        var pricing = GetSampleRequestPricingOverviewQueryHandler.MapPricing(
            new ProductPricingWorkbenchItemDto
            {
                Currency = "VND",
                IsSystemCalculatedDraft = true,
                SourceType = ProductPricingSourceType.Formula,
                SourceId = sourceId,
                SourceExternalId = "CT_SYSTEM",
                SourceName = "System formula",
                PricingStatus = ProductPricingLookupStatus.Draft
            });

        Assert.Equal(sourceId, pricing.DisplayedFormula!.SourceId);
        Assert.Equal("SystemCalculated", pricing.DisplayedFormula.PriceKind);
        Assert.Null(pricing.DraftPricingVersionId);
        Assert.Null(pricing.ApprovedPricingVersionId);
    }

    [Fact]
    public void MapPricing_ForwardsSharedRealtimePriceComparison()
    {
        var calculatedAt = new DateTime(2026, 9, 17, 11, 59, 14);
        var comparison = new StandardPriceRealtimeComparisonDto
        {
            Currency = "VND",
            ApprovedStandardPrice = 244_514m,
            RealtimeAdjustedStandardPrice = 244_514m,
            StandardPriceDifference = 0m,
            StandardPriceDifferencePercent = 0m,
            ApprovedMaterialCostSnapshot = 187_727m,
            RealtimeMaterialCost = 187_727m,
            MaterialCostDifference = 0m,
            MaterialCostDifferencePercent = 0m,
            MovementStatus = MaterialCostMovementStatus.Unchanged,
            IsMaterialCostComplete = true,
            IsIncreaseWarning = false,
            WarningThresholdPercent = 5m,
            CalculatedAt = calculatedAt
        };

        var pricing = GetSampleRequestPricingOverviewQueryHandler.MapPricing(
            new ProductPricingWorkbenchItemDto
            {
                Currency = "VND",
                RealtimePriceComparison = comparison
            });

        Assert.Same(comparison, pricing.RealtimePriceComparison);
    }

    [Fact]
    public void MapPricing_ForwardsFlatSourceMaterials()
    {
        var materials = new[]
        {
            new QuotationProductPricingMaterialDto
            {
                FormulaMaterialId = Guid.NewGuid(),
                ItemId = Guid.NewGuid()
            }
        };

        var pricing = GetSampleRequestPricingOverviewQueryHandler.MapPricing(
            new ProductPricingWorkbenchItemDto { Currency = "VND" },
            materials);

        Assert.Same(materials, pricing.Materials);
    }

    [Fact]
    public void ResolveCurrentVersions_SelectsHighestVersionPerCanonicalStatus()
    {
        var productId = Guid.NewGuid();
        var rows = new[]
        {
            Version(productId, ProductPricingStatus.Draft, 1),
            Version(productId, ProductPricingStatus.Draft, 3),
            Version(productId, ProductPricingStatus.Approved, 2)
        };

        var current = GetSampleRequestPricingOverviewQueryHandler.ResolveCurrentVersions(rows);

        Assert.Equal(3, current.Draft!.Version);
        Assert.Equal(2, current.Approved!.Version);
        Assert.Same(current.Draft, current.Preferred);
    }

    [Fact]
    public void FormulaChangedView_RequiresApprovedStandardPriceSource()
    {
        var productId = Guid.NewGuid();
        var withoutApprovedPrice = Version(productId, ProductPricingStatus.Draft, 1);
        var approvedWithoutSource = Version(productId, ProductPricingStatus.Approved, 2);
        var approvedWithSource = Version(
            productId,
            ProductPricingStatus.Approved,
            3,
            ProductPricingSourceType.Formula,
            Guid.NewGuid());

        Assert.False(GetSampleRequestPricingOverviewQueryHandler.HasApprovedFormulaBaseline(null));
        Assert.False(GetSampleRequestPricingOverviewQueryHandler.HasApprovedFormulaBaseline(withoutApprovedPrice));
        Assert.False(GetSampleRequestPricingOverviewQueryHandler.HasApprovedFormulaBaseline(approvedWithoutSource));
        Assert.True(GetSampleRequestPricingOverviewQueryHandler.HasApprovedFormulaBaseline(approvedWithSource));
    }

    [Theory]
    [InlineData("President", true)]
    [InlineData("Developer", true)]
    [InlineData("SaleUser", false)]
    public void Access_IsLimitedToExecutivePricingManagers(string role, bool expected)
    {
        Assert.Equal(expected, GetSampleRequestPricingOverviewQueryHandler.CanAccess(new User(role)));
    }

    [Theory]
    [InlineData("Developer", true)]
    [InlineData("President", true)]
    [InlineData("SaleUser", false)]
    public void ExecutiveSampleRequestConversationAccess_IsLimitedToExistingExecutiveRoles(
        string role,
        bool expected)
    {
        Assert.Equal(
            expected,
            InternalConversationAccessRules.CanReadExecutiveSampleRequestConversations(new User(role)));
    }

    [Fact]
    public void Query_NormalizesPaginationAndDoesNotExposeCompanyId()
    {
        var query = new GetSampleRequestPricingOverviewQuery
        {
            PageNumber = 0,
            PageSize = 999,
            Currency = " vnd ",
            SortDirection = "asc"
        };

        Assert.Equal(1, query.NormalizedPageNumber);
        Assert.Equal(100, query.NormalizedPageSize);
        Assert.Equal("VND", query.NormalizedCurrency);
        Assert.Equal("createdDate", query.NormalizedSortBy);
        Assert.Equal(SampleRequestPricingOverviewView.All, query.View);
        Assert.True(Enum.IsDefined(SampleRequestPricingOverviewView.FormulaChanged));
        Assert.False(query.SortDescending);
        Assert.Null(typeof(GetSampleRequestPricingOverviewQuery).GetProperty("CompanyId"));
    }

    [Fact]
    public void ApplyFilters_QuotationKeywordIncludesLegacyProductFallback()
    {
        using var dbContext = CreateDbContext();
        var request = new GetSampleRequestPricingOverviewQuery
        {
            Keyword = "BBG260900088"
        };

        var sql = GetSampleRequestPricingOverviewQueryHandler.ApplyFilters(
                dbContext.SampleRequests.AsNoTracking(),
                dbContext.QuotationLines.AsNoTracking(),
                request)
            .ToQueryString();

        Assert.Contains("QuotationLines", sql, StringComparison.Ordinal);
        Assert.Contains("SampleRequestId", sql, StringComparison.Ordinal);
        Assert.Contains("IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("ProductId", sql, StringComparison.Ordinal);
        Assert.Contains("CustomerId", sql, StringComparison.Ordinal);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=test;Password=test")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static SampleRequestOverviewRow Row(
        Guid sampleRequestId,
        Guid productId,
        string? colourCode = null,
        string? colourName = null,
        Guid? saleEmployeeId = null,
        string? saleName = null,
        Guid? labEmployeeId = null,
        string? labName = null)
        => new()
        {
            SampleRequestId = sampleRequestId,
            ProductId = productId,
            RequestCode = "TP_30398",
            ProductCode = "TP21119",
            ProductName = "Product",
            ColourCode = colourCode,
            ColourName = colourName,
            LabEmployeeId = labEmployeeId,
            LabName = labName,
            CustomerCode = "KH_3824",
            CustomerName = "Customer",
            SaleEmployeeId = saleEmployeeId,
            SaleName = saleName,
            CreatedDate = new DateTime(2026, 9, 7),
            LatestActivityAt = new DateTime(2026, 9, 7),
            Status = "InProgress"
        };

    private static PricingVersionRow Version(
        Guid productId,
        ProductPricingStatus status,
        int version,
        ProductPricingSourceType? sourceType = null,
        Guid? sourceId = null)
        => new()
        {
            ProductPricingVersionId = Guid.NewGuid(),
            ProductId = productId,
            SourceType = sourceType,
            SourceId = sourceId,
            Status = status,
            Version = version,
            CreatedDate = new DateTime(2026, 9, version)
        };

    private sealed class User(string role) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.NewGuid();
        public Guid? EmployeeId => Guid.NewGuid();
        public Guid? CompanyId => Guid.NewGuid();
        public string? UserName => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [role];
        public bool IsInRole(string candidate) => string.Equals(candidate, role, StringComparison.Ordinal);
    }
}
