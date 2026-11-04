using System.Linq.Expressions;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Application.Features.InternalMail.Services;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetConversations;

internal static class InternalConversationUnreadFilter
{
    public static Expression<Func<InternalConversationParticipant, bool>> ForEmployee(
        Guid employeeId, IQueryable<InternalConversationNotificationUnreadCount> unreadNotifications)
        => participant => unreadNotifications.Any(notification =>
                notification.ConversationId == participant.InternalConversationId) ||
            participant.Conversation.Messages.Any(message =>
                !message.IsDeleted && message.SenderEmployeeId != employeeId &&
                (message.PayloadJson == null || !EF.Functions.JsonContains(message.PayloadJson, InternalMailMentions.AudienceMarker)) &&
                message.ReadStates.Any(state => state.EmployeeId == employeeId && !state.IsRead));
}
