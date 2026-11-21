using System.Text.Json.Nodes;
using HRM.Application.Abstractions.Documents;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Queries.ExportQuotationPdf;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed partial class QuotationSendWorkflowTests
{
    private static (Fixture F, InternalMessage Message) RejectionFixture(string role = "President")
    {
        var f = new Fixture(role);
        var message = f.SeedPending();
        message.Body = "Original Sale request\nkeep this reason";
        f.MailStore.Seed("Employees", new Employee { EmployeeId = f.Quote.SaleEmployeeId, CompanyId = f.Quote.CompanyId, FullName = "Director", IsActive = true });
        return (f, message);
    }

    private static RejectQuotationSentRequest RejectRequest(Fixture f, InternalMessage message, string reason = "Raise customer pricing\nCheck commission")
        => new() { ExpectedUpdatedDate = f.Quote.UpdatedDate, RequestMessageId = message.InternalMessageId, Reason = reason };

    [Theory]
    [InlineData("President")]
    [InlineData("Developer")]
    public async Task BoardRejectionPreservesCommercialDataAndPublishesToSale(string role)
    {
        var (f, message) = RejectionFixture(role);
        var original = JsonNode.Parse(message.PayloadJson!)!;
        original["historicalExtension"] = "keep me";
        message.PayloadJson = original.ToJsonString();
        var oldNotice = new HRM.Domain.Entities.Notifications.Notification
        {
            Id = Guid.NewGuid(), CompanyId = f.Quote.CompanyId, Topic = TopicNotifications.QuotationSendConfirmationRequested,
            Link = $"/crm/quotations/{f.Quote.QuotationId}", PayloadJson = message.PayloadJson
        };
        f.MailStore.Seed("NotificationUserStates", new HRM.Domain.Entities.Notifications.NotificationUserState { Notification = oldNotice });
        var snapshot = original["comparison"]!.ToJsonString();
        var commercial = QuotationConfirmationCommercialRules.Fingerprint(f.Quote);
        var request = RejectRequest(f, message);
        var result = await f.Service.RejectConfirmationAsync(f.Quote.QuotationId, request, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(QuotationStatus.SendRejected, f.Quote.Status);
        Assert.Null(f.Quote.SentDate);
        Assert.Equal(commercial, QuotationConfirmationCommercialRules.Fingerprint(f.Quote));
        Assert.Equal("Original Sale request\nkeep this reason", message.Body);
        var payload = QuotationSendService.ReadPayload(message.PayloadJson)!;
        Assert.Equal("rejected", payload.Status);
        Assert.Equal(request.Reason, payload.RejectionReason);
        Assert.False(message.IsUrgent);
        Assert.Equal(snapshot, JsonNode.Parse(message.PayloadJson!)!["comparison"]!.ToJsonString());
        var oldNoticePayload = JsonNode.Parse(oldNotice.PayloadJson!)!;
        Assert.Equal(snapshot, oldNoticePayload["comparison"]!.ToJsonString());
        Assert.Equal("rejected", oldNoticePayload["status"]!.GetValue<string>());
        Assert.Equal("keep me", oldNoticePayload["historicalExtension"]!.GetValue<string>());
        Assert.Equal("keep me", JsonNode.Parse(message.PayloadJson!)!["historicalExtension"]!.GetValue<string>());
        var history = Assert.Single(f.WriteStore.Added.OfType<QuotationStatusHistory>());
        Assert.Equal(QuotationStatus.PendingSendConfirmation, history.FromStatus);
        Assert.Equal(QuotationStatus.SendRejected, history.ToStatus);
        Assert.Equal(request.Reason, history.Note);
        var notice = Assert.Single(f.Published);
        Assert.Equal(TopicNotifications.QuotationSendConfirmationRejected, notice.Topic);
        Assert.Contains(f.Quote.SaleEmployeeId, notice.TargetUserIds!);
        Assert.True(f.WriteStore.Transaction.Committed);
        var oldConfirm = await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new() { ExpectedUpdatedDate = f.Quote.UpdatedDate }, message.InternalMessageId, default, confirmationOnly: true);
        Assert.False(oldConfirm.Success);
    }

    [Theory]
    [InlineData("SaleUser")]
    [InlineData("Leader")]
    [InlineData("User")]
    public async Task SaleCannotReject(string role)
    {
        var (f, message) = RejectionFixture(role);
        var result = await f.Service.RejectConfirmationAsync(f.Quote.QuotationId, RejectRequest(f, message), default);
        Assert.Equal(QuotationConfirmationErrors.Forbidden, Assert.IsType<QuotationConfirmationFailure>(result).Code);
        Assert.Equal(QuotationStatus.PendingSendConfirmation, f.Quote.Status);
        Assert.Empty(f.Published);
    }

    [Theory]
    [InlineData("blankReason")]
    [InlineData("longReason")]
    [InlineData("missingToken")]
    [InlineData("staleToken")]
    [InlineData("foreignRequest")]
    [InlineData("foreignQuotation")]
    [InlineData("notPending")]
    [InlineData("commercialChanged")]
    [InlineData("foreignCompany")]
    [InlineData("deletedRequest")]
    [InlineData("ambiguous")]
    public async Task InvalidRejectionCannotMutateData(string scenario)
    {
        var (f, message) = RejectionFixture();
        var request = RejectRequest(f, message);
        var id = f.Quote.QuotationId;
        request = scenario switch
        {
            "blankReason" => new() { ExpectedUpdatedDate = f.Quote.UpdatedDate, Reason = "  " },
            "longReason" => new() { ExpectedUpdatedDate = f.Quote.UpdatedDate, Reason = new string('a', 4001) },
            "missingToken" => new() { Reason = "not accepted" },
            "staleToken" => new() { ExpectedUpdatedDate = f.Now.AddDays(-1), Reason = "not accepted" },
            "foreignRequest" => new() { ExpectedUpdatedDate = f.Quote.UpdatedDate, Reason = "not accepted", RequestMessageId = Guid.NewGuid() },
            _ => request
        };
        if (scenario == "foreignQuotation") id = Guid.NewGuid();
        if (scenario == "notPending") f.Quote.Status = QuotationStatus.Sent;
        if (scenario == "commercialChanged") f.Quote.Lines.Single().PriceTiers.Single().SetPrices(20, 0);
        if (scenario == "foreignCompany") f.Quote.CompanyId = Guid.NewGuid();
        if (scenario == "deletedRequest") message.IsDeleted = true;
        if (scenario == "ambiguous") f.MailStore.Seed("InternalMessages", message, new InternalMessage
        { InternalMessageId = Guid.NewGuid(), InternalConversationId = f.Conversation.InternalConversationId,
            Conversation = f.Conversation, MessageType = message.MessageType, PayloadJson = message.PayloadJson });
        var status = f.Quote.Status;
        var result = await f.Service.RejectConfirmationAsync(id, request, default);
        Assert.False(result.Success);
        Assert.Equal(status, f.Quote.Status);
        Assert.Empty(f.Published);
        Assert.Empty(f.WriteStore.Added.OfType<QuotationStatusHistory>());
    }

    [Fact]
    public async Task MustChangeActualPricingBeforeResubmittingAndBelowStandardStartsNewRound()
    {
        var (f, old) = RejectionFixture();
        Assert.True((await f.Service.RejectConfirmationAsync(f.Quote.QuotationId, RejectRequest(f, old), default)).Success);
        f.Quote.Note = "Only changed a note";
        var unchanged = await f.Service.ExecuteAsync(f.Quote.QuotationId, new() { ExpectedUpdatedDate = f.Quote.UpdatedDate, Note = "Ask again" }, null, default);
        Assert.Equal(QuotationSendRejectionRules.PricesUnchangedCode, Assert.IsType<QuotationConfirmationFailure>(unchanged).Code);
        Assert.Equal(QuotationStatus.SendRejected, f.Quote.Status);
        Assert.False((await f.Service.WithdrawConfirmationAsync(f.Quote.QuotationId,
            new() { ExpectedUpdatedDate = f.Quote.UpdatedDate, Reason = "go back" }, default)).Success);
        var save = await f.SaveHandler().Handle(new(f.Quote.QuotationId, new()
        { ExpectedUpdatedDate = f.Quote.UpdatedDate, CustomerPriceLines = [ChangedPrice(f, 85, 10)] }), default);
        Assert.True(save.Success, save.Message);
        Assert.Equal(QuotationStatus.SendRejected, f.Quote.Status);
        var resend = await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new() { ExpectedUpdatedDate = f.Quote.UpdatedDate, Note = "Revised pricing" }, null, default);
        Assert.True(resend.Success, resend.Message);
        Assert.Equal(QuotationStatus.PendingSendConfirmation, f.Quote.Status);
        Assert.Equal("rejected", QuotationSendService.ReadPayload(old.PayloadJson)!.Status);
        var next = Assert.Single(f.MailStore.Added.OfType<InternalMessage>(), x =>
            QuotationSendService.ReadPayload(x.PayloadJson)?.Status == "pending");
        Assert.NotEqual(old.InternalMessageId, next.InternalMessageId);
        Assert.False(QuotationSendRejectionRules.CanExportPdf(f.Quote.Status, f.Quote.SentDate, f.Now));
        next.Conversation = f.Conversation; // The in-memory store does not perform EF relationship fix-up.
        f.MailStore.Seed("InternalMessages", old, next);
        Assert.True((await f.Service.RejectConfirmationAsync(f.Quote.QuotationId, RejectRequest(f, next, "Still too low"), default)).Success);
        Assert.Equal(QuotationStatus.SendRejected, f.Quote.Status);
    }

    [Fact]
    public async Task EqualOrHigherCustomerPricingSendsAfterExplicitResubmission()
    {
        var (f, message) = RejectionFixture();
        await f.Service.RejectConfirmationAsync(f.Quote.QuotationId, RejectRequest(f, message), default);
        f.Quote.Lines.Single().PriceTiers.Single().SetPrices(100, 0);
        var result = await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new() { ExpectedUpdatedDate = f.Quote.UpdatedDate }, null, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(QuotationStatus.Sent, f.Quote.Status);
        Assert.True(new QuotationDetailDto { Status = f.Quote.Status, SentDate = f.Quote.SentDate, LastSendRejectionAt = f.Now }.CanExportPdf);
    }

    [Fact]
    public async Task NewStandardDoesNotAutomaticallySendRejectedQuotation()
    {
        var (f, message) = RejectionFixture();
        await f.Service.RejectConfirmationAsync(f.Quote.QuotationId, RejectRequest(f, message), default);
        f.Standard.PriceTiers.Single().UnitPrice = 1;
        await f.Service.AutoSendAfterPricingApprovalAsync(f.Quote.QuotationId,
            new(f.Quote.CompanyId, f.Standard.ProductId, f.Standard.ProductPricingVersionId, f.Quote.SaleEmployeeId, f.Now), default);
        Assert.Equal(QuotationStatus.SendRejected, f.Quote.Status);
        Assert.Single(f.Published);
    }

    [Fact]
    public async Task ConcurrentConfirmAndRejectOnlyOneDecisionCommits()
    {
        var (f, message) = RejectionFixture();
        var request = RejectRequest(f, message);
        var results = await Task.WhenAll(f.Service.RejectConfirmationAsync(f.Quote.QuotationId, request, default),
            f.Service.ExecuteAsync(f.Quote.QuotationId, new() { ExpectedUpdatedDate = request.ExpectedUpdatedDate }, message.InternalMessageId, default, confirmationOnly: true));
        Assert.Single(results, r => r.Success);
        Assert.Single(f.WriteStore.Added.OfType<QuotationStatusHistory>());
        Assert.Single(f.Published);
    }

    [Theory]
    [InlineData(QuotationStatus.SendRejected)]
    [InlineData(QuotationStatus.Draft)]
    [InlineData(QuotationStatus.PendingSendConfirmation)]
    public async Task PdfEndpointBlocksBeforeRenderingUntilSent(QuotationStatus status)
    {
        var (f, _) = RejectionFixture();
        f.Quote.Status = status;
        f.Quote.Company = new Company { Name = "Company" };
        f.Quote.SaleEmployee = new Employee { FullName = "Sale" };
        f.Quote.StatusHistories.Add(new() { ToStatus = QuotationStatus.SendRejected, ChangedDate = f.Now });
        f.ReadStore.Seed("Quotations", f.Quote);
        var visibility = Stub<ICustomerVisibilityService>((method, args) => method == "BuildReadScopeAsync"
            ? f.Visibility.BuildScopeAsync(default) : method == "ApplyQuotationVisibility" ? args![0] : throw new NotSupportedException(method));
        var renderer = Stub<IQuotationPdfRenderer>((_, _) => throw new InvalidOperationException("Must not render blocked PDF"));
        var result = await new ExportQuotationPdfQueryHandler(f.ReadDb, visibility, renderer).Handle(new(f.Quote.QuotationId), default);
        Assert.Equal(QuotationSendRejectionRules.PdfBlockedCode, Assert.IsType<QuotationPdfBlockedFailure>(result).Code);
    }

    [Fact]
    public void InternalTierIdsLabelsAndDecimalScaleDoNotCountAsPriceRevision()
    {
        var (f, _) = RejectionFixture();
        var before = QuotationSendRejectionRules.PricingFingerprint(f.Quote);
        var tier = f.Quote.Lines.Single().PriceTiers.Single();
        tier.QuotationLinePriceTierId = Guid.NewGuid();
        tier.QuantityRangeLabel = "Renamed label";
        tier.SetPrices(80.000000m, 10.0000m);
        Assert.Equal(before, QuotationSendRejectionRules.PricingFingerprint(f.Quote));
    }

    [Fact]
    public async Task RejectionTechnicalFailureRollsBackWithoutNotification()
    {
        var (f, message) = RejectionFixture();
        f.WriteStore.ThrowOnSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.RejectConfirmationAsync(f.Quote.QuotationId, RejectRequest(f, message), default));
        Assert.False(f.WriteStore.Transaction.Committed);
        Assert.Empty(f.Published);
    }

    [Fact]
    public void RejectionTopicHasDedicatedStableContract()
    {
        Assert.Equal(63, (int)TopicNotifications.QuotationSendConfirmationRejected);
        Assert.Equal("crm.quotation.send_confirmation.rejected", NotificationTopicCatalog.GetDefinition(TopicNotifications.QuotationSendConfirmationRejected).Code);
    }

    [Fact]
    public void CosmeticRangeFlagsAndLineQuantityDoNotReplaceARejectedPriceProposal()
    {
        var (f, _) = RejectionFixture();
        var line = f.Quote.Lines.Single();
        var tier = line.PriceTiers.Single();
        tier.MinQuantity = null;
        tier.MaxQuantity = null;
        var before = QuotationSendRejectionRules.PricingFingerprint(f.Quote);
        tier.MinInclusive = !tier.MinInclusive;
        tier.MaxInclusive = !tier.MaxInclusive;
        line.Quantity++;
        Assert.Equal(before, QuotationSendRejectionRules.PricingFingerprint(f.Quote));
    }
}
