using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.CRM.Quotations.Commands.SaveQuotation;
using HRM.Application.Features.CRM.Quotations.Commands.UpdateQuotationCustomerPriceTiers;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed partial class QuotationSendWorkflowTests
{
    [Fact]
    public async Task SavePendingPricesIncludesCommissionRangeToggleAndLineNoteAndReleasesApproval()
    {
        var f = new Fixture("President");
        var old = f.SeedPending();
        var result = await f.SaveHandler().Handle(new(f.Quote.QuotationId, new SaveQuotationRequest
        {
            ExpectedUpdatedDate = f.Quote.UpdatedDate, CustomerPriceLines = [ChangedPrice(f, 90, 10)],
            Header = new UpdateQuotationRequest { PaymentTerms = "30 days" }
        }), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(QuotationStatus.Approved, result.Data!.Status);
        Assert.False(result.Data.RequiresSendConfirmation);
        Assert.Null(result.Data.SendConfirmationMessageId);
        Assert.Null(f.Quote.SentDate);
        var line = f.Quote.Lines.Single();
        var tier = Assert.Single(line.PriceTiers, x => x.IsActive);
        Assert.Equal(90, tier.UnitPrice);
        Assert.Equal(10, tier.CommissionAmount);
        Assert.Equal(100, tier.CustomerUnitPrice);
        Assert.Equal(350, tier.MinQuantity);
        Assert.Equal(500, tier.MaxQuantity);
        Assert.Single(line.PriceTiers, x => !x.IsActive);
        Assert.Equal("Revised customer terms", line.Note);
        Assert.Equal("Existing quotation note", f.Quote.Note);
        Assert.Equal("30 days", f.Quote.PaymentTerms);
        Assert.Equal(QuotationSendConfirmationPayload.SupersededStatus, QuotationSendService.ReadPayload(old.PayloadJson)!.Status);
        Assert.False(old.IsUrgent);
        Assert.DoesNotContain(f.Published, x => x.Topic == TopicNotifications.QuotationSendConfirmationRequested);
        Assert.Contains(f.Published, x => x.Topic == TopicNotifications.QuotationMessageCreated);
        var staleApproval = await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new() { ExpectedUpdatedDate = result.Data.UpdatedDate }, old.InternalMessageId, default);
        Assert.False(staleApproval.Success);
    }

    [Fact]
    public async Task SaveBelowStandardReplacesOldSnapshotWithNewReasonAndToken()
    {
        var f = new Fixture("President");
        var old = f.SeedPending();
        var originalToken = f.Quote.UpdatedDate;
        var oldNotice = new HRM.Domain.Entities.Notifications.Notification
        {
            CompanyId = f.Quote.CompanyId, Topic = TopicNotifications.QuotationSendConfirmationRequested,
            Link = $"/crm/quotations/{f.Quote.QuotationId}", PayloadJson = old.PayloadJson,
            Severity = NotificationSeverity.Warning
        };
        f.MailStore.Seed("NotificationUserStates", new HRM.Domain.Entities.Notifications.NotificationUserState { Notification = oldNotice });
        var result = await f.SaveHandler().Handle(new(f.Quote.QuotationId, new SaveQuotationRequest
        {
            ExpectedUpdatedDate = originalToken, CustomerPriceLines = [ChangedPrice(f, 75, 10)],
            SendConfirmationNote = "New volume discount"
        }), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(QuotationStatus.PendingSendConfirmation, result.Data!.Status);
        Assert.True(result.Data.RequiresSendConfirmation);
        Assert.True(result.Data.UpdatedDate > originalToken);
        var message = Assert.Single(f.MailStore.Added.OfType<InternalMessage>());
        Assert.Equal(message.InternalMessageId, result.Data.SendConfirmationMessageId);
        var payload = QuotationSendService.ReadPayload(message.PayloadJson)!;
        Assert.Equal(result.Data.UpdatedDate, payload.ExpectedUpdatedDate);
        Assert.All(payload.Comparison, x => Assert.Equal(85, x.CustomerUnitPrice));
        Assert.Contains("New volume discount", message.Body);
        Assert.DoesNotContain("New volume discount", message.PayloadJson!);
        Assert.Equal(QuotationSendConfirmationPayload.SupersededStatus, QuotationSendService.ReadPayload(old.PayloadJson)!.Status);
        Assert.Equal(NotificationSeverity.Info, oldNotice.Severity);
        Assert.Equal(QuotationSendConfirmationPayload.SupersededStatus, QuotationSendService.ReadPayload(oldNotice.PayloadJson)!.Status);
        Assert.False(QuotationSendService.ReadPayload(oldNotice.PayloadJson)!.IsUrgent);
        Assert.False((await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new() { ExpectedUpdatedDate = result.Data.UpdatedDate }, old.InternalMessageId, default)).Success);
        Assert.Equal(QuotationStatus.PendingSendConfirmation, f.Quote.Status);
        message.Conversation = f.Conversation;
        f.MailStore.Seed("InternalMessages", old, message);
        var approval = await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new() { ExpectedUpdatedDate = result.Data.UpdatedDate }, message.InternalMessageId, default);
        Assert.True(approval.Success, approval.Message);
        Assert.Equal(QuotationStatus.Sent, f.Quote.Status);
        Assert.Equal(QuotationSendConfirmationPayload.ConfirmedStatus, QuotationSendService.ReadPayload(message.PayloadJson)!.Status);
        Assert.Equal(QuotationSendConfirmationPayload.SupersededStatus, QuotationSendService.ReadPayload(old.PayloadJson)!.Status);
    }

    [Fact]
    public async Task MissingNewReasonRollsBackWithoutInvalidatingOldRequest()
    {
        var f = new Fixture();
        var old = f.SeedPending();
        var result = await f.SaveHandler().Handle(new(f.Quote.QuotationId, new SaveQuotationRequest
        {
            ExpectedUpdatedDate = f.Quote.UpdatedDate, CustomerPriceLines = [ChangedPrice(f, 75, 10)]
        }), default);
        Assert.False(result.Success);
        Assert.Contains("sendConfirmationNote", result.Message!);
        Assert.True(f.WriteStore.Transaction.RolledBack);
        Assert.Empty(f.Published);
        Assert.Equal(QuotationSendConfirmationPayload.PendingStatus, QuotationSendService.ReadPayload(old.PayloadJson)!.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SentPricesRemainLockedForSaveAndTierEndpoint(bool throughSave)
    {
        var f = new Fixture();
        f.Quote.Status = QuotationStatus.Sent;
        f.Quote.UpdatedDate = f.Now;
        var result = throughSave
            ? await f.SaveHandler().Handle(new(f.Quote.QuotationId, new SaveQuotationRequest
                { ExpectedUpdatedDate = f.Now, CustomerPriceLines = [ChangedPrice(f, 100, 0)] }), default)
            : await f.PriceHandler().Handle(new(f.Quote.QuotationId, new UpdateQuotationCustomerPriceTiersRequest
                { ExpectedUpdatedDate = f.Now, Lines = [ChangedPrice(f, 100, 0)] }), default);
        Assert.False(result.Success);
        Assert.Equal(90, f.Quote.Lines.Single().PriceTiers.Single().CustomerUnitPrice);
        Assert.Empty(f.Published);
    }

    [Fact]
    public async Task TierEndpointAlsoInvalidatesPendingConfirmation()
    {
        var f = new Fixture();
        var old = f.SeedPending();
        var result = await f.PriceHandler().Handle(new(f.Quote.QuotationId, new UpdateQuotationCustomerPriceTiersRequest
        {
            ExpectedUpdatedDate = f.Now, Lines = [ChangedPrice(f, 110, 0)]
        }), default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(QuotationStatus.Approved, result.Data!.Status);
        Assert.Equal(QuotationSendConfirmationPayload.SupersededStatus, QuotationSendService.ReadPayload(old.PayloadJson)!.Status);
        Assert.True(f.WriteStore.Transaction.Committed);
    }

    [Fact]
    public async Task ConcurrentSavesWithSameTokenAllowExactlyOneReplacement()
    {
        var f = new Fixture();
        f.SeedPending();
        var command = new SaveQuotationCommand(f.Quote.QuotationId, new SaveQuotationRequest
        {
            ExpectedUpdatedDate = f.Now, CustomerPriceLines = [ChangedPrice(f, 75, 10)], SendConfirmationNote = "New reason"
        });
        var results = await Task.WhenAll(f.SaveHandler().Handle(command, default), f.SaveHandler().Handle(command, default));
        Assert.Single(results, x => x.Success);
        Assert.Single(results, x => !x.Success && OptimisticConcurrencyHelper.IsConflictMessage(x.Message));
        Assert.Single(f.MailStore.Added.OfType<InternalMessage>());
    }

    [Fact]
    public async Task DatabaseConflictDuringReplacementRollsBackWithoutPublishing()
    {
        var f = new Fixture();
        f.SeedPending();
        f.WriteStore.FailWithConcurrency = true;
        var result = await f.SaveHandler().Handle(new(f.Quote.QuotationId, new SaveQuotationRequest
        {
            ExpectedUpdatedDate = f.Now, CustomerPriceLines = [ChangedPrice(f, 75, 10)], SendConfirmationNote = "New reason"
        }), default);
        Assert.False(result.Success);
        Assert.True(OptimisticConcurrencyHelper.IsConflictMessage(result.Message));
        Assert.True(f.WriteStore.Transaction.RolledBack);
        Assert.False(f.WriteStore.Transaction.Committed);
        Assert.Empty(f.Published);
    }

    [Fact]
    public async Task ForeignCompanyCannotSavePendingCustomerPrices()
    {
        var f = new Fixture();
        var old = f.SeedPending();
        f.Quote.CompanyId = Guid.NewGuid();
        var result = await f.SaveHandler().Handle(new(f.Quote.QuotationId, new SaveQuotationRequest
        {
            ExpectedUpdatedDate = f.Now, CustomerPriceLines = [ChangedPrice(f, 110, 0)]
        }), default);
        Assert.False(result.Success);
        Assert.Empty(f.Published);
        Assert.Equal(QuotationSendConfirmationPayload.PendingStatus, QuotationSendService.ReadPayload(old.PayloadJson)!.Status);
    }

    private static UpdateQuotationCustomerPriceTierLineRequest ChangedPrice(Fixture f, decimal price, decimal commission)
        => new()
        {
            QuotationLineId = f.Quote.Lines.Single().QuotationLineId, Note = "Revised customer terms",
            PriceTiers = [
                new() { QuantityRangeLabel = "350-500", MinQuantity = 350, MaxQuantity = 500,
                    UnitPrice = price, CommissionAmount = commission, SortOrder = 0, IsActive = true },
                new() { QuantityRangeLabel = "Inactive", MinQuantity = 600, MaxQuantity = 700,
                    UnitPrice = 1, SortOrder = 1, IsActive = false }
            ]
        };
}
