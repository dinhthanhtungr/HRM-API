using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Commands.SendMessage;

internal sealed class SendInternalMessageCommandHandler
    : IRequestHandler<SendInternalMessageCommand, OperationResult<SendInternalMessageResultDto>>
{
    private const int MaxBodyLength = 2000;
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly INotificationService _notificationService;

    public SendInternalMessageCommandHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _notificationService = notificationService;
    }

    public async Task<OperationResult<SendInternalMessageResultDto>> Handle(
        SendInternalMessageCommand request,
        CancellationToken cancellationToken)
    {
        var body = request.Body?.Trim();
        if (request.ConversationId == Guid.Empty || string.IsNullOrWhiteSpace(body) || body.Length > MaxBodyLength)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail($"Body is required and cannot exceed {MaxBodyLength} characters.");
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
                x.Participants.Any(participant => participant.EmployeeId == senderId.Value),
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
            .Where(x => x.InternalConversationId == conversation.InternalConversationId)
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
            IsUrgent = request.IsUrgent
        };

        await _dbContext.InternalMessages.AddAsync(new InternalMessage
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
        }, cancellationToken);

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
                x.EmployeeId == senderId.Value,
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
            Message = body,
            Link = $"/internal-mail/conversations/{conversation.InternalConversationId}",
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

    private static TopicNotifications ResolveTopic(InternalMailRelatedType? relatedType)
    {
        return relatedType == InternalMailRelatedType.SampleRequest
            ? TopicNotifications.SampleRequestMessageCreated
            : TopicNotifications.InternalMailMessageCreated;
    }
}
