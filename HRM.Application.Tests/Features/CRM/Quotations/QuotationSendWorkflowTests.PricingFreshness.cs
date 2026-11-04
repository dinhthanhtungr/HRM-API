using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Entities.CustomerSchema;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed partial class QuotationSendWorkflowTests
{
    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public async Task ApprovedReferenceMetadataUsesExactExpiryBoundary(int offsetSeconds, bool effective)
    {
        var f = new Fixture();
        f.Standard.ApprovedAt = f.Now.AddDays(-30).AddSeconds(-offsetSeconds);
        var tier = f.Standard.PriceTiers.Single();
        tier.ProductPricingVersionId = f.Standard.ProductPricingVersionId;
        f.ReadStore.Seed("ProductPricingTiers", tier);
        var reader = new ApprovedProductPricingTierReader(f.ReadDb, f.Clock, Options.Create(new QuotationFeatureOptions()));
        var references = await reader.LoadAsync([f.Standard.ProductId], f.Quote.CompanyId, f.Quote.Currency, default);
        var dto = QuotationProductPricingPreviewMapper.MapApproved(references[f.Standard.ProductId])!;
        Assert.Equal(effective, dto.IsEffectiveForSending);
        Assert.Equal(f.Standard.ApprovedAt, dto.ValidFrom);
        Assert.Equal(f.Standard.ApprovedAt!.Value.AddDays(30), dto.ValidUntil);
        Assert.Equal(f.Now, dto.EvaluatedAt);
    }

    [Fact]
    public async Task SendAndPreviewSelectLatestEffectiveApprovalInMatchingCurrency()
    {
        var f = new Fixture();
        var older = new ProductPricingVersion
        {
            ProductPricingVersionId = Guid.NewGuid(), CompanyId = f.Quote.CompanyId,
            ProductId = f.Standard.ProductId, Currency = f.Quote.Currency,
            Status = ProductPricingStatus.Approved, ApprovedAt = f.Now.AddDays(-2), Version = 99,
            PriceTiers = [new ProductPricingTier { MinQuantity = 300, MaxQuantity = 600, UnitPrice = 9999 }]
        };
        var otherCurrency = new ProductPricingVersion
        {
            ProductPricingVersionId = Guid.NewGuid(), CompanyId = f.Quote.CompanyId,
            ProductId = f.Standard.ProductId, Currency = "USD",
            Status = ProductPricingStatus.Approved, ApprovedAt = f.Now, Version = 100
        };
        f.ReadStore.Seed("ProductPricingVersions", f.Standard, older, otherCurrency);
        var tier = f.Standard.PriceTiers.Single();
        tier.ProductPricingVersionId = f.Standard.ProductPricingVersionId;
        f.ReadStore.Seed("ProductPricingTiers", tier);
        var reader = new ApprovedProductPricingTierReader(f.ReadDb, f.Clock, Options.Create(new QuotationFeatureOptions()));
        var references = await reader.LoadAsync([f.Standard.ProductId], f.Quote.CompanyId, f.Quote.Currency, default);
        Assert.Equal(f.Standard.ProductPricingVersionId, references[f.Standard.ProductId].ProductPricingVersionId);
        var result = await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new MarkQuotationSentRequest { Note = "Discount" }, null, default);
        Assert.True(result.Success);
        Assert.Equal(f.Standard.ProductPricingVersionId,
            Assert.Single(QuotationSendService.ReadPayload(Assert.Single(f.Published).PayloadJson)!.Comparison).StandardPricingVersionId);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("inactive")]
    [InlineData("currency")]
    [InlineData("tier")]
    [InlineData("version")]
    [InlineData("customer")]
    [InlineData("legacy")]
    public async Task ConfirmationOnlyRejectsCustomerChangesNotReferenceChanges(string change)
    {
        var f = new Fixture("President");
        var message = f.SeedPending();
        switch (change)
        {
            case "expired": f.Standard.ApprovedAt = f.Now.AddDays(-30); break;
            case "inactive": f.Standard.IsActive = false; break;
            case "currency": f.Standard.Currency = "USD"; break;
            case "tier": f.Standard.PriceTiers.Single().UnitPrice = 110; break;
            case "version": f.Standard.ProductPricingVersionId = Guid.NewGuid(); break;
            case "customer": f.Quote.Lines.Single().PriceTiers.Single().SetPrices(85, 10); break;
            case "legacy":
                var payload = QuotationSendService.ReadPayload(message.PayloadJson)!;
                payload.ComparisonRuleVersion = 0;
                message.PayloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                break;
        }

        var result = await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new MarkQuotationSentRequest { ExpectedUpdatedDate = f.Now }, message.InternalMessageId, default);

        Assert.Equal(change != "customer", result.Success);
        Assert.Equal(change == "customer" ? QuotationStatus.PendingSendConfirmation : QuotationStatus.Sent, f.Quote.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PreviousRuleVersionCanConfirmWithoutRebuildingItsSnapshot(int version)
    {
        var f = new Fixture("President");
        var message = f.SeedPending();
        var payload = QuotationSendService.ReadPayload(message.PayloadJson)!;
        payload.ComparisonRuleVersion = version;
        message.PayloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        message.Body = "Historical reason and price comparison";
        var originalComparison = JsonSerializer.Serialize(payload.Comparison);

        var result = await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new MarkQuotationSentRequest { ExpectedUpdatedDate = f.Now }, message.InternalMessageId, default);

        Assert.True(result.Success, result.Message);
        var confirmed = QuotationSendService.ReadPayload(message.PayloadJson)!;
        Assert.Equal(originalComparison, JsonSerializer.Serialize(confirmed.Comparison));
        Assert.Equal(version, confirmed.ComparisonRuleVersion);
        Assert.Equal("Historical reason and price comparison", message.Body);
        Assert.Equal(QuotationStatus.Sent, f.Quote.Status);
    }

    [Fact]
    public async Task RuleThreeOutsideSnapshotCanConfirmWithTheSameLastTierReference()
    {
        var f = new Fixture("President");
        SetScreenshotStandard(f.Standard);
        var customer = f.Quote.Lines.Single().PriceTiers.Single();
        customer.MinQuantity = 20000;
        customer.MaxQuantity = null;
        customer.SetPrices(28500, 0);
        var message = f.SeedPending();
        var payload = QuotationSendService.ReadPayload(message.PayloadJson)!;
        Assert.Equal(3, payload.ComparisonRuleVersion);
        Assert.Equal(29000, Assert.Single(payload.Comparison).ReferenceUnitPrice);
        Assert.Equal(QuotationSendPriceSegment.LastApprovedTierFallback,
            Assert.Single(payload.Comparison).ComparisonBasis);

        var result = await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new MarkQuotationSentRequest { ExpectedUpdatedDate = f.Now }, message.InternalMessageId, default);

        Assert.True(result.Success, result.Message);
        Assert.Equal(QuotationStatus.Sent, f.Quote.Status);
    }

    [Fact]
    public async Task PendingRequestCarriesEffectiveSourceAndTierReferenceMetadata()
    {
        var f = new Fixture();
        var result = await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new MarkQuotationSentRequest { Note = "Discount" }, null, default);
        Assert.True(result.Success);
        var payload = QuotationSendService.ReadPayload(Assert.Single(f.Published).PayloadJson)!;
        Assert.Equal(QuotationSendConfirmationPayload.CurrentComparisonRuleVersion, payload.ComparisonRuleVersion);
        Assert.Equal(f.Now, payload.PricingEvaluatedAt);
        var row = Assert.Single(payload.Comparison);
        Assert.Equal(f.Standard.ApprovedAt!.Value.AddDays(30), row.StandardValidUntil);
        Assert.Equal(f.Quote.Currency, row.Currency);
        Assert.Equal(100m, row.ReferenceUnitPrice);
        Assert.True(row.IsBelowReference);
    }
}
