using System.Text.Json;
using System.Text.Json.Nodes;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Notifications;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed partial class QuotationSendWorkflowTests
{
    private static (Fixture Fixture, InternalMessage Message, QuotationPricingApprovalAutoSend Approval) AutoSendFixture()
    {
        var f = new Fixture();
        var message = f.SeedPending();
        message.SentAt = f.Now.AddMinutes(-1);
        message.Body = "Original sale reason\nsecond line";
        f.Standard.ApprovedAt = f.Now;
        var actor = Guid.NewGuid();
        f.Standard.ApprovedBy = actor;
        f.MailStore.Seed("Employees", new Employee { EmployeeId = actor, CompanyId = f.Quote.CompanyId, FullName = "Approver", IsActive = true },
            new Employee { EmployeeId = f.Quote.SaleEmployeeId, CompanyId = f.Quote.CompanyId, FullName = "Sale", IsActive = true });
        f.Standard.PriceTiers.Single().UnitPrice = 90; // customer = 80 + 10 commission
        var approval = new QuotationPricingApprovalAutoSend(f.Quote.CompanyId, f.Standard.ProductId,
            f.Standard.ProductPricingVersionId, actor, f.Now);
        return (f, message, approval);
    }

    [Theory]
    [InlineData(80)]
    [InlineData(90)]
    public async Task NewStandardAtOrBelowCustomerSendsAndPreservesSnapshot(decimal reference)
    {
        var (f, message, approval) = AutoSendFixture();
        f.Standard.PriceTiers.Single().UnitPrice = reference;
        var before = JsonNode.Parse(message.PayloadJson!)!.AsObject();
        before["historicalExtension"] = "retain me";
        message.PayloadJson = before.ToJsonString();
        var snapshot = before["comparison"]!.ToJsonString();
        var notification = new Notification
        {
            Id = Guid.NewGuid(), CompanyId = f.Quote.CompanyId,
            Topic = TopicNotifications.QuotationSendConfirmationRequested,
            Link = $"/crm/quotations/{f.Quote.QuotationId}", PayloadJson = message.PayloadJson
        };
        f.MailStore.Seed("NotificationUserStates", new NotificationUserState { Notification = notification });
        var fingerprint = QuotationConfirmationCommercialRules.Fingerprint(f.Quote);
        await f.Service.AutoSendAfterPricingApprovalAsync(f.Quote.QuotationId, approval, default);
        Assert.Equal(QuotationStatus.Sent, f.Quote.Status);
        Assert.Equal(f.Now, f.Quote.SentDate);
        Assert.Equal(fingerprint, QuotationConfirmationCommercialRules.Fingerprint(f.Quote));
        Assert.Equal("Original sale reason\nsecond line", message.Body);
        var after = JsonNode.Parse(message.PayloadJson!)!;
        Assert.Equal(snapshot, after["comparison"]!.ToJsonString());
        Assert.Equal("retain me", after["historicalExtension"]!.GetValue<string>());
        Assert.Equal("confirmed", after["status"]!.GetValue<string>());
        Assert.Equal("automatic_standard_price_approval", after["confirmationMethod"]!.GetValue<string>());
        var notificationAfter = JsonNode.Parse(notification.PayloadJson!)!;
        Assert.Equal(snapshot, notificationAfter["comparison"]!.ToJsonString());
        Assert.Equal("retain me", notificationAfter["historicalExtension"]!.GetValue<string>());
        Assert.Equal("confirmed", notificationAfter["status"]!.GetValue<string>());
        Assert.Contains("tự động", notification.Title);
        Assert.Contains("Tự động", Assert.Single(f.WriteStore.Added.OfType<QuotationStatusHistory>()).Note!);
        var notice = Assert.Single(f.Published);
        Assert.Equal(TopicNotifications.QuotationSent, notice.Topic);
        Assert.Contains("tự động", notice.Message);
        Assert.Contains(f.Quote.SaleEmployeeId, notice.TargetUserIds!);
        Assert.Contains(approval.ApprovedBy, notice.TargetUserIds!);
        Assert.True(f.WriteStore.Transaction.Committed);
    }

    [Theory]
    [InlineData(QuotationStatus.Draft)]
    [InlineData(QuotationStatus.PendingApproval)]
    [InlineData(QuotationStatus.Approved)]
    [InlineData(QuotationStatus.Sent)]
    public async Task OnlyPendingSendCanAutomaticallySend(QuotationStatus status)
    {
        var (f, _, approval) = AutoSendFixture();
        f.Quote.Status = status;
        await f.Service.AutoSendAfterPricingApprovalAsync(f.Quote.QuotationId, approval, default);
        Assert.Equal(status, f.Quote.Status);
        Assert.Empty(f.Published);
    }

    [Theory]
    [InlineData("below")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("missing")]
    [InlineData("inactiveTier")]
    [InlineData("currency")]
    [InlineData("company")]
    [InlineData("product")]
    [InlineData("commercial")]
    [InlineData("token")]
    [InlineData("legacy")]
    [InlineData("laterRequest")]
    [InlineData("superseded")]
    [InlineData("ambiguous")]
    [InlineData("inactiveActor")]
    [InlineData("deleted")]
    [InlineData("emptyCustomerTiers")]
    public async Task UnsafeOrIneligibleRequestRemainsPending(string scenario)
    {
        var (f, message, approval) = AutoSendFixture();
        var json = JsonNode.Parse(message.PayloadJson!)!;
        switch (scenario)
        {
            case "below": f.Standard.PriceTiers.Single().UnitPrice = 91; break;
            case "expired": f.Standard.ApprovedAt = f.Now.AddDays(-31); break;
            case "future": f.Standard.ApprovedAt = f.Now.AddDays(1); break;
            case "missing": f.Standard.Status = ProductPricingStatus.Superseded; break;
            case "inactiveTier": f.Standard.PriceTiers.Single().IsActive = false; break;
            case "currency": f.Standard.Currency = "USD"; break;
            case "company": approval = approval with { CompanyId = Guid.NewGuid() }; break;
            case "product": approval = approval with { ProductId = Guid.NewGuid() }; break;
            case "commercial": f.Quote.Lines.Single().PriceTiers.Single().SetPrices(79, 10); break;
            case "token": f.Quote.UpdatedDate = f.Now.AddSeconds(1); break;
            case "legacy": json["commercialFingerprint"] = null; json["commercialFingerprintVersion"] = null; break;
            case "laterRequest": message.SentAt = approval.ApprovedAt.AddSeconds(1); break;
            case "superseded": json["status"] = "superseded"; break;
            case "ambiguous":
                f.MailStore.Seed("InternalMessages", message, new InternalMessage
                {
                    InternalMessageId = Guid.NewGuid(), InternalConversationId = f.Conversation.InternalConversationId,
                    Conversation = f.Conversation, MessageType = message.MessageType, PayloadJson = message.PayloadJson
                });
                break;
            case "inactiveActor": f.MailStore.Seed<Employee>("Employees"); break;
            case "deleted": message.IsDeleted = true; break;
            case "emptyCustomerTiers": f.Quote.Lines.Single().PriceTiers.Clear(); break;
        }
        message.PayloadJson = json.ToJsonString();
        await f.Service.AutoSendAfterPricingApprovalAsync(f.Quote.QuotationId, approval, default);
        Assert.Equal(QuotationStatus.PendingSendConfirmation, f.Quote.Status);
        Assert.Empty(f.Published);
        Assert.Empty(f.WriteStore.Added.OfType<CustomerInteraction>());
        Assert.False(f.WriteStore.Transaction.Committed);
    }

    [Fact]
    public async Task AllProductsMustMeetCurrentStandards()
    {
        var (f, message, approval) = AutoSendFixture();
        var tier = new QuotationLinePriceTier { MinQuantity = 350, MaxQuantity = 600 };
        tier.SetPrices(50, 0);
        var second = new QuotationLine { QuotationLineId = Guid.NewGuid(), ProductId = Guid.NewGuid(), Quantity = 400,
            PriceTiers = [tier], ProductExternalIdSnapshot = "P2" };
        f.Quote.Lines.Add(second);
        var json = JsonNode.Parse(message.PayloadJson!)!;
        json["commercialFingerprint"] = QuotationConfirmationCommercialRules.Fingerprint(f.Quote);
        message.PayloadJson = json.ToJsonString();
        var otherStandard = new ProductPricingVersion { CompanyId = f.Quote.CompanyId, ProductId = second.ProductId,
            Status = ProductPricingStatus.Approved, ApprovedAt = f.Now, PriceTiers = [new() { MinQuantity = 300, MaxQuantity = 600, UnitPrice = 51 }] };
        f.ReadStore.Seed("ProductPricingVersions", f.Standard, otherStandard);
        await f.Service.AutoSendAfterPricingApprovalAsync(f.Quote.QuotationId, approval, default);
        Assert.Equal(QuotationStatus.PendingSendConfirmation, f.Quote.Status);
        otherStandard.PriceTiers.Single().UnitPrice = 50;
        await f.Service.AutoSendAfterPricingApprovalAsync(f.Quote.QuotationId, approval, default);
        Assert.Equal(QuotationStatus.Sent, f.Quote.Status);
    }

    [Fact]
    public async Task DuplicateConcurrentJobsOnlyCompleteOnce()
    {
        var (f, _, approval) = AutoSendFixture();
        await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            f.Service.AutoSendAfterPricingApprovalAsync(f.Quote.QuotationId, approval, default)));
        Assert.Single(f.Published);
        Assert.Single(f.WriteStore.Added.OfType<CustomerInteraction>());
    }

    [Theory]
    [InlineData(180, true)]
    [InlineData(181, false)]
    public async Task UsesQuotationCurrencyAndStoredExchangeRate(decimal standardVnd, bool sends)
    {
        var (f, message, approval) = AutoSendFixture();
        f.Quote.Currency = "USD";
        f.Quote.ExchangeRate = 2;
        f.Standard.PriceTiers.Single().UnitPrice = standardVnd;
        var json = JsonNode.Parse(message.PayloadJson!)!;
        json["commercialFingerprint"] = QuotationConfirmationCommercialRules.Fingerprint(f.Quote);
        message.PayloadJson = json.ToJsonString();
        await f.Service.AutoSendAfterPricingApprovalAsync(f.Quote.QuotationId, approval, default);
        Assert.Equal(sends ? QuotationStatus.Sent : QuotationStatus.PendingSendConfirmation, f.Quote.Status);
    }

    [Fact]
    public async Task ProcessorRejectsForeignApprovalAndRetriesTechnicalFailure()
    {
        var (f, _, approval) = AutoSendFixture();
        f.ReadStore.Seed("Quotations", f.Quote);
        var processor = new QuotationPricingApprovalAutoSendProcessor(f.ReadDb, f.Service);
        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(approval with { CompanyId = Guid.NewGuid() }, default));
        f.WriteStore.FailWithConcurrency = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(approval, default));
        Assert.False(f.WriteStore.Transaction.Committed);
        Assert.Empty(f.Published);
    }

    [Fact]
    public async Task ConcurrencyFailureRollsBackWithoutPublishing()
    {
        var (f, _, approval) = AutoSendFixture();
        f.WriteStore.FailWithConcurrency = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.AutoSendAfterPricingApprovalAsync(f.Quote.QuotationId, approval, default));
        Assert.False(f.WriteStore.Transaction.Committed);
        Assert.Empty(f.Published);
    }

    [Fact]
    public async Task ActiveApproverWithoutDisplayNameStillProcessesNormally()
    {
        var (f, _, approval) = AutoSendFixture();
        f.MailStore.Seed("Employees", new Employee { EmployeeId = approval.ApprovedBy, CompanyId = approval.CompanyId, FullName = "", IsActive = true });
        await f.Service.AutoSendAfterPricingApprovalAsync(f.Quote.QuotationId, approval, default);
        Assert.Equal(QuotationStatus.Sent, f.Quote.Status);
    }

    [Fact]
    public async Task UsesThePendingRequestsConversationWhenAnotherConversationExists()
    {
        var (f, message, approval) = AutoSendFixture();
        var duplicate = new InternalConversation { InternalConversationId = Guid.NewGuid(), CompanyId = approval.CompanyId,
            RelatedType = f.Conversation.RelatedType, RelatedId = f.Quote.QuotationId, IsActive = true };
        f.MailStore.Seed("InternalConversations", duplicate, f.Conversation);
        await f.Service.AutoSendAfterPricingApprovalAsync(f.Quote.QuotationId, approval, default);
        Assert.Equal(QuotationStatus.Sent, f.Quote.Status);
        Assert.Equal(message.InternalConversationId, Assert.Single(f.MailStore.Added.OfType<InternalMessage>()).InternalConversationId);
    }

    [Fact]
    public void ApprovalQueueContainsOnlyIdentifiersAndSurvivesSerialization()
    {
        var actor = Guid.NewGuid();
        var job = QuotationPricingApprovalAutoSend.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), actor, DateTime.Now);
        Assert.Equal(QuotationPricingApprovalAutoSend.OutboxType, job.Type);
        Assert.Null(job.ProcessedAt);
        var payload = JsonSerializer.Deserialize<QuotationPricingApprovalAutoSend>(job.PayloadJson)!;
        Assert.Equal(actor, payload.ApprovedBy);
        Assert.Equal(DateTimeKind.Unspecified, payload.ApprovedAt.Kind);
        Assert.Equal(0, payload.ApprovedAt.Ticks % TimeSpan.TicksPerMicrosecond);
        Assert.DoesNotContain("comparison", job.PayloadJson);
        Assert.DoesNotContain("priceTiers", job.PayloadJson);
    }
}
