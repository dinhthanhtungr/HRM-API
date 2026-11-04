using System.Text.Json;
using HRM.Application.Features.CRM.Quotations.Commands.SaveQuotation;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed partial class QuotationSendWorkflowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApprovalUsesStoredOfferEvenWhenStandardIsReplacedOrUnavailable(bool legacy)
    {
        var f = new Fixture("President");
        var message = f.SeedPending();
        var snapshot = QuotationSendService.ReadPayload(message.PayloadJson)!;
        if (legacy)
        {
            snapshot.CommercialFingerprint = null;
            snapshot.CommercialFingerprintVersion = null;
            snapshot.ComparisonRuleVersion = 0;
            message.PayloadJson = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        var comparison = JsonSerializer.Serialize(snapshot.Comparison);
        var body = message.Body;
        var price = f.Quote.Lines.Single().PriceTiers.Single().CustomerUnitPrice;
        f.Standard.ProductPricingVersionId = Guid.NewGuid();
        f.Standard.PriceTiers.Single().UnitPrice = 999;
        f.Standard.IsActive = false;
        f.ReadStore.Seed("ProductPricingVersions", Array.Empty<ProductPricingVersion>());
        f.ReadStore.Seed("Quotations", f.Quote);
        var detail = new QuotationDetailDto { QuotationId = f.Quote.QuotationId, Status = f.Quote.Status, UpdatedDate = f.Now };
        var untouched = message.PayloadJson;
        await f.Service.EnrichConfirmationAsync(detail, f.Quote.CompanyId, default);
        Assert.True(detail.CanConfirmSent);
        Assert.Equal(untouched, message.PayloadJson);
        Assert.True((await QuickConfirm(f, f.Now, message.InternalMessageId)).Success);
        var confirmed = QuotationSendService.ReadPayload(message.PayloadJson)!;
        Assert.Equal(comparison, JsonSerializer.Serialize(confirmed.Comparison));
        Assert.Equal(snapshot.ComparisonRuleVersion, confirmed.ComparisonRuleVersion);
        Assert.Equal(body, message.Body);
        Assert.Equal(price, f.Quote.Lines.Single().PriceTiers.Single().CustomerUnitPrice);
    }

    [Theory]
    [InlineData("price")]
    [InlineData("commission")]
    [InlineData("sameTotalDifferentCommission")]
    [InlineData("min")]
    [InlineData("max")]
    [InlineData("minInclusive")]
    [InlineData("maxInclusive")]
    [InlineData("toggle")]
    [InlineData("addTier")]
    [InlineData("product")]
    [InlineData("quantity")]
    [InlineData("currency")]
    [InlineData("exchange")]
    [InlineData("payment")]
    [InlineData("delivery")]
    [InlineData("discount")]
    [InlineData("tax")]
    [InlineData("note")]
    [InlineData("terms")]
    [InlineData("customer")]
    public async Task CommercialChangesAreRejectedEvenIfAnUpdateForgotToAdvanceToken(string change)
    {
        var f = new Fixture("President");
        var message = f.SeedPending();
        var line = f.Quote.Lines.Single();
        var tier = line.PriceTiers.Single();
        switch (change)
        {
            case "price": tier.SetPrices(tier.UnitPrice + 1, tier.CommissionAmount); break;
            case "commission": tier.SetPrices(tier.UnitPrice, tier.CommissionAmount + 1); break;
            case "sameTotalDifferentCommission": tier.SetPrices(tier.UnitPrice - 1, tier.CommissionAmount + 1); break;
            case "min": tier.MinQuantity += 1; break;
            case "max": tier.MaxQuantity = 1000; break;
            case "minInclusive": tier.MinInclusive = !tier.MinInclusive; break;
            case "maxInclusive": tier.MaxInclusive = !tier.MaxInclusive; break;
            case "toggle": tier.IsActive = false; break;
            case "addTier": line.PriceTiers.Add(new() { QuotationLinePriceTierId = Guid.NewGuid() }); break;
            case "product": line.ProductId = Guid.NewGuid(); break;
            case "quantity": line.Quantity += 1; break;
            case "currency": f.Quote.Currency = "USD"; break;
            case "exchange": f.Quote.ExchangeRate = 2; break;
            case "payment": f.Quote.PaymentTerms = "30 days"; break;
            case "delivery": f.Quote.DeliveryTerms = "FOB"; break;
            case "discount": f.Quote.DiscountAmount += 1; break;
            case "tax": f.Quote.TaxPercent += 1; break;
            case "note": line.Note = "New customer condition"; break;
            case "terms": f.Quote.Terms.Add(new() { QuotationTermId = Guid.NewGuid(), ValueVi = "New condition" }); break;
            case "customer": f.Quote.CustomerId = Guid.NewGuid(); break;
        }
        f.ReadStore.Seed("Quotations", f.Quote);
        var detail = new QuotationDetailDto { QuotationId = f.Quote.QuotationId, Status = f.Quote.Status, UpdatedDate = f.Now };
        await f.Service.EnrichConfirmationAsync(detail, f.Quote.CompanyId, default);
        Assert.False(detail.CanConfirmSent);
        Assert.Equal(QuotationConfirmationErrors.CommercialChanged, detail.ConfirmSentBlockedReasonCode);
        var stored = message.PayloadJson;
        // The changed customer also fails the existing customer-visibility gate before approval.
        if (change == "customer") return;
        var result = await QuickConfirm(f, f.Now);
        Assert.Equal(QuotationConfirmationErrors.CommercialChanged, Assert.IsType<QuotationConfirmationFailure>(result).Code);
        Assert.Equal(stored, message.PayloadJson);
        Assert.Empty(f.Published);
    }

    [Fact]
    public void CommercialFingerprintIgnoresReferenceAndDecimalScale()
    {
        var f = new Fixture("President");
        var original = QuotationConfirmationCommercialRules.Fingerprint(f.Quote);
        f.Quote.ExchangeRate = 1.0000m;
        f.Quote.Lines.Single().ProductPricingVersionId = Guid.NewGuid();
        f.Quote.QuotationDate = DateTime.SpecifyKind(f.Quote.QuotationDate, DateTimeKind.Utc);
        Assert.Equal(original, QuotationConfirmationCommercialRules.Fingerprint(f.Quote));
    }

    [Fact]
    public async Task LegacyRequestCannotApproveAChangedOfferEvenWithTheLatestPageToken()
    {
        var f = new Fixture("President");
        var message = f.SeedPending();
        var payload = QuotationSendService.ReadPayload(message.PayloadJson)!;
        payload.CommercialFingerprint = null;
        payload.CommercialFingerprintVersion = null;
        message.PayloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var original = message.PayloadJson;
        f.Quote.Lines.Single().PriceTiers.Single().SetPrices(79, 10);
        f.Quote.UpdatedDate = f.Now.AddTicks(10);
        var failure = await QuickConfirm(f, f.Quote.UpdatedDate, message.InternalMessageId);
        Assert.Equal(QuotationConfirmationErrors.RequestOutdated, Assert.IsType<QuotationConfirmationFailure>(failure).Code);
        Assert.Equal(original, message.PayloadJson);
        Assert.Empty(f.Published);
    }

    [Fact]
    public async Task UnchangedPendingSaveDoesNotRepriceOrSupersedeHistoricalRequest()
    {
        var f = new Fixture("President");
        var message = f.SeedPending();
        var original = message.PayloadJson;
        f.Standard.IsActive = false;
        var result = await f.SaveHandler().Handle(new(f.Quote.QuotationId,
            new SaveQuotationRequest { ExpectedUpdatedDate = f.Now, Header = new() }), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(QuotationStatus.PendingSendConfirmation, result.Data!.Status);
        Assert.Equal(f.Now, result.Data.UpdatedDate);
        Assert.Equal(original, message.PayloadJson);
        Assert.Empty(f.Published);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResubmittingIdenticalCustomerTiersPreservesIdsTokenAndPendingOffer(bool legacyEndpoint)
    {
        var f = new Fixture("President");
        var line = f.Quote.Lines.Single();
        var tier = line.PriceTiers.Single();
        tier.QuotationLinePriceTierId = Guid.NewGuid();
        tier.QuantityRangeLabel = "350-600";
        var term = new QuotationTerm { QuotationTermId = Guid.NewGuid(), LabelVi = "Giao hàng", ValueVi = "Tại kho" };
        f.Quote.Terms.Add(term);
        var message = f.SeedPending();
        var stored = message.PayloadJson;
        f.Standard.IsActive = false;
        var request = new UpdateQuotationCustomerPriceTierLineRequest
        {
            QuotationLineId = line.QuotationLineId, Note = line.Note,
            PriceTiers = [new()
            {
                QuantityRangeLabel = tier.QuantityRangeLabel, MinQuantity = tier.MinQuantity, MaxQuantity = tier.MaxQuantity,
                MinInclusive = tier.MinInclusive, MaxInclusive = tier.MaxInclusive,
                UnitPrice = tier.UnitPrice, CommissionAmount = tier.CommissionAmount, SortOrder = tier.SortOrder
            }]
        };
        var result = legacyEndpoint
            ? await f.PriceHandler().Handle(new(f.Quote.QuotationId, new() { ExpectedUpdatedDate = f.Now, Lines = [request] }), default)
            : await f.SaveHandler().Handle(new(f.Quote.QuotationId, new()
            {
                ExpectedUpdatedDate = f.Now, CustomerPriceLines = [request],
                Header = new() { Terms = [new() { LabelVi = term.LabelVi, ValueVi = term.ValueVi }] }
            }), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(QuotationStatus.PendingSendConfirmation, result.Data!.Status);
        Assert.Equal(f.Now, result.Data.UpdatedDate);
        Assert.Same(tier, Assert.Single(line.PriceTiers));
        Assert.Same(term, Assert.Single(f.Quote.Terms, x => x.IsActive));
        Assert.Equal(stored, message.PayloadJson);
        Assert.Empty(f.Published);
    }
}
