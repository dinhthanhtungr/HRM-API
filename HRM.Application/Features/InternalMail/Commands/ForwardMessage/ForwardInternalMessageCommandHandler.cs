using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.InternalMail.Commands.SendMessage;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Application.Features.InternalMail.Commands.ForwardMessage;

internal sealed class ForwardInternalMessageCommandHandler(
    IInternalMailDbContext db, ICurrentUser user, InternalMailAreaAccessService access, IFileStorage files, ISender sender)
    : IRequestHandler<ForwardInternalMessageCommand, OperationResult<SendInternalMessageResultDto>>
{
    public async Task<OperationResult<SendInternalMessageResultDto>> Handle(ForwardInternalMessageCommand request, CancellationToken ct)
    {
        var source = await access.Messages().AsNoTracking().Where(m => m.InternalMessageId == request.MessageId && !m.IsDeleted)
            .Select(m => new { m.Body, m.InternalConversationId, m.Conversation.RelatedId, m.Conversation.RelatedType,
                SenderName = m.SenderEmployee.FullName }).FirstOrDefaultAsync(ct);
        var target = await access.Conversations().AsNoTracking().FirstOrDefaultAsync(c => c.InternalConversationId == request.TargetConversationId &&
            c.Participants.Any(p => p.EmployeeId == user.EmployeeId && p.IsActive), ct);
        if (source is null || target is null || source.InternalConversationId == target.InternalConversationId)
            return OperationResult<SendInternalMessageResultDto>.Fail("Source or destination conversation was not found.");
        var sourceRoot = await access.RootIdAsync(source.InternalConversationId, ct);
        var targetRoot = await access.RootIdAsync(target.InternalConversationId, ct);
        if (sourceRoot != targetRoot) return OperationResult<SendInternalMessageResultDto>.Fail("Choose an area of the same record.");

        await using var transaction = request.HasGroupTransaction ? null : await db.BeginTransactionAsync(ct);
        if (transaction is not null) await db.LockInternalConversationAsync(user.CompanyId!.Value, sourceRoot, ct);
        if (!await access.Messages().AnyAsync(m => m.InternalMessageId == request.MessageId && !m.IsDeleted, ct) ||
            !await access.Conversations().AnyAsync(c => c.InternalConversationId == target.InternalConversationId &&
                c.Participants.Any(p => p.EmployeeId == user.EmployeeId && p.IsActive), ct))
            return OperationResult<SendInternalMessageResultDto>.Fail("Conversation access changed.");

        var targetMembers = await db.InternalConversationParticipants.AsNoTracking()
            .Where(p => p.InternalConversationId == target.InternalConversationId && p.IsActive)
            .Select(p => p.EmployeeId).ToArrayAsync(ct);
        var sourceIds = await db.InternalMessageReferences.Where(r => r.InternalMessageId == request.MessageId &&
                r.RelatedType == InternalMailRelatedType.ConversationForwardSource).Select(r => r.RelatedId).ToListAsync(ct);
        sourceIds.Add(source.InternalConversationId);
        if (!await access.IsForwardDestinationBoundedAsync(target.RelatedType, sourceIds.Distinct().ToArray(), ct))
            return OperationResult<SendInternalMessageResultDto>.Fail("Restricted messages must stay in a membership-only group. Create a private group or choose Technical.");
        foreach (var sourceId in sourceIds.Distinct())
            if (!await access.CanForwardToMembersAsync(sourceId, targetMembers, ct))
                return OperationResult<SendInternalMessageResultDto>.Fail("Every destination member must have access to the source conversation.");

        var attachments = await db.InternalMessageAttachments.AsNoTracking().Where(a => a.InternalMessageId == request.MessageId && a.Attachment.IsActive)
            .Select(a => new { a.Attachment.StoragePath, a.Attachment.FileName }).ToListAsync(ct);
        var uploads = new List<AttachmentUploadFile>();
        try
        {
            foreach (var attachment in attachments)
            {
                var file = await files.OpenReadAsync(attachment.StoragePath, ct);
                uploads.Add(new AttachmentUploadFile(file.Stream, attachment.FileName, file.ContentType, file.Length));
            }
            // Copy only the selected body/files. Never grant source membership or copy executable action payloads/replies.
            var result = await sender.Send(new SendInternalMessageCommand
            {
                HasGroupTransaction = true,
                ConversationId = target.InternalConversationId,
                AreaCode = InternalMailAreaAccessService.AreaOf(target.RelatedType),
                Body = source.Body, Attachments = uploads, IsForwarded = true,
                ForwardSourceConversationIds = sourceIds.Distinct().ToArray(), ForwardedSenderName = source.SenderName
            }, ct);
            if (result.Success && transaction is not null) await transaction.CommitAsync(ct);
            return result;
        }
        finally
        {
            foreach (var upload in uploads) await upload.Stream.DisposeAsync();
        }
    }
}
