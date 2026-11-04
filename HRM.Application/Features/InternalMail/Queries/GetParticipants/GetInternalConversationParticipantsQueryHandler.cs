using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetParticipants;

internal sealed class GetInternalConversationParticipantsQueryHandler(InternalMailAreaAccessService areas, ICurrentUser user)
    : IRequestHandler<GetInternalConversationParticipantsQuery, IReadOnlyList<InternalConversationParticipantDto>?>
{
    public async Task<IReadOnlyList<InternalConversationParticipantDto>?> Handle(GetInternalConversationParticipantsQuery request, CancellationToken ct)
    {
        var result = await areas.Conversations().AsNoTracking().Where(c => c.InternalConversationId == request.ConversationId)
            .Select(c => new
            {
                c.RelatedType,
                Participants = c.Participants.Where(p => p.IsActive).OrderBy(p => p.JoinedAt)
                    .Select(p => new InternalConversationParticipantDto
                    {
                        EmployeeId = p.EmployeeId, EmployeeName = p.Employee.FullName, Role = p.Role, JoinedAt = p.JoinedAt
                    }).ToList()
            }).FirstOrDefaultAsync(ct);
        if (result is null || (request.AreaCode != null && request.AreaCode != InternalMailAreaAccessService.AreaOf(result.RelatedType))) return null;
        var visible = await areas.VisibleParticipantsAsync(result.RelatedType, result.Participants, ct);
        if (request.AreaCode is null) return await areas.WithReaderGroupsAsync(visible, ct);

        var memberIds = result.Participants.Select(p => p.EmployeeId).ToArray();
        var eligible = await areas.Recipients(user.CompanyId!.Value, request.AreaCode, request.ConversationId)
            .Where(id => memberIds.Contains(id)).ToHashSetAsync(ct);
        return await areas.WithReaderGroupsAsync(visible.Where(p => eligible.Contains(p.EmployeeId)).ToArray(), ct);
    }
}
