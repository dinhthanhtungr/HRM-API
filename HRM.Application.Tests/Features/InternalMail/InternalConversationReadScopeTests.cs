using System.Text.Json;
using HRM.Application.Features.InternalMail.Commands.MarkConversationRead;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;
using HRM.Domain.Enums.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail;

public sealed class InternalConversationReadScopeTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly Guid _conversationId = Guid.NewGuid();
    private readonly DateTime _cutoff = new(2026, 9, 30, 12, 0, 0);

    [Fact]
    public void ConversationMessages_SelectsMessageWithoutRecipientReadState()
    {
        var message = Message().Message;
        Assert.Empty(message.ReadStates);
        Assert.True(InternalConversationReadScope.ConversationMessages(
            _companyId, _employeeId, _conversationId, _cutoff).Compile()(message));
    }

    [Theory]
    [InlineData("company")]
    [InlineData("conversation")]
    [InlineData("employee")]
    [InlineData("inactiveParticipant")]
    [InlineData("inactiveConversation")]
    [InlineData("deleted")]
    [InlineData("newMessage")]
    [InlineData("sameTimeNewerId")]
    public void ConversationMessages_MissingReadStateDoesNotBypassScope(string change)
    {
        var message = Message().Message;
        var boundaryId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        message.InternalMessageId = boundaryId;
        var predicate = InternalConversationReadScope.ConversationMessages(
            _companyId, _employeeId, _conversationId, _cutoff, boundaryId).Compile();
        Assert.True(predicate(message));
        switch (change)
        {
            case "company": message.Conversation.CompanyId = Guid.NewGuid(); break;
            case "conversation": message.InternalConversationId = Guid.NewGuid(); break;
            case "employee": message.Conversation.Participants.Single().EmployeeId = Guid.NewGuid(); break;
            case "inactiveParticipant": message.Conversation.Participants.Single().IsActive = false; break;
            case "inactiveConversation": message.Conversation.IsActive = false; break;
            case "deleted": message.IsDeleted = true; break;
            case "newMessage": message.SentAt = _cutoff.AddTicks(1); break;
            case "sameTimeNewerId": message.InternalMessageId = Guid.Parse("00000000-0000-0000-0000-000000000003"); break;
        }
        Assert.False(predicate(message));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PricingApproval_WithoutMessage_IsAcknowledgedWhenThreadIsViewed(bool historicalCasing)
    {
        var row = new NotificationDto
        {
            Topic = TopicNotifications.QuotationPricingApproved,
            CreatedDate = _cutoff,
            PayloadJson = historicalCasing
                ? JsonSerializer.Serialize(new { ConversationId = _conversationId, MessageId = (Guid?)null })
                : JsonSerializer.Serialize(new { conversationId = _conversationId })
        };
        Assert.True(InternalConversationReadScope.MatchesReadReceipt(row, _conversationId, new HashSet<Guid>(), _cutoff));
    }

    [Theory]
    [InlineData("conversation")]
    [InlineData("topic")]
    [InlineData("newNotification")]
    [InlineData("invalidMessageId")]
    [InlineData("unseenMessage")]
    public void ThreadReceipt_DoesNotAcknowledgeUnrelatedOrNewNotification(string change)
    {
        var row = new NotificationDto
        {
            Topic = TopicNotifications.QuotationPricingApproved,
            CreatedDate = _cutoff,
            PayloadJson = JsonSerializer.Serialize(new { conversationId = _conversationId })
        };
        switch (change)
        {
            case "conversation": row.PayloadJson = JsonSerializer.Serialize(new { conversationId = Guid.NewGuid() }); break;
            case "topic": row.Topic = (TopicNotifications)0; break;
            case "newNotification": row.CreatedDate = _cutoff.AddTicks(1); break;
            case "invalidMessageId": row.PayloadJson = JsonSerializer.Serialize(new { conversationId = _conversationId, messageId = "invalid" }); break;
            case "unseenMessage": row.PayloadJson = JsonSerializer.Serialize(new { conversationId = _conversationId, messageId = Guid.NewGuid() }); break;
        }
        Assert.False(InternalConversationReadScope.MatchesReadReceipt(row, _conversationId, new HashSet<Guid>(), _cutoff));
    }

    [Fact]
    public void ReopeningReadThread_StillSelectsOldMessagesToRepairNotifications()
    {
        var state = Message();
        state.IsRead = true;
        Assert.True(MessagePredicate()(state));
        var notification = Payload(_conversationId, state.InternalMessageId);
        Assert.True(InternalConversationReadScope.MatchesMessage(
            notification, _conversationId, new HashSet<Guid> { state.InternalMessageId }));
    }

    [Theory]
    [InlineData("employee")]
    [InlineData("company")]
    [InlineData("conversation")]
    [InlineData("inactiveParticipant")]
    [InlineData("inactiveConversation")]
    [InlineData("deleted")]
    [InlineData("newMessage")]
    public void Messages_ExcludeOutOfScopeRows(string change)
    {
        var state = Message();
        switch (change)
        {
            case "employee": state.EmployeeId = Guid.NewGuid(); break;
            case "company": state.Message.Conversation.CompanyId = Guid.NewGuid(); break;
            case "conversation": state.Message.InternalConversationId = Guid.NewGuid(); break;
            case "inactiveParticipant": state.Message.Conversation.Participants.Single().IsActive = false; break;
            case "inactiveConversation": state.Message.Conversation.IsActive = false; break;
            case "deleted": state.Message.IsDeleted = true; break;
            case "newMessage": state.Message.SentAt = _cutoff.AddTicks(1); break;
        }
        Assert.False(MessagePredicate()(state));
    }

    [Fact]
    public void Boundary_ExcludesMessagesArrivingAfterLoadedMessageEvenWithSameTimestamp()
    {
        var boundaryId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var predicate = InternalConversationReadScope.Messages(
            _companyId, _employeeId, _conversationId, _cutoff, boundaryId).Compile();
        var state = Message();
        state.InternalMessageId = boundaryId;
        Assert.True(predicate(state));
        state.InternalMessageId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        Assert.False(predicate(state));
        state.Message.SentAt = _cutoff.AddSeconds(-1);
        Assert.True(predicate(state));
    }

    [Fact]
    public void ExplicitBoundary_AcceptsPersistedFutureTimestampAndSynchronizesLinkedNotification()
    {
        var boundaryId = Guid.Parse("01a0f14d-1dd5-7f47-b2ff-f14ea75b0f36");
        var state = Message();
        state.InternalMessageId = boundaryId;
        state.Message.InternalMessageId = boundaryId;
        state.Message.SentAt = _cutoff.AddHours(7);
        var boundaryQuery = InternalConversationReadScope.Boundary(_companyId, _conversationId, boundaryId).Compile();
        // The old SentAt <= API clock condition rejected this persisted, authorized boundary.
        Assert.True(boundaryQuery(state.Message));
        var messageScope = InternalConversationReadScope.Messages(
            _companyId, _employeeId, _conversationId, state.Message.SentAt, boundaryId).Compile();
        Assert.True(messageScope(state));
        var notification = new NotificationUserState
        {
            UserId = _employeeId,
            Notification = new Notification { CompanyId = _companyId, CreatedDate = state.Message.SentAt.AddSeconds(1) }
        };
        Assert.True(InternalConversationReadScope.Notifications(_companyId, _employeeId, null).Compile()(notification));
        Assert.True(InternalConversationReadScope.MatchesMessage(
            Payload(_conversationId, boundaryId), _conversationId, new HashSet<Guid> { boundaryId }));
        state.Message.SentAt = state.Message.SentAt.AddTicks(1);
        Assert.False(messageScope(state));
    }

    [Theory]
    [InlineData("message")]
    [InlineData("conversation")]
    [InlineData("company")]
    [InlineData("inactive")]
    public void ExplicitBoundary_RejectsWrongIdentityAndScope(string change)
    {
        var state = Message();
        var id = state.InternalMessageId;
        state.Message.InternalMessageId = id;
        var query = InternalConversationReadScope.Boundary(_companyId, _conversationId, id).Compile();
        switch (change)
        {
            case "message": state.Message.InternalMessageId = Guid.NewGuid(); break;
            case "conversation": state.Message.InternalConversationId = Guid.NewGuid(); break;
            case "company": state.Message.Conversation.CompanyId = Guid.NewGuid(); break;
            case "inactive": state.Message.Conversation.IsActive = false; break;
        }
        Assert.False(query(state.Message));
    }

    [Fact]
    public void ExplicitBoundary_StillAcceptsDeletedMessageReturnedAsTombstone()
    {
        var state = Message();
        state.Message.InternalMessageId = state.InternalMessageId;
        state.Message.IsDeleted = true;
        Assert.True(InternalConversationReadScope.Boundary(
            _companyId, _conversationId, state.InternalMessageId).Compile()(state.Message));
        Assert.False(MessagePredicate()(state));
    }

    [Fact]
    public void Notifications_ExcludeOtherUsersCompaniesArchivedReadAndNewRows()
    {
        var predicate = InternalConversationReadScope.Notifications(_companyId, _employeeId, _cutoff).Compile();
        NotificationUserState Row() => new()
        {
            UserId = _employeeId,
            Notification = new Notification { CompanyId = _companyId, CreatedDate = _cutoff }
        };
        Assert.True(predicate(Row()));
        var otherUser = Row(); otherUser.UserId = Guid.NewGuid();
        var otherCompany = Row(); otherCompany.Notification.CompanyId = Guid.NewGuid();
        var archived = Row(); archived.IsArchived = true;
        var read = Row(); read.IsRead = true;
        var newer = Row(); newer.Notification.CreatedDate = _cutoff.AddTicks(1);
        Assert.All(new[] { otherUser, otherCompany, archived, read, newer }, row => Assert.False(predicate(row)));
    }

    [Fact]
    public void MultipleNotifications_RequireMatchingThreadAndSnapshotMessage()
    {
        var messageId = Guid.NewGuid();
        var messageIds = new HashSet<Guid> { messageId };
        var rows = new[]
        {
            Payload(_conversationId, messageId),
            Payload(_conversationId, messageId),
            Payload(Guid.NewGuid(), messageId),
            Payload(_conversationId, Guid.NewGuid())
        };
        Assert.Equal(2, rows.Count(row => InternalConversationReadScope.MatchesMessage(row, _conversationId, messageIds)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"conversationId\":\"bad-guid\",\"messageId\":null}")]
    public void MalformedOrUnlinkedNotification_IsNotMarkedRead(string? payload)
    {
        Assert.False(InternalConversationReadScope.MatchesMessage(
            new NotificationDto { PayloadJson = payload }, _conversationId, new HashSet<Guid>()));
    }

    [Fact]
    public void HistoricalPascalCasePayload_IsSupported()
    {
        var messageId = Guid.NewGuid();
        var row = new NotificationDto
        {
            PayloadJson = JsonSerializer.Serialize(new { ConversationId = _conversationId, MessageId = messageId })
        };
        Assert.True(InternalConversationReadScope.MatchesMessage(row, _conversationId, new HashSet<Guid> { messageId }));
    }

    [Fact]
    public void ScopedSnapshotAndNotificationPaging_TranslateToPostgres()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
        var id = Guid.NewGuid();
        var snapshotIds = new[] { id };
        var messages = db.InternalMessageReadStates
            .Where(InternalConversationReadScope.Messages(_companyId, _employeeId, _conversationId, _cutoff, id))
            .Where(state => !state.IsRead && snapshotIds.Contains(state.InternalMessageId)).ToQueryString();
        var notifications = db.NotificationUserStates
            .Where(InternalConversationReadScope.Notifications(_companyId, _employeeId, _cutoff))
            .Where(state => state.Notification.CreatedDate > _cutoff.AddDays(-1) ||
                (state.Notification.CreatedDate == _cutoff.AddDays(-1) && state.NotificationId.CompareTo(id) > 0))
            .OrderBy(state => state.Notification.CreatedDate).ThenBy(state => state.NotificationId)
            .Take(200).ToQueryString();
        Assert.Contains("EXISTS", messages);
        var missingStateSnapshot = db.InternalMessages.Where(InternalConversationReadScope.ConversationMessages(
            _companyId, _employeeId, _conversationId, _cutoff, id)).ToQueryString();
        Assert.Contains("EXISTS", missingStateSnapshot);
        Assert.DoesNotContain("InternalMessageReadStates", missingStateSnapshot);
        Assert.Contains("ORDER BY", notifications);
        Assert.Contains("LIMIT", notifications);
        var boundary = db.InternalMessages.Where(InternalConversationReadScope.Boundary(
            _companyId, _conversationId, id)).Select(message => message.SentAt).ToQueryString();
        Assert.Contains("InternalMessageId", boundary);
        var boundedNotifications = db.NotificationUserStates.Where(
            InternalConversationReadScope.Notifications(_companyId, _employeeId, null)).ToQueryString();
        Assert.Contains("company_id", boundedNotifications);
    }

    private Func<InternalMessageReadState, bool> MessagePredicate() =>
        InternalConversationReadScope.Messages(_companyId, _employeeId, _conversationId, _cutoff).Compile();

    private InternalMessageReadState Message()
    {
        var conversation = new InternalConversation { CompanyId = _companyId, IsActive = true };
        conversation.Participants.Add(new InternalConversationParticipant { EmployeeId = _employeeId, IsActive = true });
        return new InternalMessageReadState
        {
            InternalMessageId = Guid.NewGuid(), EmployeeId = _employeeId,
            Message = new InternalMessage { InternalConversationId = _conversationId, SentAt = _cutoff, Conversation = conversation }
        };
    }

    private static NotificationDto Payload(Guid conversationId, Guid messageId) => new()
    {
        PayloadJson = JsonSerializer.Serialize(new { conversationId, messageId })
    };
}
