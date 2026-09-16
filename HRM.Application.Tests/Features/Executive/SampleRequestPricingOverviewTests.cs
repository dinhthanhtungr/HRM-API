using HRM.Application.Abstractions.Security;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.Executive.SampleRequestPricingOverview;
using HRM.Application.Features.Executive.SampleRequestPricingOverview.Models;
using HRM.Application.Features.InternalMail.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using HRM.Domain.Enums.CustomerEnum;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.Executive;

public sealed class SampleRequestPricingOverviewTests
{
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
        Assert.Equal(ProductPricingWorkbenchView.All, query.View);
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
        int version)
        => new()
        {
            ProductPricingVersionId = Guid.NewGuid(),
            ProductId = productId,
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
