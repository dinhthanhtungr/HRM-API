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

namespace HRM.Application.Features.InternalMail.Commands.ForwardMessage;

internal sealed class ForwardInternalMessageCommandHandler(
    IInternalMailDbContext db, ICurrentUser user, InternalMailAreaAccessService access, IFileStorage files, ISender sender)
    : IRequestHandler<ForwardInternalMessageCommand, OperationResult<SendInternalMessageResultDto>>
{
    public async Task<OperationResult<SendInternalMessageResultDto>> Handle(ForwardInternalMessageCommand request, CancellationToken ct)
    {
        var source = await access.Messages().AsNoTracking().Where(m => m.InternalMessageId == request.MessageId && !m.IsDeleted)
            .Select(m => new { m.Body, m.InternalConversationId, m.Conversation.RelatedId, m.Conversation.RelatedType }).FirstOrDefaultAsync(ct);
        var target = await access.Conversations().AsNoTracking().FirstOrDefaultAsync(c => c.InternalConversationId == request.TargetConversationId &&
            c.Participants.Any(p => p.EmployeeId == user.EmployeeId && p.IsActive), ct);
        if (source is null || target is null || source.InternalConversationId == target.InternalConversationId)
            return OperationResult<SendInternalMessageResultDto>.Fail("Source or destination conversation was not found.");
        var sourceRoot = InternalMailAreaAccessService.IsPrivateArea(source.RelatedType) ? source.RelatedId : source.InternalConversationId;
        var targetRoot = InternalMailAreaAccessService.IsPrivateArea(target.RelatedType) ? target.RelatedId : target.InternalConversationId;
        if (sourceRoot != targetRoot) return OperationResult<SendInternalMessageResultDto>.Fail("Choose an area of the same record.");

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
            return await sender.Send(new SendInternalMessageCommand
            {
                ConversationId = target.InternalConversationId,
                AreaCode = InternalMailAreaAccessService.AreaOf(target.RelatedType),
                Body = source.Body, Attachments = uploads, IsForwarded = true
            }, ct);
        }
        finally
        {
            foreach (var upload in uploads) await upload.Stream.DisposeAsync();
        }
    }
}
