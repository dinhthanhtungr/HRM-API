using HRM.Application.Features.NotificationHub.Commands.MarkAllRead;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;

namespace HRM.Application.Tests.Features.NotificationHub;

public sealed class NotificationHubReadAllScopeTests
{
    [Fact]
    public void UnreadNotifications_OnlyMatchesCurrentEmployeeAndCompany()
    {
        var companyId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var predicate = NotificationHubReadAllScope
            .UnreadNotifications(companyId, employeeId)
            .Compile();

        Assert.True(predicate(CreateNotificationState(companyId, employeeId)));
        Assert.False(predicate(CreateNotificationState(Guid.NewGuid(), employeeId)));
        Assert.False(predicate(CreateNotificationState(companyId, Guid.NewGuid())));
        Assert.False(predicate(CreateNotificationState(companyId, employeeId, isRead: true)));
        Assert.False(predicate(CreateNotificationState(companyId, employeeId, isArchived: true)));
    }

    [Fact]
    public void UnreadMessages_RequiresActiveParticipantInCurrentCompany()
    {
        var companyId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var predicate = NotificationHubReadAllScope
            .UnreadMessages(companyId, employeeId)
            .Compile();

        Assert.True(predicate(CreateMessageReadState(companyId, employeeId)));
        Assert.False(predicate(CreateMessageReadState(Guid.NewGuid(), employeeId)));
        Assert.False(predicate(CreateMessageReadState(companyId, employeeId, participantIsActive: false)));
        Assert.False(predicate(CreateMessageReadState(companyId, employeeId, conversationIsActive: false)));
        Assert.False(predicate(CreateMessageReadState(companyId, employeeId, messageIsDeleted: true)));
    }

    [Fact]
    public void ReadAll_UsesTransactionSnapshotInsteadOfIndependentTimestamps_AndKeepsReadMarkerMonotonic()
    {
        var company = Guid.NewGuid();
        var employee = Guid.NewGuid();
        var cutoff = new DateTime(2026, 9, 30, 12, 0, 0);
        var notification = CreateNotificationState(company, employee);
        notification.Notification.CreatedDate = cutoff.AddTicks(1);
        // Message creation and notification publication can straddle the request time in ONE sender transaction.
        // Independent timestamp predicates would split them again; the shared MVCC snapshot owns the boundary.
        Assert.True(NotificationHubReadAllScope.UnreadNotifications(company, employee).Compile()(notification));

        var state = CreateMessageReadState(company, employee);
        var participant = state.Message.Conversation.Participants.Single();
        state.Message.ReadStates.Add(state);
        participant.Conversation.Messages.Add(state.Message);
        state.Message.SentAt = cutoff;
        Assert.True(NotificationHubReadAllScope.UnreadMessages(company, employee).Compile()(state));
        Assert.True(NotificationHubReadAllScope.ConversationsWithUnreadMessages(company, employee, cutoff).Compile()(participant));
        participant.LastReadAt = cutoff.AddMinutes(1);
        Assert.False(NotificationHubReadAllScope.ConversationsWithUnreadMessages(company, employee, cutoff).Compile()(participant));
    }

    private static NotificationUserState CreateNotificationState(
        Guid companyId,
        Guid employeeId,
        bool isRead = false,
        bool isArchived = false) => new()
        {
            UserId = employeeId,
            IsRead = isRead,
            IsArchived = isArchived,
            Notification = new Notification { CompanyId = companyId }
        };

    private static InternalMessageReadState CreateMessageReadState(
        Guid companyId,
        Guid employeeId,
        bool participantIsActive = true,
        bool conversationIsActive = true,
        bool messageIsDeleted = false)
    {
        var conversation = new InternalConversation
        {
            CompanyId = companyId,
            IsActive = conversationIsActive
        };
        conversation.Participants.Add(new InternalConversationParticipant
        {
            EmployeeId = employeeId,
            IsActive = participantIsActive,
            Conversation = conversation
        });

        return new InternalMessageReadState
        {
            EmployeeId = employeeId,
            Message = new InternalMessage
            {
                IsDeleted = messageIsDeleted,
                Conversation = conversation
            }
        };
    }
}
