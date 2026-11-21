using System.Text.Json;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.Commands.DispatchWorkflowNotification;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SampleRequestWorkflowNotificationTests
{
    [Theory]
    [InlineData("CustomerPriceAccepted")]
    [InlineData("CustomerPriceRejected")]
    [InlineData("CustomerPriceNoResponse")]
    [InlineData("CustomerPriceAcceptanceRevoked")]
    [InlineData("CustomerPriceConfirmed")]
    [InlineData("CustomerPriceConfirmationRevoked")]
    public void PricingOutcomeOnlyDoesNotQueueChatButExplicitNotesDo(string eventType)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var evt = new SampleRequestWorkflowEvent { Id = Guid.NewGuid(), EventType = eventType,
            PriceReference = "BBG-1", FormulaId = Guid.NewGuid(), Note = "  " };
        SampleRequestWorkflowNotificationQueue.Queue(db, evt);
        Assert.Empty(db.OutboxMessages.Local);
        evt.Note = "Private pricing context";
        SampleRequestWorkflowNotificationQueue.Queue(db, evt);
        Assert.Single(db.OutboxMessages.Local);
    }

    [Fact]
    public void SharedLabFeedbackAndExistingMessageRetriesRemainPublishable()
    {
        Assert.True(HRM.Application.Features.PLM.SampleRequests.Rules.SampleRequestWorkflowPublicationRules.ShouldPublishMessage(
            new() { EventType = SampleFlowRules.PriceFeedbackShared, Note = "Customer response" }));
        Assert.True(HRM.Application.Features.PLM.SampleRequests.Rules.SampleRequestWorkflowPublicationRules.ShouldPublishMessage(
            new() { EventType = "CustomerPriceAccepted", MessageId = Guid.NewGuid() }));
    }

    [Fact]
    public void PublicJsonCannotDeferNotificationOrImpersonateInitialSummary()
    {
        var command = JsonSerializer.Deserialize<SendSampleRequestMessageCommand>(
            "{\"RestoreInitialSummaryHistory\":true,\"DeferNotification\":true,\"IsInitialMessage\":true,\"WorkflowEventId\":\"11111111-1111-1111-1111-111111111111\"}")!;
        Assert.False(command.DeferNotification);
        Assert.False(command.RestoreInitialSummaryHistory);
        Assert.False(command.IsInitialMessage);
        Assert.Null(command.WorkflowEventId);
    }

    [Fact]
    public void StoredSummaryQueueDoesNotDeferTheWorkersNotification()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var evt = new SampleRequestWorkflowEvent
        {
            CompanyId = Guid.NewGuid(), SampleRequestId = Guid.NewGuid(),
            MessageId = Guid.NewGuid(), ConversationId = Guid.NewGuid()
        };
        SampleRequestWorkflowNotificationQueue.Queue(db, evt, new SendSampleRequestMessageCommand
        {
            WorkflowEventId = evt.Id, IsInitialMessage = true, DeferNotification = true,
            Message = "Original summary from FE"
        });
        var payload = JsonSerializer.Deserialize<WorkflowNotificationPayload>(Assert.Single(db.OutboxMessages.Local).PayloadJson)!;
        Assert.Equal(evt.Id, payload.EventId);
        Assert.Equal("Original summary from FE", payload.Message);
        Assert.True(payload.IsInitialMessage);
        Assert.DoesNotContain("DeferNotification", Assert.Single(db.OutboxMessages.Local).PayloadJson);
        Assert.Null(evt.NotificationId);
    }

    [Theory]
    [InlineData("No active President employee was found in the current company.", WorkflowNotificationFailureCode.MissingPresident)]
    [InlineData("This product is outside your communication scope.", WorkflowNotificationFailureCode.CommunicationScopeDenied)]
    [InlineData("Some recipients do not exist or are inactive.", WorkflowNotificationFailureCode.InvalidRecipients)]
    [InlineData("Customer-private-content-or-an-unexpected-error", WorkflowNotificationFailureCode.MessageCreationFailed)]
    public void DispatchFailureOnlyPersistsKnownOperationalCode(string message, WorkflowNotificationFailureCode expected)
        => Assert.Equal(expected, WorkflowNotificationFailureRules.FromMessage(message));

    [Fact]
    public void MaterialReapprovalIsQueuedOnceWithItsEventAndRetainsReasonForWorker()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var evt = new SampleRequestWorkflowEvent
        {
            CompanyId = Guid.NewGuid(), SampleRequestId = Guid.NewGuid(),
            EventType = "LabFormulaConfirmed", FormulaId = Guid.NewGuid(),
            RecordedAt = new DateTime(2026, 11, 1, 10, 0, 0)
        };
        db.SampleRequestWorkflowEvents.Add(evt);

        SampleRequestWorkflowNotificationQueue.Queue(db, evt, materialsChanged: true);

        var payload = JsonSerializer.Deserialize<WorkflowNotificationPayload>(Assert.Single(db.OutboxMessages.Local).PayloadJson)!;
        Assert.True(payload.MaterialsChanged);
        Assert.Equal(evt.Id, payload.EventId);
        Assert.Equal(EntityState.Added, db.Entry(evt).State);
        Assert.Null(evt.MessageId);
        Assert.Null(evt.NotificationId);
    }

    [Fact]
    public void ExistingConfirmationJobsWithoutMaterialFlagRemainOrdinaryConfirmations()
    {
        var payload = JsonSerializer.Deserialize<WorkflowNotificationPayload>(
            $$"""{"EventId":"{{Guid.NewGuid()}}","CompanyId":"{{Guid.NewGuid()}}"}""")!;
        Assert.False(payload.MaterialsChanged);
    }

    [Fact]
    public void QueueTracksOutboxAlongsideEventWithoutSavingOrDispatching()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var evt = new SampleRequestWorkflowEvent
        {
            CompanyId = Guid.NewGuid(), SampleRequestId = Guid.NewGuid(), EventType = "CustomerFeedbackRecorded",
            RecordedAt = new DateTime(2026, 11, 1, 10, 0, 0), RecordedByEmployeeId = Guid.NewGuid()
        };
        db.SampleRequestWorkflowEvents.Add(evt);
        SampleRequestWorkflowNotificationQueue.Queue(db, evt);
        var outbox = Assert.Single(db.OutboxMessages.Local);
        var payload = JsonSerializer.Deserialize<WorkflowNotificationPayload>(outbox.PayloadJson)!;
        Assert.Equal(evt.Id, payload.EventId);
        Assert.Equal(evt.CompanyId, payload.CompanyId);
        Assert.Equal("SampleRequestWorkflowNotification", outbox.Type);
        Assert.Null(outbox.ProcessedAt);
        Assert.Equal(EntityState.Added, db.Entry(evt).State);
        Assert.Equal(EntityState.Added, db.Entry(outbox).State);
    }

    [Fact]
    public void PublicMessageJsonCannotSetTrustedWorkflowActorLink()
    {
        var id = Guid.NewGuid();
        var command = JsonSerializer.Deserialize<SendSampleRequestMessageCommand>(
            $$"""{"WorkflowEventId":"{{id}}","Message":"hello"}""")!;
        Assert.Null(command.WorkflowEventId);
    }

    [Fact]
    public void QueuePreservesExplicitInitialRecipientsAndSilentWatchers()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var recipient = Guid.NewGuid();
        var watcher = Guid.NewGuid();
        SampleRequestWorkflowNotificationQueue.Queue(db, new SampleRequestWorkflowEvent(), new SendSampleRequestMessageCommand
        {
            IsInitialMessage = true, Message = "Initial request", ExtraRecipientEmployeeIds = [recipient], SilentWatcherEmployeeIds = [watcher]
        });
        var payload = JsonSerializer.Deserialize<WorkflowNotificationPayload>(Assert.Single(db.OutboxMessages.Local).PayloadJson)!;
        Assert.True(payload.IsInitialMessage);
        Assert.Equal(new[] { recipient }, payload.ExtraRecipients);
        Assert.Equal(new[] { watcher }, payload.SilentWatchers);
    }

    [Fact]
    public void ScopedOutboxLookupTranslatesJsonAndArrayParameters()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var ids = new[] { Guid.NewGuid().ToString() };
        var company = Guid.NewGuid().ToString();
        var query = db.OutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM notification.outbox_messages WHERE type={SampleRequestWorkflowNotificationQueue.OutboxType}
            AND payload_json->>'CompanyId'={company} AND payload_json->>'EventId'=ANY({ids})
            """).AsNoTracking();
        Assert.Contains("ANY(@", query.ToQueryString());
    }
}
