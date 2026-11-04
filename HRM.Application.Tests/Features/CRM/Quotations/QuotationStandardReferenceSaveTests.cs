using System.Text.Json;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationStandardReferenceSaveTests
{
    private sealed class Fixture
    {
        internal readonly Guid Company = Guid.NewGuid();
        internal readonly Guid Product = Guid.NewGuid();
        internal readonly Quotation Quote;
        internal readonly ProductPricingVersion Old;
        internal readonly ProductPricingVersion Current;
        internal readonly IqcTestStore Store;
        internal readonly QuotationStandardReferenceSaveService Service;
        internal Fixture()
        {
            var context = IqcTestStore.CreateContext<ICRMReadDbContext>(); Store = context.Store; Service = new(context.Db);
            Old = Version(1, ProductPricingStatus.Superseded); Current = Version(2, ProductPricingStatus.Approved);
            Quote = new() { QuotationId = Guid.NewGuid(), CompanyId = Company, Lines = [new()
            {
                QuotationLineId = Guid.NewGuid(), ProductId = Product, ProductExternalIdSnapshot = "TEST",
                ProductPricingVersionId = Old.ProductPricingVersionId, PriceMode = QuotationLinePriceMode.ApprovedPricingEditable,
                UnitPrice = 77m, IsActive = true, Note = "customer note"
            }] };
            Seed();
        }
        internal ProductPricingVersion Version(int number, ProductPricingStatus status) => new()
        {
            ProductPricingVersionId = Guid.NewGuid(), CompanyId = Company, ProductId = Product, Currency = "VND",
            Version = number, IsActive = true, Status = status, StandardSellingPrice = 100m,
            PriceTiers = [new() { UnitPrice = 100m, MinQuantity = 1m, MaxQuantity = 100m, QuantityRangeLabel = "1–100" }]
        };
        internal void Seed(params ProductPricingVersion[] extra) => Store.Seed("ProductPricingVersions", new[] { Old, Current }.Concat(extra).ToArray());
    }

    [Fact]
    public async Task SameContentRebindsWithoutAcknowledgmentAndPreservesCustomerInputs()
    {
        var f = new Fixture(); f.Current.StandardSellingPrice = 100.000000m;
        f.Current.PriceTiers.Single().UnitPrice = 100.00m;
        f.Current.PriceTiers.Single().QuantityRangeLabel = "renamed display label";
        f.Current.PriceTiers.Single().SortOrder = 5;
        var changes = await f.Service.PrepareAsync(f.Quote, new(), default);
        Assert.False(Assert.Single(changes).RequiresAcceptance);
        Assert.Equal(f.Current.ProductPricingVersionId, f.Quote.Lines.Single().ProductPricingVersionId);
        Assert.Equal(77m, f.Quote.Lines.Single().UnitPrice);
        Assert.Equal("customer note", f.Quote.Lines.Single().Note);
        Assert.Equal(0, f.Store.Saves);
    }

    [Theory]
    [InlineData("price")]
    [InlineData("tierPrice")]
    [InlineData("boundary")]
    [InlineData("inclusive")]
    [InlineData("active")]
    public async Task RealChangesRequireAcceptanceAndOldAcceptanceCannotApproveAnotherRevision(string field)
    {
        var f = new Fixture(); var tier = f.Current.PriceTiers.Single();
        switch (field)
        {
            case "price": f.Current.StandardSellingPrice = 110; break;
            case "tierPrice": tier.UnitPrice = 110; break;
            case "boundary": tier.MaxQuantity = 200; break;
            case "inclusive": tier.MinInclusive = false; break;
            case "active": tier.IsActive = false; break;
        }
        var change = Assert.Single(await f.Service.PrepareAsync(f.Quote, new(), default));
        Assert.True(change.RequiresAcceptance);
        Assert.Equal(f.Old.ProductPricingVersionId, f.Quote.Lines.Single().ProductPricingVersionId);
        var accepted = new SaveQuotationRequest { AcceptedStandardPriceChanges = new Dictionary<Guid, string>
        { [change.QuotationLineId] = change.AcceptanceToken! } };
        f.Current.StandardSellingPrice = 120;
        Assert.True(Assert.Single(await f.Service.PrepareAsync(f.Quote, accepted, default)).RequiresAcceptance);
        var latest = Assert.Single(await f.Service.PrepareAsync(f.Quote, new(), default));
        var latestAccepted = new SaveQuotationRequest { AcceptedStandardPriceChanges = new Dictionary<Guid, string>
        { [latest.QuotationLineId] = latest.AcceptanceToken! } };
        Assert.False(Assert.Single(await f.Service.PrepareAsync(f.Quote, latestAccepted, default)).RequiresAcceptance);
        Assert.Equal(77m, f.Quote.Lines.Single().UnitPrice);
    }

    [Fact]
    public async Task AlreadyViewedCurrentVersionDoesNotAskAgainAfterReopening()
    {
        var f = new Fixture(); f.Current.StandardSellingPrice = 120;
        Assert.Empty(await f.Service.PrepareAsync(f.Quote, new()
        { ObservedStandardPriceVersions = new Dictionary<Guid, Guid> { [f.Quote.Lines.Single().QuotationLineId] = f.Current.ProductPricingVersionId } }, default));
        Assert.Equal(f.Current.ProductPricingVersionId, f.Quote.Lines.Single().ProductPricingVersionId);
    }

    [Fact]
    public async Task MissingCurrentPricePreservesOldReferenceForHistoryAndDoesNotSubstituteForeignPrice()
    {
        var f = new Fixture(); f.Current.CompanyId = Guid.NewGuid();
        var change = Assert.Single(await f.Service.PrepareAsync(f.Quote, new(), default));
        Assert.True(change.IsUnavailable); Assert.False(change.RequiresAcceptance);
        Assert.Null(change.CurrentVersionId);
        Assert.Equal(f.Old.ProductPricingVersionId, f.Quote.Lines.Single().ProductPricingVersionId);
    }

    [Fact]
    public async Task SameProductDraftReplacementRebindsOnlyReferenceAndKeepsTierZero()
    {
        var f = new Fixture(); var line = f.Quote.Lines.Single();
        var incoming = new QuotationLineRequest { QuotationLineId = line.QuotationLineId, ProductId = f.Product,
            ProductPricingVersionId = f.Old.ProductPricingVersionId, PriceMode = line.PriceMode,
            PriceTiers = [new() { UnitPrice = 0, QuantityRangeLabel = "all" }] };
        await f.Service.PrepareAsync(f.Quote, new() { Lines = [incoming] }, default);
        Assert.Equal(f.Current.ProductPricingVersionId, incoming.ProductPricingVersionId);
        Assert.Equal(0, incoming.PriceTiers.Single().UnitPrice);
    }

    [Fact]
    public void MissingOrNullTierPriceIsRejectedDuringJsonBindingButExplicitZeroIsValid()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<QuotationLinePriceTierRequest>("{}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<QuotationLinePriceTierRequest>("{\"UnitPrice\":null}"));
        Assert.Equal(0, JsonSerializer.Deserialize<QuotationLinePriceTierRequest>("{\"UnitPrice\":0}")!.UnitPrice);
    }
}
