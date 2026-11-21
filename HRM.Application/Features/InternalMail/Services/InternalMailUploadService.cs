using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.InternalMail.Commands.SendMessage;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Domain.Enums.Attachment;
using HRM.Domain.Security.Rules.Attachment;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

public sealed record StartInternalMailUpload(Guid SessionId, Guid ConversationId, string? AreaCode,
    IReadOnlyList<AttachmentUploadDescriptor> Files);

/// <summary>Phiên upload thuộc đúng người gửi/công ty; quyền gửi được kiểm tra lại mỗi request.</summary>
public sealed class InternalMailUploadService(IResumableAttachmentStore store, ICurrentUser user,
    InternalMailAreaAccessService areas, IInternalMailDbContext db, ISender sender)
{
    private AttachmentUploadOwner Owner => user.IsAuthenticated && user.CompanyId is { } company &&
        user.EmployeeId is { } employee && company != Guid.Empty && employee != Guid.Empty
        ? new(company, employee) : throw new UnauthorizedAccessException();

    private async Task CheckConversationAsync(Guid conversationId, string? areaCode, CancellationToken ct)
    {
        var owner = Owner;
        if (areaCode == InternalMailAreaAccessService.ResearchArea)
        {
            if (!await areas.CanUseResearchAsync(ct) || !await areas.Conversations().AnyAsync(c =>
                c.InternalConversationId == conversationId && c.CompanyId == owner.CompanyId && c.IsActive &&
                c.RelatedType == HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.SampleRequest, ct))
                throw new UnauthorizedAccessException();
            return;
        }
        if (!await areas.SendableConversations().AnyAsync(c => c.InternalConversationId == conversationId &&
                c.CompanyId == owner.CompanyId && c.IsActive, ct)) throw new UnauthorizedAccessException();
    }

    private async Task<AttachmentUploadSession> SessionAsync(Guid id, CancellationToken ct)
    {
        var session = await store.GetAsync(Owner, id, ct);
        await CheckConversationAsync(session.ConversationId, session.AreaCode, ct);
        return session;
    }

    public async Task<AttachmentUploadProgress> StartAsync(StartInternalMailUpload request, CancellationToken ct)
    {
        await CheckConversationAsync(request.ConversationId, request.AreaCode, ct);
        if (request.SessionId == Guid.Empty || request.Files is null || request.Files.Count is < 1 or > 10)
            throw new InvalidOperationException("Choose between 1 and 10 attachments.");
        var rule = AttachmentRules.Map[AttachmentSlot.InternalMail];
        long total = 0;
        foreach (var file in request.Files)
        {
            if (string.IsNullOrWhiteSpace(file.FileName) || file.FileName.Length > 240 ||
                file.FileName != Path.GetFileName(file.FileName) || file.FileName.Contains('\\') ||
                string.IsNullOrWhiteSpace(file.ContentType) || file.ContentType.Length > 150 ||
                file.Length <= 0 || file.Length > rule.MaxBytes ||
                !rule.AllowedMimePrefixes.Any(p => file.ContentType.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("An attachment has an unsupported name, type or size.");
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is ".exe" or ".dll" or ".bat" or ".cmd" or ".ps1" or ".js" or ".vbs" or ".html" or ".svg")
                throw new InvalidOperationException("Executable and active-content attachments are not allowed.");
            if (AttachmentMediaRules.IsVideoFile(file.FileName) != file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Video extension and content type do not match.");
            total = checked(total + file.Length);
        }
        if (total > 50 * AttachmentRules.MB) throw new InvalidOperationException("Total attachment size cannot exceed 50 MB.");
        await store.CreateAsync(Owner, new(request.SessionId, request.ConversationId, request.AreaCode,
            request.Files, DateTimeOffset.UtcNow.AddHours(24)), ct);
        return await store.GetProgressAsync(Owner, request.SessionId, ct);
    }

    public async Task<AttachmentUploadProgress> ProgressAsync(Guid id, CancellationToken ct)
    {
        await SessionAsync(id, ct);
        return await store.GetProgressAsync(Owner, id, ct);
    }

    public async Task<long> AppendAsync(Guid id, int index, long offset, Stream body, CancellationToken ct)
    {
        await SessionAsync(id, ct);
        return await store.AppendAsync(Owner, id, index, offset, body, ct);
    }

    public async Task CancelAsync(Guid id, CancellationToken ct)
    {
        await SessionAsync(id, ct);
        await store.CancelAsync(Owner, id, ct);
    }

    public async Task<OperationResult<SendInternalMessageResultDto>> CompleteAsync(Guid id,
        SendInternalMessageCommand command, CancellationToken ct)
    {
        var session = await SessionAsync(id, ct);
        await using var lease = await store.LockAsync(Owner, id, ct);
        // ID session cũng là ID message: retry sau khi commit không phát lại notification.
        var previous = await db.InternalMessages.AsNoTracking().Where(m => m.InternalMessageId == id &&
                (m.InternalConversationId == session.ConversationId ||
                    (session.AreaCode == InternalMailAreaAccessService.ResearchArea &&
                     m.Conversation.RelatedId == session.ConversationId &&
                     m.Conversation.RelatedExternalId == InternalMailAreaAccessService.ResearchMarker)) &&
                m.SenderEmployeeId == Owner.EmployeeId &&
                m.Conversation.CompanyId == Owner.CompanyId && !m.IsDeleted)
            .Select(m => new SendInternalMessageResultDto { ConversationId = m.InternalConversationId, MessageId = m.InternalMessageId })
            .SingleOrDefaultAsync(ct);
        if (previous is not null) return OperationResult<SendInternalMessageResultDto>.Ok(previous);
        var files = new List<AttachmentUploadFile>();
        try
        {
            for (var index = 0; index < session.Files.Count; index++)
            {
                var descriptor = session.Files[index];
                var stream = await store.OpenAsync(Owner, id, index, ct);
                files.Add(new(stream, descriptor.FileName, descriptor.ContentType, descriptor.Length));
            }
            command.ConversationId = session.ConversationId;
            command.AreaCode = session.AreaCode;
            command.UploadMessageId = id;
            command.Attachments = files;
            return await sender.Send(command, ct);
        }
        finally { foreach (var file in files) await file.Stream.DisposeAsync(); }
    }
}
