using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.Pricing.Authorization;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed partial class QuotationSendWorkflowTests
{
    [Fact]
    public async Task DetailKeepsVndReferencesAndDoesNotConvertSavedUsdTierAgain()
    {
        var f = new Fixture();
        f.Standard.StandardSellingPrice = 70000m;
        var tier = f.Standard.PriceTiers.Single();
        tier.UnitPrice = 70000m;
        tier.ProductPricingVersionId = f.Standard.ProductPricingVersionId;
        f.ReadStore.Seed("ProductPricingTiers", tier);
        var plm = IqcTestStore.CreateContext<IPLMReadDbContext>();
        var source = new ProductPricingRealtimeSourceQueryService(plm.Db, f.Clock, null!, null!, null!);
        var visibility = Stub<IPricingVisibilityService>((_, _) =>
            new PricingAccessDecision(false, false, false, false, false, false, false, false, false));
        var resolver = new QuotationProductTierPricingResolver(
            new ApprovedProductPricingTierReader(f.ReadDb, f.Clock, Options.Create(new QuotationFeatureOptions())),
            new SystemCalculatedProductPricingTierResolver(source, f.Clock),
            new LatestQuotationPricingTierReader(f.ReadDb),
            new StandardPriceRealtimeComparisonQueryService(source, f.Clock, new QuotationFeatureOptions()), visibility);
        var service = new QuotationTierPriceReferenceService(resolver);
        var line = new QuotationLineDto { ProductId = f.Standard.ProductId };
        var detail = new QuotationDetailDto
        {
            Currency = "USD", ExchangeRate = 25000m, Lines = [line]
        };

        await service.EnrichAsync(detail, f.Quote.CompanyId, f.ReadDb.Quotations, default);
        Assert.Equal("VND", line.ApprovedPricing!.Currency);
        Assert.Equal(70000m, Assert.Single(line.ApprovedPricing.PriceTiers).UnitPrice);
        Assert.Equal(2.8m, Assert.Single(line.PriceTiers).UnitPrice);
        Assert.False(Assert.Single(line.PriceTiers).IsSnapshot);

        line.PriceTiers = [new QuotationLinePriceTierDto
        {
            IsSnapshot = true, UnitPrice = 2.6m, CustomerUnitPrice = 2.6m
        }];
        await service.EnrichAsync(detail, f.Quote.CompanyId, f.ReadDb.Quotations, default);
        Assert.Equal(2.6m, Assert.Single(line.PriceTiers).UnitPrice);
        Assert.True(Assert.Single(line.PriceTiers).IsSnapshot);
    }

    [Theory]
    [InlineData("USD", 25000, 2.6, 2.8)]
    [InlineData("VND", 25000, 65000, 70000)]
    public async Task SendConvertsVndStandardOnceAndPreservesCustomerSnapshot(
        string currency, decimal rate, decimal customerPrice, decimal expectedStandard)
    {
        var f = new Fixture();
        f.Quote.Currency = currency;
        f.Quote.ExchangeRate = rate;
        f.Standard.Currency = "VND";
        f.Standard.PriceTiers.Single().UnitPrice = 70000m;
        var customer = f.Quote.Lines.Single().PriceTiers.Single();
        customer.SetPrices(customerPrice, 0m);

        var result = await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new MarkQuotationSentRequest { Note = "Discount" }, null, default);

        Assert.True(result.Success, result.Message);
        var comparison = Assert.Single(QuotationSendService.ReadPayload(
            Assert.Single(f.Published).PayloadJson)!.Comparison);
        Assert.Equal(expectedStandard, comparison.StandardUnitPrice);
        Assert.Equal(customerPrice, comparison.CustomerUnitPrice);
        Assert.Equal(currency, comparison.Currency);
        Assert.Equal(customerPrice, customer.UnitPrice);
        Assert.Equal(70000m, f.Standard.PriceTiers.Single().UnitPrice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SendRejectsInvalidForeignExchangeRate(decimal rate)
    {
        var f = new Fixture();
        f.Quote.Currency = "USD";
        f.Quote.ExchangeRate = rate;
        var result = await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new MarkQuotationSentRequest { Note = "Discount" }, null, default);
        Assert.False(result.Success);
        Assert.Empty(f.Published);
    }
}
