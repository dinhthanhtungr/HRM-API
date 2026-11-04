using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Commands.EnsureDefaultAreas;

internal sealed class EnsureInternalConversationAreasCommandHandler(
    IInternalMailDbContext db, ICurrentUser user, IDateTimeProvider clock, InternalMailAreaAccessService access)
    : IRequestHandler<EnsureInternalConversationAreasCommand, OperationResult<EnsureInternalMailGroupsResultDto>>
{
    public async Task<OperationResult<EnsureInternalMailGroupsResultDto>> Handle(EnsureInternalConversationAreasCommand request, CancellationToken ct)
    {
        var current = await access.Conversations().AsNoTracking()
            .SingleOrDefaultAsync(c => c.InternalConversationId == request.ConversationId, ct);
        if (current is null) return OperationResult<EnsureInternalMailGroupsResultDto>.Fail("Conversation was not found.");
        var rootId = await access.RootIdAsync(current.InternalConversationId, ct);
        await using var transaction = await db.BeginTransactionAsync(ct);
        await db.LockInternalConversationAsync(user.CompanyId!.Value, rootId, ct);
        if (!await access.Conversations().AnyAsync(c => c.InternalConversationId == request.ConversationId, ct))
            return OperationResult<EnsureInternalMailGroupsResultDto>.Fail("Conversation was not found.");
        var root = await db.InternalConversations.SingleOrDefaultAsync(c => c.InternalConversationId == rootId &&
            c.CompanyId == user.CompanyId && c.IsActive && c.RelatedType == InternalMailRelatedType.SampleRequest, ct);
        if (root is null) return OperationResult<EnsureInternalMailGroupsResultDto>.Fail("Default groups are only available for sample requests.");

        foreach (var type in new[] { InternalMailRelatedType.ConversationTechnical, InternalMailRelatedType.ConversationPricing })
        {
            var child = await access.GroupChildren(rootId, type).Include(c => c.Participants).SingleOrDefaultAsync(ct);
            if (child is null)
            {
                // GROUP-STORAGE-TRANSITION: legacy-write-shape — fields consumed by the shared atomic DbContext writer.
                child = new InternalConversation
                {
                    InternalConversationId = Guid.CreateVersion7(), CompanyId = root.CompanyId,
                    Subject = root.Subject, RelatedType = type, RelatedId = rootId, RelatedExternalId = root.RelatedExternalId,
                    CreatedBy = root.CreatedBy, CreatedAt = clock.Now, LastMessageAt = root.CreatedAt
                };
                db.InternalConversations.Add(child);
            }
            var defaults = await access.DefaultMembersAsync(rootId, InternalMailAreaAccessService.AreaOf(type), ct);
            defaults = await access.ConstrainDefaultMembersToForwardSourcesAsync(child.InternalConversationId, defaults, ct);
            // Never reactivate explicitly removed members or replace invitations/preferences.
            var existing = child.Participants.Select(p => p.EmployeeId).ToHashSet();
            foreach (var employee in defaults.Where(id => !existing.Contains(id)))
                child.Participants.Add(new InternalConversationParticipant
                {
                    InternalConversationId = child.InternalConversationId, EmployeeId = employee,
                    Role = InternalConversationParticipantRole.Member, JoinedAt = clock.Now
                });
        }
        var changed = await db.SaveChangesAsync(ct) > 0;
        await transaction.CommitAsync(ct);
        return OperationResult<EnsureInternalMailGroupsResultDto>.Ok(new EnsureInternalMailGroupsResultDto(changed));
    }
}
