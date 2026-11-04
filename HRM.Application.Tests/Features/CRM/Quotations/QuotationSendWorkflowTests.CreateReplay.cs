using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Features.CRM.Quotations.Commands.CreateQuotation;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed partial class QuotationSendWorkflowTests
{
    [Fact]
    public async Task ChangedStandardPreventsAllHeaderWritesUntilExplicitAcceptance()
    {
        var f = new Fixture(); f.Quote.UpdatedDate = f.Now;
        f.Quote.Status = HRM.Domain.Enums.CustomerEnum.QuotationStatus.Approved;
        f.Standard.Status = HRM.Domain.Enums.CustomerEnum.ProductPricingStatus.Superseded;
        f.Standard.Currency = "VND"; f.Standard.StandardSellingPrice = 100; f.Standard.Version = 1;
        f.Quote.Lines.Single().ProductPricingVersionId = f.Standard.ProductPricingVersionId;
        var current = new ProductPricingVersion
        {
            ProductPricingVersionId = Guid.NewGuid(), CompanyId = f.Quote.CompanyId, ProductId = f.Standard.ProductId,
            Currency = "VND", StandardSellingPrice = 120, Version = 2,
            Status = HRM.Domain.Enums.CustomerEnum.ProductPricingStatus.Approved
        };
        f.ReadStore.Seed("ProductPricingVersions", f.Standard, current);
        var first = await f.SaveHandler().Handle(new(f.Quote.QuotationId, new()
        { ExpectedUpdatedDate = f.Now, Header = new() { Note = "not saved before acceptance" } }), default);
        var failure = Assert.IsType<QuotationStandardReferenceFailure>(first);
        Assert.Equal(0, f.WriteStore.Saves);
        Assert.Equal("Existing quotation note", f.Quote.Note);
        var change = Assert.Single(failure.Changes);
        var second = await f.SaveHandler().Handle(new(f.Quote.QuotationId, new()
        {
            ExpectedUpdatedDate = f.Now, Header = new() { Note = "not saved before acceptance" },
            AcceptedStandardPriceChanges = new Dictionary<Guid, string> { [change.QuotationLineId] = change.AcceptanceToken! }
        }), default);
        Assert.True(second.Success, second.Message);
        Assert.Equal(1, f.WriteStore.Saves);
        Assert.Equal("not saved before acceptance", f.Quote.Note);
    }

    [Fact]
    public async Task ConcurrentCreateAndNetworkReplayReturnOneQuotationWithoutGeneratingAnotherCode()
    {
        var f = new Fixture();
        f.WriteStore.ReflectAddedOnSave = true;
        var read = Stub<ICRMReadDbContext>((method, _) => method switch
        {
            "get_Quotations" => f.WriteDb.Quotations,
            "get_Customers" => f.ReadDb.Customers,
            "get_Products" => f.ReadDb.Products,
            "get_ProductPricingVersions" => f.ReadDb.ProductPricingVersions,
            "get_SampleRequests" => f.ReadDb.SampleRequests,
            _ => throw new NotSupportedException(method)
        });
        var generated = 0;
        var ids = Stub<IExternalIdService>((_, _) => { generated++; return Task.FromResult("TEST-CREATE"); });
        var handler = new CreateQuotationCommandHandler(read, f.WriteDb, f.Visibility, ids, f.Clock,
            new QuotationLineBuilder(read), f.MutationLock);
        var request = new CreateQuotationRequest { IdempotencyKey = Guid.NewGuid(), CustomerId = f.Quote.CustomerId,
            Currency = "VND", ExchangeRate = 1, QuotationDate = f.Now, Note = "replay test" };
        var results = await Task.WhenAll(handler.Handle(new(request), default), handler.Handle(new(request), default));
        Assert.All(results, result => Assert.True(result.Success, result.Message));
        Assert.Equal(results[0].Data!.QuotationId, results[1].Data!.QuotationId);
        Assert.Equal(1, generated); Assert.Equal(1, f.WriteStore.Saves);
        Assert.Single(f.WriteStore.Added.OfType<Quotation>());
        var replayAfterLostResponse = await handler.Handle(new(request), default);
        Assert.True(replayAfterLostResponse.Success);
        Assert.Equal(results[0].Data!.QuotationId, replayAfterLostResponse.Data!.QuotationId);
        var changed = new CreateQuotationRequest { IdempotencyKey = request.IdempotencyKey, CustomerId = request.CustomerId,
            Currency = "VND", ExchangeRate = 1, QuotationDate = f.Now, Note = "different content" };
        Assert.IsType<QuotationCreateConflictFailure>(await handler.Handle(new(changed), default));
        Assert.Equal(1, f.WriteStore.Saves);
    }

    [Fact]
    public async Task NoteOnlySaveDoesNotValidateOrReplaceLegacyPriceTiers()
    {
        var f = new Fixture(); f.Quote.UpdatedDate = f.Now;
        var line = f.Quote.Lines.Single(); var tier = line.PriceTiers.Single();
        tier.MinQuantity = 500; tier.MaxQuantity = 1; // legacy invalid data, unchanged and not submitted
        var result = await f.SaveHandler().Handle(new(f.Quote.QuotationId, new SaveQuotationRequest
        {
            ExpectedUpdatedDate = f.Now,
            CustomerPriceLines = [new() { QuotationLineId = line.QuotationLineId, NoteOnly = true, Note = "new line note" }]
        }), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal("new line note", line.Note);
        Assert.Same(tier, line.PriceTiers.Single());
        Assert.Equal(1, f.WriteStore.Saves);
    }
}
