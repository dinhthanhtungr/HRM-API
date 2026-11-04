using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.AuditSchema;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed partial class QuotationSendWorkflowTests
{
    private static SampleRequest LinkSample(Fixture f, string status = "InProgress", DateTime? previous = null)
    {
        var line = f.Quote.Lines.Single();
        var sample = new SampleRequest
        {
            SampleRequestId = Guid.NewGuid(), CompanyId = f.Quote.CompanyId,
            CustomerId = f.Quote.CustomerId, ProductId = line.ProductId,
            IsActive = true, Status = status, RealPriceQuoteDate = previous
        };
        line.SampleRequestId = sample.SampleRequestId;
        f.WriteStore.Seed("SampleRequests", sample);
        return sample;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SentQuotationUpdatesLinkedOpenSampleToLatestDate(bool hadPreviousDate)
    {
        var f = new Fixture();
        f.Quote.Lines.Single().PriceTiers.Single().SetPrices(90, 10);
        var sample = LinkSample(f, previous: hadPreviousDate ? f.Now.AddDays(-3) : null);
        var result = await f.Service.ExecuteAsync(f.Quote.QuotationId, new(), null, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(f.Quote.SentDate, sample.RealPriceQuoteDate);
        Assert.Equal("InProgress", sample.Status);
        Assert.Single(f.WriteStore.Added.OfType<AuditLog>(), x => x.RecordId == sample.SampleRequestId);
        Assert.Empty(f.WriteStore.Added.OfType<SampleRequestSampleTrial>());
        Assert.True((await f.Service.ExecuteAsync(f.Quote.QuotationId, new(), null, default)).Success);
        Assert.Single(f.WriteStore.Added.OfType<AuditLog>(), x => x.RecordId == sample.SampleRequestId);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    [InlineData("WrongCompany")]
    [InlineData("WrongCustomer")]
    [InlineData("WrongProduct")]
    [InlineData("Inactive")]
    [InlineData("FutureDate")]
    public async Task SentQuotationDoesNotChangeUnrelatedOrClosedSample(string scenario)
    {
        var f = new Fixture();
        f.Quote.Lines.Single().PriceTiers.Single().SetPrices(90, 10);
        var sample = LinkSample(f, scenario is "Completed" or "Cancelled" ? scenario : "InProgress", f.Now.AddDays(-2));
        if (scenario == "WrongCompany") sample.CompanyId = Guid.NewGuid();
        if (scenario == "WrongCustomer") sample.CustomerId = Guid.NewGuid();
        if (scenario == "WrongProduct") sample.ProductId = Guid.NewGuid();
        if (scenario == "Inactive") sample.IsActive = false;
        if (scenario == "FutureDate") sample.RealPriceQuoteDate = f.Now.AddDays(1);
        var original = sample.RealPriceQuoteDate;
        Assert.True((await f.Service.ExecuteAsync(f.Quote.QuotationId, new(), null, default)).Success);
        Assert.Equal(original, sample.RealPriceQuoteDate);
        Assert.Empty(f.WriteStore.Added.OfType<AuditLog>());
    }

    [Fact]
    public async Task BoardConfirmationKeepsDateAlreadyRecordedBySale()
    {
        var f = new Fixture("President");
        var sample = LinkSample(f, previous: f.Now.AddDays(-1));
        var previous = sample.RealPriceQuoteDate;
        var message = f.SeedPending();
        var result = await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new() { ExpectedUpdatedDate = f.Quote.UpdatedDate }, message.InternalMessageId, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(previous, sample.RealPriceQuoteDate);
        Assert.Empty(f.WriteStore.Added.OfType<AuditLog>());
    }

    [Fact]
    public async Task SaleMarkSentUpdatesSampleDateEvenWhenQuotationAwaitsConfirmation()
    {
        var f = new Fixture();
        var sample = LinkSample(f);
        Assert.True((await f.Service.ExecuteAsync(f.Quote.QuotationId, new() { Note = "Needs confirmation" }, null, default)).Success);
        Assert.Equal(f.Now, sample.RealPriceQuoteDate);
        Assert.Null(f.Quote.SentDate);
        Assert.Single(f.WriteStore.Added.OfType<AuditLog>());
    }
}
