using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.FileStorage;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Attachments.Dtos;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.Attachment;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Security.Rules.Attachment;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Commands.SendMessage;

internal sealed class SendInternalMessageCommandHandler
    : IRequestHandler<SendInternalMessageCommand, OperationResult<SendInternalMessageResultDto>>
{
    private const int MaxBodyLength = 2000;
    private const int MaxAttachmentCount = 10;
    private const long MaxTotalAttachmentBytes = 50 * AttachmentRules.MB;
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly INotificationService _notificationService;
    private readonly IFileStorage _fileStorage;
    private readonly IImageThumbnailGenerator _thumbnailGenerator;

    public SendInternalMessageCommandHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        INotificationService notificationService,
        IFileStorage fileStorage,
        IImageThumbnailGenerator thumbnailGenerator)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _notificationService = notificationService;
        _fileStorage = fileStorage;
        _thumbnailGenerator = thumbnailGenerator;
    }

    public async Task<OperationResult<SendInternalMessageResultDto>> Handle(
        SendInternalMessageCommand request,
        CancellationToken cancellationToken)
    {
        var body = request.Body?.Trim() ?? string.Empty;
        var attachments = request.Attachments ?? Array.Empty<AttachmentUploadFile>();
        if (request.ConversationId == Guid.Empty ||
            (string.IsNullOrWhiteSpace(body) && attachments.Count == 0) ||
            body.Length > MaxBodyLength)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(
                $"A message body or attachment is required. Body cannot exceed {MaxBodyLength} characters.");
        }

        var attachmentValidationError = ValidateAttachments(attachments);
        if (attachmentValidationError is not null)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(attachmentValidationError);
        }

        var companyId = _currentUser.CompanyId;
        var senderId = _currentUser.EmployeeId;
        if (!companyId.HasValue || !senderId.HasValue)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Current employee or company is invalid.");
        }

        var conversation = await _dbContext.InternalConversations
            .FirstOrDefaultAsync(x =>
                x.InternalConversationId == request.ConversationId &&
                x.CompanyId == companyId.Value &&
                x.IsActive &&
                x.Participants.Any(participant => participant.EmployeeId == senderId.Value && participant.IsActive),
                cancellationToken);
        if (conversation is null)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Conversation was not found.");
        }

        if (request.ReplyToMessageId is { } replyId && replyId != Guid.Empty)
        {
            var replyExists = await _dbContext.InternalMessages
                .AsNoTracking()
                .AnyAsync(x =>
                    x.InternalMessageId == replyId &&
                    x.InternalConversationId == conversation.InternalConversationId &&
                    !x.IsDeleted,
                    cancellationToken);
            if (!replyExists)
            {
                return OperationResult<SendInternalMessageResultDto>.Fail("Reply message was not found in this conversation.");
            }
        }

        var participants = await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .Where(x => x.InternalConversationId == conversation.InternalConversationId && x.IsActive)
            .Select(x => new { x.EmployeeId, x.IsMuted })
            .ToListAsync(cancellationToken);

        var now = _dateTimeProvider.Now;
        var messageId = Guid.CreateVersion7();
        var payload = new InternalMailNotificationPayload
        {
            ConversationId = conversation.InternalConversationId,
            MessageId = messageId,
            RelatedType = conversation.RelatedType?.ToString(),
            RelatedId = conversation.RelatedId,
            IsUrgent = request.IsUrgent,
            AttachmentCount = attachments.Count
        };

        var message = new InternalMessage
        {
            InternalMessageId = messageId,
            InternalConversationId = conversation.InternalConversationId,
            SenderEmployeeId = senderId.Value,
            MessageType = InternalMessageType.Text,
            Body = body,
            PayloadJson = JsonSerializer.Serialize(payload),
            ReplyToMessageId = request.ReplyToMessageId is { } validReplyId && validReplyId != Guid.Empty
                ? validReplyId
                : null,
            IsUrgent = request.IsUrgent,
            SentAt = now
        };

        var savedPaths = new List<string>(attachments.Count);
        try
        {
            await _dbContext.InternalMessages.AddAsync(message, cancellationToken);
            await AttachFilesAsync(messageId, attachments, now, savedPaths, cancellationToken);

            foreach (var participant in participants)
            {
                await _dbContext.InternalMessageReadStates.AddAsync(new InternalMessageReadState
                {
                    InternalMessageId = messageId,
                    EmployeeId = participant.EmployeeId,
                    IsRead = participant.EmployeeId == senderId.Value,
                    ReadAt = participant.EmployeeId == senderId.Value ? now : null
                }, cancellationToken);
            }

            conversation.LastMessageId = messageId;
            conversation.LastMessageAt = now;

            var senderParticipant = await _dbContext.InternalConversationParticipants
                .FirstAsync(x =>
                    x.InternalConversationId == conversation.InternalConversationId &&
                    x.EmployeeId == senderId.Value &&
                    x.IsActive,
                    cancellationToken);
            senderParticipant.LastReadAt = now;
            senderParticipant.IsArchived = false;
            senderParticipant.ArchivedAt = null;

            var senderName = await _dbContext.Employees
                .AsNoTracking()
                .Where(x => x.EmployeeId == senderId.Value)
                .Select(x => x.FullName)
                .FirstOrDefaultAsync(cancellationToken);
            var targetUserIds = participants
                .Where(x => x.EmployeeId != senderId.Value && !x.IsMuted)
                .Select(x => x.EmployeeId)
                .ToArray();

            var notificationId = await _notificationService.PublishAsync(new PublishNotificationRequest
            {
                CompanyId = companyId.Value,
                CreatedBy = senderId.Value,
                CreatedByNameSnapshot = senderName ?? _currentUser.UserName,
                Topic = ResolveTopic(conversation.RelatedType),
                Severity = request.IsUrgent ? NotificationSeverity.Warning : NotificationSeverity.Info,
                Title = conversation.Subject,
                Message = string.IsNullOrWhiteSpace(body) ? $"{attachments.Count} attachment(s)" : body,
                Link = $"/internal-mail/conversations/{conversation.InternalConversationId}",
                AggregateId = conversation.RelatedId,
                AggregateCode = conversation.RelatedExternalId,
                ConversationId = conversation.InternalConversationId,
                MessageId = messageId,
                PayloadJson = JsonSerializer.Serialize(payload),
                TargetUserIds = targetUserIds
            }, cancellationToken);

            return OperationResult<SendInternalMessageResultDto>.Ok(new SendInternalMessageResultDto
            {
                ConversationId = conversation.InternalConversationId,
                MessageId = messageId,
                NotificationId = notificationId
            });
        }
        catch
        {
            foreach (var storagePath in savedPaths)
            {
                try
                {
                    await _fileStorage.DeleteAsync(storagePath, CancellationToken.None);
                }
                catch
                {
                    // Keep the original send failure as the reported error.
                }
            }

            throw;
        }
    }

    private static string? ValidateAttachments(IReadOnlyList<AttachmentUploadFile> attachments)
    {
        if (attachments.Count > MaxAttachmentCount)
        {
            return $"A message can contain at most {MaxAttachmentCount} attachments.";
        }

        if (attachments.Sum(x => x.Length) > MaxTotalAttachmentBytes)
        {
            return "Total attachment size cannot exceed 50 MB.";
        }

        var rule = AttachmentRules.Map[AttachmentSlot.InternalMail];
        foreach (var attachment in attachments)
        {
            if (attachment.Stream is null || !attachment.Stream.CanRead ||
                string.IsNullOrWhiteSpace(attachment.FileName) ||
                string.IsNullOrWhiteSpace(attachment.ContentType) ||
                attachment.Length <= 0)
            {
                return "One or more attachments are invalid.";
            }

            if (attachment.Length > rule.MaxBytes)
            {
                return $"Attachment {attachment.FileName} exceeds the maximum allowed size.";
            }

            if (!rule.AllowedMimePrefixes.Any(prefix =>
                    attachment.ContentType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Attachment {attachment.FileName} has an unsupported content type.";
            }
        }

        return null;
    }

    private async Task AttachFilesAsync(
        Guid messageId,
        IReadOnlyList<AttachmentUploadFile> attachments,
        DateTime now,
        ICollection<string> savedPaths,
        CancellationToken cancellationToken)
    {
        if (attachments.Count == 0)
        {
            return;
        }

        var collectionId = Guid.CreateVersion7();
        await _dbContext.AttachmentCollections.AddAsync(new AttachmentCollection
        {
            AttachmentCollectionId = collectionId
        }, cancellationToken);

        var relativeFolder = $"collections/internal-mail/{collectionId:N}";
        foreach (var file in attachments)
        {
            var storagePath = await _fileStorage.SaveAsync(
                file.Stream,
                file.ContentType,
                file.FileName,
                relativeFolder,
                cancellationToken);
            savedPaths.Add(storagePath);

            var attachmentId = Guid.CreateVersion7();
            if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                var storedOriginal = await _fileStorage.OpenReadAsync(storagePath, cancellationToken);
                await using (storedOriginal.Stream)
                {
                    await using var thumbnail = await _thumbnailGenerator.GenerateWebpAsync(
                        storedOriginal.Stream,
                        InternalMailThumbnailStorage.MaxWidth,
                        InternalMailThumbnailStorage.MaxHeight,
                        cancellationToken);
                    if (thumbnail is not null)
                    {
                        var thumbnailPath = InternalMailThumbnailStorage.GetPath(storagePath, attachmentId);
                        await _fileStorage.SaveAtPathAsync(thumbnail, thumbnailPath, cancellationToken);
                        savedPaths.Add(thumbnailPath);
                    }
                }
            }

            await _dbContext.AttachmentModels.AddAsync(new AttachmentModel
            {
                AttachmentId = attachmentId,
                AttachmentCollectionId = collectionId,
                Slot = AttachmentSlot.InternalMail,
                FileName = Path.GetFileName(file.FileName),
                SizeBytes = file.Length,
                StoragePath = storagePath,
                CreateDate = now,
                CreateBy = _currentUser.EmployeeId,
                IsActive = true
            }, cancellationToken);
            await _dbContext.InternalMessageAttachments.AddAsync(new InternalMessageAttachment
            {
                InternalMessageAttachmentId = Guid.CreateVersion7(),
                InternalMessageId = messageId,
                AttachmentId = attachmentId,
                AttachedAt = now
            }, cancellationToken);
        }
    }

    private static TopicNotifications ResolveTopic(InternalMailRelatedType? relatedType)
    {
        return relatedType switch
        {
            InternalMailRelatedType.SampleRequest => TopicNotifications.SampleRequestMessageCreated,
            InternalMailRelatedType.Quotation => TopicNotifications.QuotationMessageCreated,
            _ => TopicNotifications.InternalMailMessageCreated
        };
    }
}
