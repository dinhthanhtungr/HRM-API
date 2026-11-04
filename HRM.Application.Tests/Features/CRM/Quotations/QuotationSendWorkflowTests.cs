using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.CRM.Quotations.Commands.SaveQuotation;
using HRM.Application.Features.CRM.Quotations.Commands.UpdateQuotationCustomerPriceTiers;
using System.Reflection;
using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Tests.Features.DevAndQA.IQC;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.CRM.Quotations;

// Orchestration tests using the repository's async store; PostgreSQL translation is verified separately at deployment.
public sealed partial class QuotationSendWorkflowTests
{
    [Fact]
    public async Task BelowStandardStoresReasonOnlyInThreadAndWaitsForBoard()
    {
        var f = new Fixture();
        var result = await f.Service.ExecuteAsync(f.Quote.QuotationId, new() { Note = "Special customer" }, null, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(QuotationStatus.PendingSendConfirmation, f.Quote.Status);
        Assert.Null(f.Quote.SentDate);
        Assert.Equal("Existing quotation note", f.Quote.Note);
        Assert.All(f.WriteStore.Added.OfType<QuotationStatusHistory>(), x => Assert.Null(x.Note));
        Assert.Empty(f.WriteStore.Added.OfType<CustomerInteraction>());
        var message = Assert.Single(f.MailStore.Added.OfType<InternalMessage>());
        Assert.Equal(f.Conversation.InternalConversationId, message.InternalConversationId);
        Assert.Contains("Special customer", message.Body);
        Assert.True(message.IsUrgent);
        Assert.DoesNotContain("Special customer", message.PayloadJson!);
        var notice = Assert.Single(f.Published);
        Assert.DoesNotContain("Special customer", notice.Message);
        Assert.Equal(message.InternalMessageId, notice.MessageId);
        Assert.True(f.WriteStore.Transaction.Committed);
    }

    [Fact]
    public async Task MissingReasonOrExpiredStandardDoesNotPersistRequest()
    {
        var f = new Fixture();
        Assert.False((await f.Service.ExecuteAsync(f.Quote.QuotationId, new(), null, default)).Success);
        Assert.Empty(f.Published);
        Assert.Empty(f.MailStore.Added.OfType<InternalMessage>());
        f = new Fixture();
        f.Standard.ApprovedAt = f.Now.AddDays(-30);
        Assert.False((await f.Service.ExecuteAsync(f.Quote.QuotationId, new() { Note = "Reason" }, null, default)).Success);
        Assert.Empty(f.Published);
    }

    [Fact]
    public async Task AtStandardCompletesExistingSendFlowWithoutReason()
    {
        var f = new Fixture();
        f.Quote.Lines.Single().PriceTiers.Single().SetPrices(90, 10);
        Assert.True((await f.Service.ExecuteAsync(f.Quote.QuotationId, new(), null, default)).Success);
        Assert.Equal(QuotationStatus.Sent, f.Quote.Status);
        Assert.NotNull(f.Quote.SentDate);
        Assert.Single(f.WriteStore.Added.OfType<CustomerInteraction>());
    }

    [Fact]
    public async Task ScreenshotHighPricesAcrossGapsAndOutsideTableSendWithoutBoardRequest()
    {
        var f = new Fixture();
        SetScreenshotStandard(f.Standard);
        var line = f.Quote.Lines.Single();
        line.PriceTiers = new (decimal Min, decimal? Max)[] { (3000, 4000), (5000, 10000), (10025, 20000), (20000, null) }
            .Select(range =>
            {
                var tier = new QuotationLinePriceTier { MinQuantity = range.Min, MaxQuantity = range.Max, MinInclusive = range.Min != 20000 };
                tier.SetPrices(500000, 0);
                return tier;
            }).ToList();
        var result = await f.Service.ExecuteAsync(f.Quote.QuotationId, new(), null, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(QuotationStatus.Sent, f.Quote.Status);
        Assert.DoesNotContain(f.Published, x => x.Topic == TopicNotifications.QuotationSendConfirmationRequested);
        Assert.Single(f.Published, x => x.Topic == TopicNotifications.QuotationSent);
    }

    [Fact]
    public async Task GapPriceBelowHigherNeighbourStillRequiresReason()
    {
        var f = new Fixture();
        SetScreenshotStandard(f.Standard);
        var customer = f.Quote.Lines.Single().PriceTiers.Single();
        customer.MinQuantity = 6005;
        customer.MaxQuantity = 6010;
        customer.SetPrices(29200, 0);
        Assert.False((await f.Service.ExecuteAsync(f.Quote.QuotationId, new(), null, default)).Success);
        Assert.Empty(f.Published);
    }

    [Fact]
    public async Task OutsidePriceAboveLastApprovedPriceSendsWithoutBoardConfirmation()
    {
        var f = new Fixture();
        SetScreenshotStandard(f.Standard);
        var customer = f.Quote.Lines.Single().PriceTiers.Single();
        customer.MinQuantity = 10025;
        customer.MaxQuantity = null;
        customer.SetPrices(29500, 0); // Above the last tier (29000), below the highest tier (29800).
        var result = await f.Service.ExecuteAsync(f.Quote.QuotationId, new() { Note = "Special volume" }, null, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(QuotationStatus.Sent, f.Quote.Status);
        Assert.DoesNotContain(f.MailStore.Added.OfType<InternalMessage>(),
            message => QuotationSendService.ReadPayload(message.PayloadJson) is not null);
    }

    [Fact]
    public async Task OutsidePriceBelowLastApprovedPriceWaitsAndExplainsLastTierFallback()
    {
        var f = new Fixture();
        SetScreenshotStandard(f.Standard);
        var customer = f.Quote.Lines.Single().PriceTiers.Single();
        customer.MinQuantity = 10025;
        customer.MaxQuantity = null;
        customer.SetPrices(28500, 0);
        var result = await f.Service.ExecuteAsync(f.Quote.QuotationId, new() { Note = "Special volume" }, null, default);
        Assert.True(result.Success, result.Message);
        Assert.Equal(QuotationStatus.PendingSendConfirmation, f.Quote.Status);
        var message = Assert.Single(f.MailStore.Added.OfType<InternalMessage>());
        Assert.Contains("giá của mốc chuẩn cuối theo khối lượng", message.Body);
        var comparison = Assert.Single(QuotationSendService.ReadPayload(message.PayloadJson)!.Comparison);
        Assert.Equal(QuotationSendPriceSegment.LastApprovedTierFallback, comparison.ComparisonBasis);
        Assert.Equal(29000, comparison.StandardUnitPrice);
        Assert.Equal(-500, comparison.DifferenceAmount);
    }

    private static void SetScreenshotStandard(ProductPricingVersion standard)
        => standard.PriceTiers = [
            new() { MinQuantity = 1025, MaxQuantity = 3000, UnitPrice = 29800 },
            new() { MinQuantity = 3025, MaxQuantity = 6000, UnitPrice = 29400 },
            new() { MinQuantity = 6025, MaxQuantity = 10000, UnitPrice = 29000 }
        ];

    [Theory]
    [InlineData("SaleUser", false)]
    [InlineData("Developer", true)]
    [InlineData("President", true)]
    public async Task OnlyBoardCanConfirmAndResolveRequest(string role, bool allowed)
    {
        var f = new Fixture(role);
        var message = f.SeedPending();
        var result = await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new() { ExpectedUpdatedDate = f.Quote.UpdatedDate }, message.InternalMessageId, default);
        Assert.Equal(allowed, result.Success);
        Assert.Equal(allowed ? QuotationStatus.Sent : QuotationStatus.PendingSendConfirmation, f.Quote.Status);
        Assert.Equal(!allowed, message.IsUrgent);
        Assert.Equal(allowed ? "confirmed" : "pending", QuotationSendService.ReadPayload(message.PayloadJson)!.Status);
    }

    [Fact]
    public async Task ForeignCompanyOrWrongMessageCannotBeConfirmed()
    {
        var f = new Fixture("President");
        var message = f.SeedPending();
        f.Quote.CompanyId = Guid.NewGuid();
        Assert.False((await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new() { ExpectedUpdatedDate = f.Quote.UpdatedDate }, message.InternalMessageId, default)).Success);
        f = new Fixture("President");
        f.SeedPending();
        Assert.False((await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new() { ExpectedUpdatedDate = f.Quote.UpdatedDate }, Guid.NewGuid(), default)).Success);
    }

