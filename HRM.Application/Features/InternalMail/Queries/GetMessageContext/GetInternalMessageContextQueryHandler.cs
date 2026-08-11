using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetMessageContext;

internal sealed class GetInternalMessageContextQueryHandler
    : IRequestHandler<GetInternalMessageContextQuery, InternalMessageContextDto?>
{
    private const int MaxContextSize = 50;
    private const int MaxReplyPreviewLength = 300;

    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetInternalMessageContextQueryHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<InternalMessageContextDto?> Handle(
        GetInternalMessageContextQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (request.ConversationId == Guid.Empty ||
            request.MessageId == Guid.Empty ||
            !companyId.HasValue ||
            !employeeId.HasValue)
        {
            return null;
        }

        var canRead = await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .AnyAsync(x =>
                x.InternalConversationId == request.ConversationId &&
                x.EmployeeId == employeeId.Value &&
                x.IsActive &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive,
                cancellationToken);

        if (!canRead)
        {
            return null;
        }

        var target = await _dbContext.InternalMessages
            .AsNoTracking()
            .Where(x =>
                x.InternalConversationId == request.ConversationId &&
                x.InternalMessageId == request.MessageId &&
                !x.IsDeleted)
            .Select(x => new
            {
                x.InternalMessageId,
                x.SentAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (target is null)
        {
            return null;
        }

        var before = Math.Clamp(request.Before, 0, MaxContextSize);
        var after = Math.Clamp(request.After, 0, MaxContextSize);

        var olderIds = await _dbContext.InternalMessages
            .AsNoTracking()
            .Where(x =>
                x.InternalConversationId == request.ConversationId &&
                !x.IsDeleted &&
                x.SentAt < target.SentAt)
            .OrderByDescending(x => x.SentAt)
            .ThenByDescending(x => x.InternalMessageId)
            .Take(before)
            .Select(x => x.InternalMessageId)
            .ToListAsync(cancellationToken);

        var newerIds = await _dbContext.InternalMessages
            .AsNoTracking()
            .Where(x =>
                x.InternalConversationId == request.ConversationId &&
                !x.IsDeleted &&
                x.SentAt > target.SentAt)
            .OrderBy(x => x.SentAt)
            .ThenBy(x => x.InternalMessageId)
            .Take(after)
            .Select(x => x.InternalMessageId)
            .ToListAsync(cancellationToken);

        var messageIds = olderIds
            .Concat(new[] { target.InternalMessageId })
            .Concat(newerIds)
            .ToArray();

        var messages = await _dbContext.InternalMessages
            .AsNoTracking()
            .Where(x => messageIds.Contains(x.InternalMessageId))
            .OrderBy(x => x.SentAt)
            .ThenBy(x => x.InternalMessageId)
            .Select(x => new InternalMessageDto
            {
                MessageId = x.InternalMessageId,
                ConversationId = x.InternalConversationId,
                SenderEmployeeId = x.SenderEmployeeId,
                SenderName = x.SenderEmployee.FullName,
                MessageType = x.MessageType,
                Body = x.Body,
                PayloadJson = x.PayloadJson,
                ReplyToMessageId = x.ReplyToMessageId,
                ReplyTo = x.ReplyToMessageId == null ? null : new InternalMessageReplyDto
                {
                    MessageId = x.ReplyToMessage!.InternalMessageId,
                    SenderEmployeeId = x.ReplyToMessage.SenderEmployeeId,
                    SenderName = x.ReplyToMessage.SenderEmployee.FullName,
                    BodyPreview = x.ReplyToMessage.IsDeleted
                        ? string.Empty
                        : x.ReplyToMessage.Body.Length <= MaxReplyPreviewLength
                            ? x.ReplyToMessage.Body
                            : x.ReplyToMessage.Body.Substring(0, MaxReplyPreviewLength),
                    MessageType = x.ReplyToMessage.MessageType,
                    IsDeleted = x.ReplyToMessage.IsDeleted
                },
                IsUrgent = x.IsUrgent,
                SentAt = x.SentAt,
                IsEdited = x.IsEdited,
                EditedAt = x.EditedAt,
                IsDeleted = x.IsDeleted,
                IsRead = x.ReadStates
                    .Where(state => state.EmployeeId == employeeId.Value)
                    .Select(state => state.IsRead)
                    .FirstOrDefault(),
                ReadAt = x.ReadStates
                    .Where(state => state.EmployeeId == employeeId.Value)
                    .Select(state => state.ReadAt)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        await AttachReferencesAndAttachmentsAsync(messages, cancellationToken);

        var firstSentAt = messages.FirstOrDefault()?.SentAt ?? target.SentAt;
        var lastSentAt = messages.LastOrDefault()?.SentAt ?? target.SentAt;
        var hasOlder = await _dbContext.InternalMessages
            .AsNoTracking()
            .AnyAsync(x =>
                x.InternalConversationId == request.ConversationId &&
                !x.IsDeleted &&
                x.SentAt < firstSentAt,
                cancellationToken);
        var hasNewer = await _dbContext.InternalMessages
            .AsNoTracking()
            .AnyAsync(x =>
                x.InternalConversationId == request.ConversationId &&
                !x.IsDeleted &&
                x.SentAt > lastSentAt,
                cancellationToken);

        return new InternalMessageContextDto
        {
            TargetMessageId = target.InternalMessageId,
            ConversationId = request.ConversationId,
            Messages = messages,
            HasOlderMessages = hasOlder,
            HasNewerMessages = hasNewer
        };
    }

    private async Task AttachReferencesAndAttachmentsAsync(
        IReadOnlyCollection<InternalMessageDto> messages,
        CancellationToken cancellationToken)
    {
        var messageIds = messages.Select(x => x.MessageId).ToArray();
        if (messageIds.Length == 0)
        {
            return;
        }

        var references = await _dbContext.InternalMessageReferences
            .AsNoTracking()
            .Where(x => messageIds.Contains(x.InternalMessageId))
            .Select(x => new
            {
                x.InternalMessageId,
                Item = new InternalMessageReferenceDto
                {
                    ReferenceId = x.InternalMessageReferenceId,
                    RelatedType = x.RelatedType,
                    RelatedId = x.RelatedId,
                    RelatedExternalId = x.RelatedExternalId,
                    RelatedNameSnapshot = x.RelatedNameSnapshot,
                    SnapshotJson = x.SnapshotJson,
                    IsPrimary = x.IsPrimary
                }
            })
            .ToListAsync(cancellationToken);

        var attachments = await _dbContext.InternalMessageAttachments
            .AsNoTracking()
            .Where(x => messageIds.Contains(x.InternalMessageId) && x.Attachment.IsActive)
            .Select(x => new
            {
                x.InternalMessageId,
                Item = new InternalMessageAttachmentDto
                {
                    AttachmentId = x.AttachmentId,
                    FileName = x.Attachment.FileName,
                    SizeBytes = x.Attachment.SizeBytes
                }
            })
            .ToListAsync(cancellationToken);

        foreach (var item in messages)
        {
            item.References = references
                .Where(x => x.InternalMessageId == item.MessageId)
                .Select(x => x.Item)
                .ToList();
            var messageAttachments = attachments
                .Where(x => x.InternalMessageId == item.MessageId)
                .Select(x => x.Item)
                .ToList();
            foreach (var attachment in messageAttachments)
            {
                InternalMessageAttachmentPresentation.Enrich(attachment);
            }

            item.Attachments = messageAttachments;
        }
    }
}
