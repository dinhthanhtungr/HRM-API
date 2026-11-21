using System.Linq.Expressions;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Application.Features.InternalMail.Services;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetConversationDetail;

internal static class InternalConversationDetailProjection
{
    public static Expression<Func<InternalConversation, InternalConversationDetailDto>> ForEmployee(Guid employeeId,
        IQueryable<InternalMessage>? visibleMessages = null,
        IQueryable<InternalConversation>? conversations = null,
        bool includeLegacyUnread = true)
    {
        if (visibleMessages is null) return UnscopedForEmployee(employeeId, includeLegacyUnread);
        var parents = conversations ?? visibleMessages.Select(message => message.Conversation);
        return x => new InternalConversationDetailDto
        {
            ConversationId = x.InternalConversationId, Subject = x.Subject,
            RelatedType = x.RelatedType, RelatedId = x.RelatedId, RelatedExternalId = x.RelatedExternalId,
            CreatedBy = x.CreatedBy, CreatedByName = x.CreatedByNavigation.FullName, CreatedAt = x.CreatedAt,
            LastMessageAt = visibleMessages.Where(m => m.InternalConversationId == x.InternalConversationId).Max(m => (DateTime?)m.SentAt) ??
                ((x.RelatedType == HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationTechnical ||
                  x.RelatedType == HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationPricing ||
                  x.RelatedType == HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationCustom)
                    ? parents.Where(parent => parent.InternalConversationId == x.RelatedId && parent.CompanyId == x.CompanyId && parent.IsActive)
                        .Select(parent => (DateTime?)parent.CreatedAt).FirstOrDefault() : null) ?? x.CreatedAt,
            LastMessageId = visibleMessages.Where(m => m.InternalConversationId == x.InternalConversationId && !m.IsDeleted)
                .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.InternalMessageId).Select(m => (Guid?)m.InternalMessageId).FirstOrDefault(),
            UnreadCount = includeLegacyUnread ? visibleMessages.Count(m => m.InternalConversationId == x.InternalConversationId && !m.IsDeleted &&
                (m.PayloadJson == null || !EF.Functions.JsonContains(m.PayloadJson, InternalMailMentions.AudienceMarker)) &&
                m.SenderEmployeeId != employeeId && m.ReadStates.Any(s => s.EmployeeId == employeeId && !s.IsRead)) : 0,
            IsArchived = x.Participants.Where(p => p.EmployeeId == employeeId && p.IsActive).Select(p => p.IsArchived).FirstOrDefault(),
            IsMuted = x.Participants.Where(p => p.EmployeeId == employeeId && p.IsActive).Select(p => p.IsMuted).FirstOrDefault(),
            LastReadAt = x.Participants.Where(p => p.EmployeeId == employeeId && p.IsActive).Select(p => p.LastReadAt).FirstOrDefault(),
            Participants = x.Participants.Where(p => p.IsActive).OrderBy(p => p.JoinedAt).Select(p => new InternalConversationParticipantDto
            {
                EmployeeId = p.EmployeeId, EmployeeName = p.Employee.FullName, Role = p.Role, JoinedAt = p.JoinedAt
            }).ToList()
        };
    }

    private static Expression<Func<InternalConversation, InternalConversationDetailDto>> UnscopedForEmployee(Guid employeeId, bool includeLegacyUnread) =>
        x => new InternalConversationDetailDto
            {
                ConversationId = x.InternalConversationId,
                Subject = x.Subject,
                RelatedType = x.RelatedType,
                RelatedId = x.RelatedId,
                RelatedExternalId = x.RelatedExternalId,
                CreatedBy = x.CreatedBy,
                CreatedByName = x.CreatedByNavigation.FullName,
                CreatedAt = x.CreatedAt,
                LastMessageAt = x.LastMessageAt,
                LastMessageId = x.LastMessageId,
                UnreadCount = includeLegacyUnread ? x.Messages.Count(message =>
                    (message.PayloadJson == null || !EF.Functions.JsonContains(message.PayloadJson, InternalMailMentions.AudienceMarker)) &&
                    !message.IsDeleted && message.SenderEmployeeId != employeeId &&
                    message.ReadStates.Any(state => state.EmployeeId == employeeId && !state.IsRead)) : 0,
                IsArchived = x.Participants
                    .Where(participant => participant.EmployeeId == employeeId && participant.IsActive)
                    .Select(participant => participant.IsArchived)
                    .FirstOrDefault(),
                IsMuted = x.Participants
                    .Where(participant => participant.EmployeeId == employeeId && participant.IsActive)
                    .Select(participant => participant.IsMuted)
                    .FirstOrDefault(),
                LastReadAt = x.Participants
                    .Where(participant => participant.EmployeeId == employeeId && participant.IsActive)
                    .Select(participant => participant.LastReadAt)
                    .FirstOrDefault(),
                Participants = x.Participants
                    .Where(participant => participant.IsActive)
                    .OrderBy(participant => participant.JoinedAt)
                    .Select(participant => new InternalConversationParticipantDto
                    {
                        EmployeeId = participant.EmployeeId,
                        EmployeeName = participant.Employee.FullName,
                        Role = participant.Role,
                        JoinedAt = participant.JoinedAt
                    })
                    .ToList()
            };
}