    [Fact]
    public async Task StaleTokenAndDuplicateRequestCannotCreateSideEffects()
    {
        var f = new Fixture("President");
        var message = f.SeedPending();
        Assert.False((await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new() { ExpectedUpdatedDate = f.Now.AddMinutes(-1) }, message.InternalMessageId, default)).Success);
        Assert.True((await f.Service.ExecuteAsync(f.Quote.QuotationId, new(), null, default)).Success);
        Assert.Empty(f.Published);
        Assert.Empty(f.MailStore.Added.OfType<InternalMessage>());
    }

    [Fact]
    public async Task ConfirmationResolvesNotificationWithoutDeletingHistory()
    {
        var f = new Fixture("President");
        var message = f.SeedPending();
        var notification = new Notification
        {
            CompanyId = f.Quote.CompanyId, Topic = TopicNotifications.QuotationSendConfirmationRequested,
            Link = $"/crm/quotations/{f.Quote.QuotationId}", PayloadJson = message.PayloadJson,
            Severity = NotificationSeverity.Warning
        };
        f.MailStore.Seed("NotificationUserStates", new NotificationUserState { Notification = notification });
        Assert.True((await f.Service.ExecuteAsync(f.Quote.QuotationId,
            new() { ExpectedUpdatedDate = f.Quote.UpdatedDate }, message.InternalMessageId, default)).Success);
        Assert.Equal(NotificationSeverity.Success, notification.Severity);
        Assert.Equal("confirmed", QuotationSendService.ReadPayload(notification.PayloadJson)!.Status);
        Assert.False(QuotationSendService.ReadPayload(notification.PayloadJson)!.IsUrgent);
        Assert.False(message.IsDeleted);
        Assert.Single(f.WriteStore.Added.OfType<CustomerInteraction>());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AnonymousOrExplicitRevokedPermissionCannotConfirm(bool anonymous, bool explicitSet)
    {
        var user = new IqcWorkflowTests.User { IsAuthenticated = !anonymous, Roles = ["President"], HasExplicitPermissionSet = explicitSet };
        Assert.False(new CurrentUserPermissionService(user).HasPermission(ApplicationPermissions.Quotations.ConfirmSent));
        Assert.False(new CurrentUserPermissionService(user).HasPermission("unknown.permission"));
    }

    [Fact]
    public void PendingStateAllowsPriceSaveButBlocksOtherPricingTransitions()
    {
        var status = QuotationStatus.PendingSendConfirmation;
        Assert.False(QuotationWorkflowRules.CanMarkSent(status));
        Assert.False(QuotationWorkflowRules.CanRequestPricing(status));
        Assert.True(QuotationWorkflowRules.CanEditCustomerPricing(status));
        Assert.False(QuotationWorkflowRules.CanWithdrawPricingRequest(status));
        Assert.Null(QuotationSaveRules.ValidateStatus(new Quotation { Status = status }, new SaveQuotationRequest()));
    }

}
