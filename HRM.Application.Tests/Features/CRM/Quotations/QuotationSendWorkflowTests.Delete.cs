using HRM.Application.Features.CRM.Quotations.Commands.DeleteQuotation;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed partial class QuotationSendWorkflowTests
{
    private static DeleteQuotationCommandHandler DeleteHandler(Fixture f) => new(
        f.ReadDb, f.WriteDb, f.Visibility, f.MutationLock, new QuotationAtomicSaveService(f.WriteDb), f.Clock);

    [Theory]
    [InlineData("SaleUser")]
    [InlineData("Developer")]
    public async Task DeleteDraftPreservesPricesAndHistory(string role)
    {
        var f = new Fixture(role); f.Quote.UpdatedDate = f.Now;
        var line = f.Quote.Lines.Single(); var tier = line.PriceTiers.Single();
        var result = await DeleteHandler(f).Handle(new(f.Quote.QuotationId, f.Now), default);
        Assert.True(result.Success, result.Message);
        Assert.False(f.Quote.IsActive);
        Assert.Same(line, f.Quote.Lines.Single()); Assert.Same(tier, line.PriceTiers.Single());
        Assert.Empty(f.WriteStore.Removed); Assert.Empty(f.Published);
        Assert.True(f.WriteStore.Transaction.Committed);
        Assert.True(f.Quote.UpdatedDate > f.Now);
    }

    [Theory]
    [InlineData(10)] [InlineData(20)] [InlineData(30)] [InlineData(40)] [InlineData(50)]
    [InlineData(60)] [InlineData(70)] [InlineData(80)] [InlineData(90)]
    public async Task DeleteRejectsNonDraftEvenForDeveloper(int status)
    {
        var f = new Fixture("Developer"); f.Quote.UpdatedDate = f.Now; f.Quote.Status = (QuotationStatus)status;
        var result = await DeleteHandler(f).Handle(new(f.Quote.QuotationId, f.Now), default);
        Assert.False(result.Success); Assert.True(f.Quote.IsActive); Assert.Equal(0, f.WriteStore.Saves);
    }

    [Theory]
    [InlineData("missing-token")] [InlineData("stale-token")] [InlineData("foreign-company")]
    [InlineData("foreign-customer")] [InlineData("inactive")] [InlineData("empty-id")]
    [InlineData("already-sent")]
    public async Task DeleteInvalidRequestDoesNotWrite(string scenario)
    {
        var f = new Fixture(); f.Quote.UpdatedDate = f.Now;
        if (scenario == "foreign-company") f.Quote.CompanyId = Guid.NewGuid();
        if (scenario == "foreign-customer") f.Quote.CustomerId = Guid.NewGuid();
        if (scenario == "inactive") f.Quote.IsActive = false;
        if (scenario == "already-sent") f.Quote.SentDate = f.Now;
        var result = await DeleteHandler(f).Handle(new(
            scenario == "empty-id" ? Guid.Empty : f.Quote.QuotationId,
            scenario == "missing-token" ? null : scenario == "stale-token" ? f.Now.AddSeconds(-1) : f.Now), default);
        Assert.False(result.Success); Assert.Equal(0, f.WriteStore.Saves);
        Assert.Empty(f.WriteStore.Removed);
    }

    [Fact]
    public async Task ConcurrentDeleteCommitsOnlyOnce()
    {
        var f = new Fixture(); f.Quote.UpdatedDate = f.Now;
        var handler = DeleteHandler(f); var command = new DeleteQuotationCommand(f.Quote.QuotationId, f.Now);
        var results = await Task.WhenAll(handler.Handle(command, default), handler.Handle(command, default));
        Assert.Single(results, x => x.Success); Assert.Equal(1, f.WriteStore.Saves);
    }

    [Fact]
    public async Task DeleteDatabaseConcurrencyFailureRollsBack()
    {
        var f = new Fixture(); f.Quote.UpdatedDate = f.Now; f.WriteStore.FailWithConcurrency = true;
        var result = await DeleteHandler(f).Handle(new(f.Quote.QuotationId, f.Now), default);
        Assert.False(result.Success); Assert.True(f.WriteStore.Transaction.RolledBack);
    }

    [Fact]
    public async Task DeleteLegacyDraftUsesCreatedDateWhenNeverUpdated()
    {
        var f = new Fixture(); f.Quote.CreatedDate = f.Now; f.Quote.UpdatedDate = null;
        var result = await DeleteHandler(f).Handle(new(f.Quote.QuotationId, f.Now), default);
        Assert.True(result.Success, result.Message); Assert.False(f.Quote.IsActive);
    }
}
