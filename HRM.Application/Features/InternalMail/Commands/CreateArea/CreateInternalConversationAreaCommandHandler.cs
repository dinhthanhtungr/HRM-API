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

namespace HRM.Application.Features.InternalMail.Commands.CreateArea;

internal sealed class CreateInternalConversationAreaCommandHandler(
    IInternalMailDbContext db, ICurrentUser user, IDateTimeProvider clock, InternalMailAreaAccessService access)
    : IRequestHandler<CreateInternalConversationAreaCommand, OperationResult<InternalMailAreaDto>>
{
    public async Task<OperationResult<InternalMailAreaDto>> Handle(CreateInternalConversationAreaCommand request, CancellationToken ct)
    {
        if (request.AreaCode is not (InternalMailAreas.Technical or InternalMailAreas.Pricing) ||
            !user.CompanyId.HasValue || !user.EmployeeId.HasValue)
            return OperationResult<InternalMailAreaDto>.Fail("Invalid conversation area.");
        var source = await access.Conversations().FirstOrDefaultAsync(c => c.InternalConversationId == request.ConversationId, ct);
        if (source is null) return OperationResult<InternalMailAreaDto>.Fail("Conversation was not found.");
        var rootId = InternalMailAreaAccessService.IsPrivateArea(source.RelatedType) ? source.RelatedId!.Value : source.InternalConversationId;
        var root = await access.Conversations().FirstOrDefaultAsync(c => c.InternalConversationId == rootId &&
            c.Participants.Any(p => p.EmployeeId == user.EmployeeId && p.IsActive && p.Role == InternalConversationParticipantRole.Owner), ct);
        if (root is null) return OperationResult<InternalMailAreaDto>.Fail("Only the parent conversation owner can create an area.");
        var type = request.AreaCode == InternalMailAreas.Technical ? InternalMailRelatedType.ConversationTechnical : InternalMailRelatedType.ConversationPricing;
        var members = (request.EmployeeIds ?? []).Where(id => id != Guid.Empty).Append(user.EmployeeId.Value).Distinct().ToArray();
        var eligible = await access.Recipients(user.CompanyId.Value, null, rootId).Where(id => members.Contains(id)).ToArrayAsync(ct);
        if (eligible.Length != members.Length) return OperationResult<InternalMailAreaDto>.Fail("Some employees cannot participate in this record.");

        await using var transaction = await db.BeginTransactionAsync(ct);
        // Lock the existing parent row: works across API instances without a schema/index change.
        await db.LockInternalConversationAsync(user.CompanyId.Value, rootId, ct);
        if (!await access.Conversations().AnyAsync(c => c.InternalConversationId == rootId &&
            c.Participants.Any(p => p.EmployeeId == user.EmployeeId && p.IsActive && p.Role == InternalConversationParticipantRole.Owner), ct))
            return OperationResult<InternalMailAreaDto>.Fail("Conversation was not found or ownership changed.");
        var existing = await db.InternalConversations.FirstOrDefaultAsync(c => c.CompanyId == user.CompanyId &&
            c.RelatedId == rootId && c.RelatedType == type && c.IsActive, ct);
        if (existing is not null)
        {
            if (!await access.Conversations().AnyAsync(c => c.InternalConversationId == existing.InternalConversationId, ct))
                return OperationResult<InternalMailAreaDto>.Fail("Area already exists; ask its owner for access.");
            return OperationResult<InternalMailAreaDto>.Ok(new InternalMailAreaDto(request.AreaCode, existing.InternalConversationId, true, 0));
        }
        var now = clock.Now;
        var child = new InternalConversation
        {
            InternalConversationId = Guid.CreateVersion7(), CompanyId = user.CompanyId.Value,
            Subject = root.Subject, RelatedType = type, RelatedId = rootId, RelatedExternalId = root.RelatedExternalId,
            CreatedBy = user.EmployeeId.Value, CreatedAt = now, LastMessageAt = now, IsActive = true
        };
        db.InternalConversations.Add(child);
        foreach (var id in members)
            child.Participants.Add(new InternalConversationParticipant
            {
                EmployeeId = id, InternalConversationId = child.InternalConversationId, IsActive = true, JoinedAt = now,
                Role = id == user.EmployeeId.Value ? InternalConversationParticipantRole.Owner : InternalConversationParticipantRole.Member
            });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return OperationResult<InternalMailAreaDto>.Ok(new InternalMailAreaDto(request.AreaCode, child.InternalConversationId, true, 0));
    }
}
