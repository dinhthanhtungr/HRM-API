using System.Linq.Expressions;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.Notifications;

namespace HRM.Application.Features.NotificationHub.Commands.MarkAllRead;

internal static class NotificationHubReadAllScope
{
    public static Expression<Func<NotificationUserState, bool>> UnreadNotifications(
        Guid companyId,
        Guid employeeId) => state =>
            state.UserId == employeeId &&
            !state.IsRead &&
            !state.IsArchived &&
            state.Notification.CompanyId == companyId;

    public static Expression<Func<InternalConversationParticipant, bool>> ConversationsWithUnreadMessages(
        Guid companyId,
        Guid employeeId,
        DateTime readThrough) => participant =>
            participant.EmployeeId == employeeId &&
            participant.IsActive &&
            participant.Conversation.IsActive &&
            participant.Conversation.CompanyId == companyId &&
            (!participant.LastReadAt.HasValue || participant.LastReadAt.Value < readThrough) &&
            participant.Conversation.Messages.Any(message =>
                !message.IsDeleted &&
                message.ReadStates.Any(state =>
                    state.EmployeeId == employeeId &&
                    !state.IsRead));

    public static Expression<Func<InternalMessageReadState, bool>> UnreadMessages(
        Guid companyId,
        Guid employeeId) => state =>
            state.EmployeeId == employeeId &&
            !state.IsRead &&
            !state.Message.IsDeleted &&
            state.Message.Conversation.IsActive &&
            state.Message.Conversation.CompanyId == companyId &&
            state.Message.Conversation.Participants.Any(participant =>
                participant.EmployeeId == employeeId &&
                participant.IsActive);
}
