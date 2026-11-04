using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetInviteCandidates;

internal sealed class GetInternalMailInviteCandidatesQueryHandler(IInternalMailDbContext db, ICurrentUser user, InternalMailAreaAccessService access)
    : IRequestHandler<GetInternalMailInviteCandidatesQuery, IReadOnlyList<InternalConversationParticipantDto>?>
{
    public async Task<IReadOnlyList<InternalConversationParticipantDto>?> Handle(GetInternalMailInviteCandidatesQuery request, CancellationToken ct)
    {
        var group = await access.Conversations().AsNoTracking().SingleOrDefaultAsync(c => c.InternalConversationId == request.ConversationId &&
            c.Participants.Any(p => p.EmployeeId == user.EmployeeId && p.IsActive), ct);
        if (group is null || !InternalMailAreaAccessService.IsPrivateArea(group.RelatedType)) return null;
        var eligible = access.Recipients(user.CompanyId!.Value, null, request.ConversationId);
        var sources = await db.InternalMessageReferences.Where(r => r.Message.InternalConversationId == request.ConversationId &&
                r.RelatedType == InternalMailRelatedType.ConversationForwardSource).Select(r => r.RelatedId).Distinct().ToArrayAsync(ct);
        var visible = access.Conversations().Select(c => c.InternalConversationId);
        if (sources.Length != await visible.CountAsync(id => sources.Contains(id), ct)) return [];
        var candidates = await db.InternalConversationParticipants.AsNoTracking().Where(p => p.InternalConversationId == group.RelatedId &&
                p.IsActive && eligible.Contains(p.EmployeeId) && !db.InternalConversationParticipants.Any(existing =>
                    existing.InternalConversationId == request.ConversationId && existing.EmployeeId == p.EmployeeId && existing.IsActive) &&
                db.InternalConversationParticipants.Count(member => sources.Contains(member.InternalConversationId) &&
                    member.EmployeeId == p.EmployeeId && member.IsActive && member.Conversation.IsActive && member.Conversation.CompanyId == user.CompanyId) == sources.Length)
            .OrderBy(p => p.Employee.FullName).Select(p => new InternalConversationParticipantDto
            {
                EmployeeId = p.EmployeeId, EmployeeName = p.Employee.FullName, Role = InternalConversationParticipantRole.Member
            }).ToArrayAsync(ct);
        return await access.VisibleParticipantsAsync(group.RelatedType, candidates, ct);
    }
}
