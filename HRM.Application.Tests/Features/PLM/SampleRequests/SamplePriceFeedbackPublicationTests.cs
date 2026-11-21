using System.Reflection;
using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Commands.DispatchWorkflowNotification;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using MediatR;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class SamplePriceFeedbackPublicationTests
{
    [Theory]
    [InlineData("CustomerPriceAccepted")]
    [InlineData("CustomerPriceRejected")]
    [InlineData("CustomerPriceNoResponse")]
    public async Task OldQueuedOutcomeWithoutPrivateNoteIsConsumedWithoutSending(string type)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var company = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var evt = new SampleRequestWorkflowEvent { Id = Guid.NewGuid(), CompanyId = company,
            SampleRequestId = Guid.NewGuid(), EventType = type, RecordedByEmployeeId = actor,
            Note = "  ", PriceReference = "BBG-1" };
        store.Seed(nameof(db.SampleRequestWorkflowEvents), evt);
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = actor, CompanyId = company, IsActive = true });
        store.Seed(nameof(db.OutboxMessages), new OutboxMessage { Id = 1,
            Type = SampleRequestWorkflowNotificationQueue.OutboxType,
            PayloadJson = JsonSerializer.Serialize(new WorkflowNotificationPayload(evt.Id, company)) });
        var sender = DispatchProxy.Create<ISender, CaptureSender>();
        var result = await new DispatchSampleRequestWorkflowNotificationCommandHandler(db, sender, null!, null!)
            .Handle(new(1), default);
        Assert.True(result.Success);
        Assert.Empty(((CaptureSender)sender).Commands);
        Assert.Null(evt.MessageId);
        Assert.Null(evt.NotificationId);
        Assert.Equal(type, evt.EventType);
    }

    [Theory]
    [InlineData(InternalMailRelatedType.SampleRequest, SampleRequestWorkflowEvents.PriceAccepted)]
    [InlineData(InternalMailRelatedType.ConversationTechnical, SampleRequestWorkflowEvents.PriceAccepted)]
    [InlineData(InternalMailRelatedType.ConversationPricing, SampleRequestWorkflowEvents.PriceAccepted)]
    [InlineData(InternalMailRelatedType.SampleRequest, SampleFlowRules.StandardPriceApproved)]
    [InlineData(InternalMailRelatedType.ConversationTechnical, SampleFlowRules.StandardPriceApproved)]
    [InlineData(InternalMailRelatedType.ConversationPricing, SampleFlowRules.StandardPriceApproved)]
    public async Task PendingHistoricalPriceMessageOutsidePricingIsNotRepointedOrRepublished(InternalMailRelatedType area, string eventType)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var company = Guid.NewGuid(); var actor = Guid.NewGuid(); var request = Guid.NewGuid();
        var messageId = Guid.NewGuid(); var conversationId = Guid.NewGuid();
        var evt = new SampleRequestWorkflowEvent { Id = Guid.NewGuid(), CompanyId = company, SampleRequestId = request,
            EventType = eventType, RecordedByEmployeeId = actor,
            MessageId = messageId, ConversationId = conversationId, Note = "PRIVATE_PRICE" };
        store.Seed(nameof(db.SampleRequestWorkflowEvents), evt);
        store.Seed(nameof(db.OutboxMessages), new OutboxMessage { Id = 1, Type = SampleRequestWorkflowNotificationQueue.OutboxType,
            PayloadJson = JsonSerializer.Serialize(new WorkflowNotificationPayload(evt.Id, company)) });
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = actor, CompanyId = company, IsActive = true });
        store.Seed(nameof(db.SampleRequests), new SampleRequest { SampleRequestId = request, CompanyId = company, CreatedDate = new(2026, 11, 4) });
        var original = new InternalMessage { InternalMessageId = messageId, InternalConversationId = conversationId, Body = "PRIVATE_PRICE",
            Conversation = new InternalConversation { InternalConversationId = conversationId, CompanyId = company, IsActive = true,
                RelatedType = area, RelatedId = Guid.NewGuid() } }; // No matching request root: corrupt Pricing links fail closed too.
        store.Seed(nameof(db.InternalMessages), original);
        var sender = DispatchProxy.Create<ISender, CaptureSender>();
        var handler = new DispatchSampleRequestWorkflowNotificationCommandHandler(db, sender, null!, null!);
        var result = await handler.Handle(new DispatchSampleRequestWorkflowNotificationCommand(1), default);
        Assert.False(result.Success);
        Assert.Equal(nameof(WorkflowNotificationFailureCode.HistoricalPricePublicationRequiresReview), result.Message);
        Assert.Empty(((CaptureSender)sender).Commands);
        Assert.Equal(messageId, evt.MessageId);
        Assert.Equal(conversationId, evt.ConversationId);
        Assert.Null(evt.NotificationId);
        Assert.Equal("PRIVATE_PRICE", original.Body);
        Assert.Empty(store.Added);
        Assert.Equal(0, store.Saves);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DispatcherSeparatesPrivatePriceAndExplicitPublicFeedbackAndDoesNotRepublishCompletedEvents(bool maxLength)
    {
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var company = Guid.NewGuid(); var actor = Guid.NewGuid(); var request = Guid.NewGuid();
        var privateNote = "SECRET_PRICE_35000" + (maxLength ? new string('n', 2000 - "SECRET_PRICE_35000".Length) : "");
        var reference = "SECRET_PRICE_REFERENCE" + (maxLength ? new string('r', 500 - "SECRET_PRICE_REFERENCE".Length) : "");
        var sharedNote = "PUBLIC_CUSTOMER_FEEDBACK" + (maxLength ? new string('s', 2000 - "PUBLIC_CUSTOMER_FEEDBACK".Length) : "");
        var primary = new SampleRequestWorkflowEvent { Id = Guid.NewGuid(), CompanyId = company,
            SampleRequestId = request, EventType = SampleRequestWorkflowEvents.PriceAccepted,
            RecordedByEmployeeId = actor, Note = privateNote, PriceReference = reference };
        var shared = new SampleRequestWorkflowEvent { Id = Guid.NewGuid(), CompanyId = company,
            SampleRequestId = request, EventType = SampleFlowRules.PriceFeedbackShared, RelatedEventId = primary.Id,
            RecordedByEmployeeId = actor, Note = sharedNote };
        var jobs = new[] { primary, shared }.Select((evt, index) => new OutboxMessage
        {
            Id = index + 1, Type = SampleRequestWorkflowNotificationQueue.OutboxType,
            PayloadJson = JsonSerializer.Serialize(new WorkflowNotificationPayload(evt.Id, company,
                Message: "UNTRUSTED_OVERRIDE_SECRET", Title: "UNTRUSTED_TITLE_SECRET"))
        }).ToArray();
        store.Seed(nameof(db.OutboxMessages), jobs);
        store.Seed(nameof(db.SampleRequestWorkflowEvents), primary, shared);
        store.Seed(nameof(db.SampleRequests), new SampleRequest { SampleRequestId = request, CompanyId = company,
            CreatedDate = new(2026, 11, 4) });
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = actor, CompanyId = company, IsActive = true });
        var sender = DispatchProxy.Create<ISender, CaptureSender>();
        var capture = (CaptureSender)sender;
        var handler = new DispatchSampleRequestWorkflowNotificationCommandHandler(db, sender, null!, null!);
        foreach (var id in new long[] { 1, 2, 1, 2 })
            Assert.True((await handler.Handle(new DispatchSampleRequestWorkflowNotificationCommand(id), default)).Success);
        Assert.Equal(2, capture.Commands.Count);
        var privateCommand = capture.Commands[0]; var publicCommand = capture.Commands[1];
        Assert.Contains("SECRET_PRICE_35000", privateCommand.Message);
        if (!maxLength) Assert.Contains("SECRET_PRICE_REFERENCE", privateCommand.Message);
        Assert.InRange(privateCommand.Message.Length, 1, 2000);
        Assert.InRange(publicCommand.Message.Length, 1, 2000);
        Assert.Equal(privateNote, primary.Note);
        Assert.Equal(reference, primary.PriceReference);
        Assert.Equal(sharedNote, shared.Note);
        Assert.DoesNotContain("UNTRUSTED", privateCommand.Message + privateCommand.TitleOverride);
        Assert.Contains("PUBLIC_CUSTOMER_FEEDBACK", publicCommand.Message);
        Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(publicCommand));
        Assert.Equal(TopicNotifications.SampleRequestCustomerFeedbackRecorded, publicCommand.TopicOverride);
        Assert.Equal(primary.Id, privateCommand.WorkflowEventId);
        Assert.Equal(shared.Id, publicCommand.WorkflowEventId);
    }

    public class CaptureSender : DispatchProxy
    {
        public List<SendSampleRequestMessageCommand> Commands { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (args?[0] is not SendSampleRequestMessageCommand command) throw new NotSupportedException();
            Commands.Add(command);
            return Task.FromResult(OperationResult<SendInternalMessageResultDto>.Ok(new SendInternalMessageResultDto()
            { ConversationId = Guid.NewGuid(), MessageId = Guid.NewGuid(), NotificationId = Guid.NewGuid() }));
        }
    }
}
