using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Commands.ForwardMessage;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Commands.ForwardToGroup;

internal sealed class ForwardToInternalGroupCommandHandler(
    IInternalMailDbContext db, ICurrentUser user, IDateTimeProvider clock, InternalMailAreaAccessService access, ISender sender)
    : IRequestHandler<ForwardToInternalGroupCommand, OperationResult<InternalMailAreaDto>>
{
    public async Task<OperationResult<InternalMailAreaDto>> Handle(ForwardToInternalGroupCommand request, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var messageIds = request.MessageIds.Distinct().ToArray();
        var members = request.EmployeeIds.Append(user.EmployeeId ?? Guid.Empty).Distinct().ToArray();
        if (name.Length is < 1 or > 100 || messageIds.Length > 20 || members.Length is < 2 or > 50 || members.Contains(Guid.Empty))
            return OperationResult<InternalMailAreaDto>.Fail("Provide a group name, up to 20 messages and 2–50 members.");
        var sourceQuery = messageIds.Length == 0 ? access.SendableConversations() : access.Conversations()
            .Where(c => c.Participants.Any(p => p.EmployeeId == user.EmployeeId && p.IsActive));
        var source = await sourceQuery.AsNoTracking().SingleOrDefaultAsync(c => c.InternalConversationId == request.ConversationId, ct);
        if (source is null) return OperationResult<InternalMailAreaDto>.Fail("Source conversation was not found.");
        var rootId = await access.RootIdAsync(source.InternalConversationId, ct);
        var root = await db.InternalConversations.AsNoTracking().Where(c => c.InternalConversationId == rootId && c.CompanyId == user.CompanyId &&
                c.IsActive && c.RelatedType == InternalMailRelatedType.SampleRequest).Select(c => new { c.CreatedAt }).SingleOrDefaultAsync(ct);
        if (root is null)
            return OperationResult<InternalMailAreaDto>.Fail("Choose a sample request conversation.");
        await using var transaction = await db.BeginTransactionAsync(ct);
        await db.LockInternalConversationAsync(user.CompanyId!.Value, rootId, ct);
        var messages = await access.Messages().Where(m => m.InternalConversationId == request.ConversationId &&
                messageIds.Contains(m.InternalMessageId) && !m.IsDeleted)
            .OrderBy(m => m.SentAt).ThenBy(m => m.InternalMessageId).Select(m => m.InternalMessageId).ToArrayAsync(ct);
        // Observer đã có quyền gửi không cần membership nguồn để tạo nhóm rỗng; không nới quyền forward tin cũ.
        var audienceToCheck = messageIds.Length == 0 && access.CanSendCompanyMessages
            ? members.Where(id => id != user.EmployeeId).ToArray() : members;
        // Empty groups may invite company employees without granting source membership or copying any messages.
        var eligibleAudience = messageIds.Length == 0
            ? await access.CanInviteToEmptyGroupAsync(source.InternalConversationId, audienceToCheck, ct)
            : await access.CanForwardToMembersAsync(source.InternalConversationId, audienceToCheck, ct);
        if (messages.Length != messageIds.Length || !eligibleAudience)
            return OperationResult<InternalMailAreaDto>.Fail("Messages or selected members are not eligible for forwarding.");
        var sources = await db.InternalMessageReferences.Where(r => messageIds.Contains(r.InternalMessageId) &&
                r.RelatedType == InternalMailRelatedType.ConversationForwardSource).Select(r => r.RelatedId).Distinct().ToArrayAsync(ct);
        foreach (var original in sources)
            if (!await access.CanForwardToMembersAsync(original, members, ct))
                return OperationResult<InternalMailAreaDto>.Fail("Members must be allowed to view all original messages.");
        // GROUP-STORAGE-TRANSITION: legacy-write-shape — shared application writer persists mapping atomically from this root/type.
        var group = new InternalConversation
        {
            InternalConversationId = Guid.CreateVersion7(), CompanyId = user.CompanyId.Value,
            Subject = name, RelatedType = InternalMailRelatedType.ConversationCustom, RelatedId = rootId,
            RelatedExternalId = source.RelatedExternalId, CreatedBy = user.EmployeeId!.Value,
            CreatedAt = clock.Now, LastMessageAt = messageIds.Length == 0 ? root.CreatedAt : clock.Now
        };
        foreach (var employee in members)
            group.Participants.Add(new InternalConversationParticipant
            {
                InternalConversationId = group.InternalConversationId, EmployeeId = employee, JoinedAt = clock.Now,
                Role = employee == user.EmployeeId ? InternalConversationParticipantRole.Owner : InternalConversationParticipantRole.Member
            });
        db.InternalConversations.Add(group);
        await db.SaveChangesAsync(ct);
        foreach (var message in messages)
        {
            var result = await sender.Send(new ForwardInternalMessageCommand { MessageId = message, TargetConversationId = group.InternalConversationId, HasGroupTransaction = true }, ct);
            if (!result.Success) return OperationResult<InternalMailAreaDto>.Fail(result.Message ?? "Forwarding failed.");
        }
        await transaction.CommitAsync(ct);
        return OperationResult<InternalMailAreaDto>.Ok(new InternalMailAreaDto(InternalMailAreas.Custom, group.InternalConversationId, true, 0, Name: name));
    }
}
