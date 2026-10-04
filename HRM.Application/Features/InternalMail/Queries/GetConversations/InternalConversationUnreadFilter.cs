using System.Linq.Expressions;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Domain.Entities.InternalMailSchema;

namespace HRM.Application.Features.InternalMail.Queries.GetConversations;

internal static class InternalConversationUnreadFilter
{
    public static Expression<Func<InternalConversationParticipant, bool>> ForEmployee(
        Guid employeeId, IQueryable<InternalConversationNotificationUnreadCount> unreadNotifications)
        => participant => unreadNotifications.Any(notification =>
                notification.ConversationId == participant.InternalConversationId) ||
            participant.Conversation.Messages.Any(message =>
                !message.IsDeleted && message.SenderEmployeeId != employeeId &&
                message.ReadStates.Any(state => state.EmployeeId == employeeId && !state.IsRead));
}
