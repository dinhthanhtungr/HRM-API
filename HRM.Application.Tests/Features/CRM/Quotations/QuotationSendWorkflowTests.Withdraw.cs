using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed partial class QuotationSendWorkflowTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("expired")]
    [InlineData("missing")]
    [InlineData("legacy")]
    [InlineData("no-message")]
    public async Task WithdrawRestoresDraftWithoutChangingCustomerPricesOrNeedingStandard(string scenario)
    {
        var f = new Fixture();
        var old = scenario == "no-message" ? null : f.SeedPending();
        f.Quote.Status = QuotationStatus.PendingSendConfirmation;
        f.Quote.UpdatedDate = f.Now;
        var customerTier = f.Quote.Lines.Single().PriceTiers.Single();
        if (scenario == "expired") f.Standard.ApprovedAt = f.Now.AddDays(-31);
        if (scenario == "missing") f.Standard.IsActive = false;
        if (scenario == "legacy")
        {
            var payload = QuotationSendService.ReadPayload(old!.PayloadJson)!;
            payload.ComparisonRuleVersion = 0;
            old.PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        }
        var result = await f.Service.WithdrawConfirmationAsync(f.Quote.QuotationId,
            new() { ExpectedUpdatedDate = f.Now, Reason = "Cần sửa\nLý do nhiều dòng" }, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(QuotationStatus.Draft, result.Data!.Status);
        Assert.True(result.Data.UpdatedDate > f.Now);
        Assert.Null(f.Quote.SentDate);
        Assert.Same(customerTier, f.Quote.Lines.Single().PriceTiers.Single());
        if (old is not null)
        {
            var payload = QuotationSendService.ReadPayload(old.PayloadJson)!;
            Assert.Equal(QuotationSendConfirmationPayload.SupersededStatus, payload.Status);
            Assert.False(old.IsUrgent);
            Assert.Equal(90, Assert.Single(payload.Comparison).CustomerUnitPrice);
        }
        Assert.Contains("Lý do nhiều dòng", Assert.Single(f.MailStore.Added.OfType<InternalMessage>()).Body);
        Assert.True(f.WriteStore.Transaction.Committed);
    }

    [Theory]
    [InlineData(0)] [InlineData(10)] [InlineData(20)] [InlineData(30)]
    [InlineData(40)] [InlineData(50)] [InlineData(60)] [InlineData(70)] [InlineData(80)]
    public async Task WithdrawRejectsEveryNonPendingStatus(int status)
    {
        var f = new Fixture(); f.Quote.Status = (QuotationStatus)status; f.Quote.UpdatedDate = f.Now;
        var result = await f.Service.WithdrawConfirmationAsync(f.Quote.QuotationId,
            new() { ExpectedUpdatedDate = f.Now, Reason = "Withdraw" }, default);
        Assert.False(result.Success); Assert.Empty(f.Published);
        Assert.Equal((QuotationStatus)status, f.Quote.Status);
    }

    [Theory]
    [InlineData("empty-id")] [InlineData("missing-token")] [InlineData("stale-token")]
    [InlineData("empty-reason")] [InlineData("long-reason")] [InlineData("foreign-company")]
    [InlineData("inactive")] [InlineData("database-conflict")]
    public async Task WithdrawInvalidRequestsDoNotPublish(string scenario)
    {
        var f = new Fixture(); f.SeedPending();
        if (scenario == "foreign-company") f.Quote.CompanyId = Guid.NewGuid();
        if (scenario == "inactive") f.Quote.IsActive = false;
        if (scenario == "database-conflict") f.WriteStore.FailWithConcurrency = true;
        var result = await f.Service.WithdrawConfirmationAsync(scenario == "empty-id" ? Guid.Empty : f.Quote.QuotationId,
            new()
            {
                ExpectedUpdatedDate = scenario == "missing-token" ? null : scenario == "stale-token" ? f.Now.AddSeconds(-1) : f.Now,
                Reason = scenario == "empty-reason" ? "  " : scenario == "long-reason" ? new string('a', 501) : "Withdraw"
            }, default);
        Assert.False(result.Success); Assert.Empty(f.Published);
    }

    [Fact]
    public async Task ConcurrentWithdrawAndConfirmAllowOnlyOneTransition()
    {
        var f = new Fixture("President"); var message = f.SeedPending();
        var withdraw = f.Service.WithdrawConfirmationAsync(f.Quote.QuotationId, new() { ExpectedUpdatedDate = f.Now, Reason = "Withdraw" }, default);
        var confirm = f.Service.ExecuteAsync(f.Quote.QuotationId, new() { ExpectedUpdatedDate = f.Now }, message.InternalMessageId, default);
        await Task.WhenAll(withdraw, confirm);
        var withdrawn = await withdraw;
        var confirmed = await confirm;
        Assert.NotEqual(withdrawn.Success, confirmed.Success);
        Assert.True(withdrawn.Success || confirmed.Success);
    }

    [Fact]
    public async Task OldRuleSnapshotCanStillBeWithdrawnWithoutChangingComparison()
    {
        var f = new Fixture("President"); var message = f.SeedPending();
        var payload = QuotationSendService.ReadPayload(message.PayloadJson)!;
        payload.ComparisonRuleVersion = 2;
        message.PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.True((await f.Service.WithdrawConfirmationAsync(f.Quote.QuotationId, new() { ExpectedUpdatedDate = f.Now, Reason = "New rules" }, default)).Success);
    }

    [Fact]
    public async Task WithdrawSupersedesEveryPendingRequestAndRejectsRepeatedToken()
    {
        var f = new Fixture("President");
        var first = f.SeedPending();
        var second = f.SeedPending();
        f.MailStore.Seed("InternalMessages", first, second);
        var request = new WithdrawQuotationPricingRequest { ExpectedUpdatedDate = f.Now, Reason = new string('a', 500) };
        Assert.True((await f.Service.WithdrawConfirmationAsync(f.Quote.QuotationId, request, default)).Success);
        Assert.All(new[] { first, second }, message =>
            Assert.Equal(QuotationSendConfirmationPayload.SupersededStatus, QuotationSendService.ReadPayload(message.PayloadJson)!.Status));
        var publications = f.Published.Count;
        Assert.False((await f.Service.WithdrawConfirmationAsync(f.Quote.QuotationId, request, default)).Success);
        Assert.Equal(publications, f.Published.Count);
        Assert.False((await f.Service.ExecuteAsync(f.Quote.QuotationId, new() { ExpectedUpdatedDate = f.Quote.UpdatedDate }, first.InternalMessageId, default)).Success);
    }
}
