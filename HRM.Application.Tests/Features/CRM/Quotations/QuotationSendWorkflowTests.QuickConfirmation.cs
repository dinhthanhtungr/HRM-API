using System.Text.Json;
using HRM.Application.Features.CRM.Quotations.Commands.ConfirmQuotationSent;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed partial class QuotationSendWorkflowTests
{
    private static Task<HRM.Application.Commons.Models.OperationResult> QuickConfirm(Fixture f,
        DateTime? token, Guid? messageId = null)
        => new ConfirmQuotationSentCommandHandler(f.Service).Handle(
            new(f.Quote.QuotationId, new() { ExpectedUpdatedDate = token, RequestMessageId = messageId }), default);

    [Theory]
    [InlineData("President")]
    [InlineData("Developer")]
    public async Task QuickConfirmResolvesCurrentRequestAndPreservesCustomerPrice(string role)
    {
        var f = new Fixture(role);
        var message = f.SeedPending();
        var tier = f.Quote.Lines.Single().PriceTiers.Single();
        var price = tier.UnitPrice;
        var commission = tier.CommissionAmount;
        var result = await QuickConfirm(f, f.Now);
        Assert.True(result.Success, result.Message);
        Assert.Equal(QuotationStatus.Sent, f.Quote.Status);
        Assert.True(f.Quote.UpdatedDate > f.Now);
        Assert.Equal(price, tier.UnitPrice);
        Assert.Equal(commission, tier.CommissionAmount);
        Assert.Equal("confirmed", QuotationSendService.ReadPayload(message.PayloadJson)!.Status);
    }

    [Fact]
    public async Task QuickConfirmStillSupportsNotificationHubMessageId()
    {
        var f = new Fixture("Developer");
        var message = f.SeedPending();
        Assert.True((await QuickConfirm(f, f.Now, message.InternalMessageId)).Success);
    }

    [Theory]
    [InlineData("missing", QuotationConfirmationErrors.NoPendingRequest)]
    [InlineData("superseded", QuotationConfirmationErrors.NoPendingRequest)]
    [InlineData("confirmed", QuotationConfirmationErrors.NoPendingRequest)]
    [InlineData("deleted", QuotationConfirmationErrors.NoPendingRequest)]
    [InlineData("foreign", QuotationConfirmationErrors.NoPendingRequest)]
    [InlineData("otherQuotation", QuotationConfirmationErrors.NoPendingRequest)]
    [InlineData("multiple", QuotationConfirmationErrors.AmbiguousRequests)]
    [InlineData("oldToken", QuotationConfirmationErrors.RequestOutdated)]
    [InlineData("wrongId", QuotationConfirmationErrors.RequestOutdated)]
    public async Task QuickConfirmDoesNotSelectAnOldOrAmbiguousRequest(string scenario, string code)
    {
        var f = new Fixture("President");
        var message = f.SeedPending();
        var payload = QuotationSendService.ReadPayload(message.PayloadJson)!;
        switch (scenario)
        {
            case "missing": f.MailStore.Seed("InternalMessages", Array.Empty<InternalMessage>()); break;
            case "superseded": payload.Status = "superseded"; break;
            case "confirmed": payload.Status = "confirmed"; break;
            case "deleted": message.IsDeleted = true; break;
            case "foreign": f.Conversation.CompanyId = Guid.NewGuid(); break;
            case "otherQuotation": f.Conversation.RelatedId = Guid.NewGuid(); break;
            case "multiple":
                var second = f.SeedPending();
                f.MailStore.Seed("InternalMessages", message, second); break;
            case "oldRule": payload.ComparisonRuleVersion = 2; break;
            case "oldToken": payload.ExpectedUpdatedDate = f.Now.AddSeconds(-1); break;
        }
        message.PayloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var stored = message.PayloadJson;
        var result = await QuickConfirm(f, f.Now, scenario == "wrongId" ? Guid.NewGuid() : null);
        Assert.Equal(code, Assert.IsType<QuotationConfirmationFailure>(result).Code);
        Assert.Equal(QuotationStatus.PendingSendConfirmation, f.Quote.Status);
        Assert.Equal(stored, message.PayloadJson);
        Assert.Empty(f.Published);
    }

    [Theory]
    [InlineData("SaleUser", QuotationConfirmationErrors.Forbidden)]
    [InlineData("Leader", QuotationConfirmationErrors.Forbidden)]
    [InlineData("ACCUser", QuotationConfirmationErrors.Forbidden)]
    public async Task QuickConfirmEnforcesCapabilityWithoutMessageId(string role, string code)
    {
        var f = new Fixture(role);
        f.SeedPending();
        Assert.Equal(code, Assert.IsType<QuotationConfirmationFailure>(await QuickConfirm(f, f.Now)).Code);
        Assert.Empty(f.Published);
    }

    [Fact]
    public async Task QuickConfirmRequiresTokenAndRejectsChangedQuotation()
    {
        var f = new Fixture("President");
        f.SeedPending();
        Assert.Equal(QuotationConfirmationErrors.TokenRequired,
            Assert.IsType<QuotationConfirmationFailure>(await QuickConfirm(f, null)).Code);
        Assert.Equal(QuotationConfirmationErrors.Conflict,
            Assert.IsType<QuotationConfirmationFailure>(await QuickConfirm(f, f.Now.AddTicks(-10))).Code);
        f.Quote.Lines.Single().PriceTiers.Single().SetPrices(99, 2);
        f.Quote.UpdatedDate = f.Now.AddTicks(10);
        Assert.Equal(QuotationConfirmationErrors.Conflict,
            Assert.IsType<QuotationConfirmationFailure>(await QuickConfirm(f, f.Now)).Code);
        Assert.Empty(f.Published);
    }

    [Theory]
    [InlineData("state", QuotationConfirmationErrors.NotPending)]
    [InlineData("company", QuotationConfirmationErrors.NotFound)]
    [InlineData("databaseConflict", QuotationConfirmationErrors.Conflict)]
    public async Task QuickConfirmRechecksStatePricingAndDatabaseConcurrency(string change, string code)
    {
        var f = new Fixture("President");
        f.SeedPending();
        if (change == "snapshot") f.Standard.PriceTiers.Single().UnitPrice = 110;
        if (change == "expired") f.Standard.ApprovedAt = f.Now.AddDays(-31);
        if (change == "state") f.Quote.Status = QuotationStatus.Approved;
        if (change == "company") f.Quote.CompanyId = Guid.NewGuid();
        if (change == "databaseConflict") f.WriteStore.FailWithConcurrency = true;
        Assert.Equal(code, Assert.IsType<QuotationConfirmationFailure>(await QuickConfirm(f, f.Now)).Code);
        Assert.Empty(f.Published);
    }

    [Fact]
    public async Task ConcurrentQuickConfirmationsAllowExactlyOneTransition()
    {
        var f = new Fixture("President");
        f.SeedPending();
        var results = await Task.WhenAll(QuickConfirm(f, f.Now), QuickConfirm(f, f.Now));
        Assert.Single(results, x => x.Success);
        Assert.Equal(QuotationConfirmationErrors.Conflict,
            Assert.IsType<QuotationConfirmationFailure>(Assert.Single(results, x => !x.Success)).Code);
        Assert.Single(f.Quote.StatusHistories);
    }

    [Theory]
    [InlineData("President", true)]
    [InlineData("Developer", true)]
    [InlineData("SaleUser", false)]
    public async Task ConfirmationDetailIsViewerScopedAndPreservesStoredSnapshot(string role, bool allowed)
    {
        var f = new Fixture(role);
        var message = f.SeedPending();
        f.ReadStore.Seed("Quotations", f.Quote);
        var dto = new QuotationDetailDto { QuotationId = f.Quote.QuotationId, Status = f.Quote.Status, UpdatedDate = f.Now };
        var stored = message.PayloadJson;
        await f.Service.EnrichConfirmationAsync(dto, f.Quote.CompanyId, default);
        Assert.Equal(allowed, dto.CanConfirmSent);
        Assert.Equal(allowed, dto.PendingSendConfirmation is not null);
        Assert.Equal(stored, message.PayloadJson);
        if (!allowed) Assert.Equal(QuotationConfirmationErrors.Forbidden, dto.ConfirmSentBlockedReasonCode);
    }
}
