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

namespace HRM.Application.Features.InternalMail.Commands.CreateConversation;

internal sealed class CreateInternalConversationCommandHandler
    : IRequestHandler<CreateInternalConversationCommand, OperationResult<SendInternalMessageResultDto>>
{
    private const int MaxSubjectLength = 250;
    private const int MaxBodyLength = 2000;

    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly INotificationService _notificationService;

    public CreateInternalConversationCommandHandler(
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
        CreateInternalConversationCommand request,
        CancellationToken cancellationToken)
    {
        var subject = request.Subject?.Trim();
        var body = request.Body?.Trim();
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > MaxSubjectLength)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail($"Subject is required and cannot exceed {MaxSubjectLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(body) || body.Length > MaxBodyLength)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail($"Body is required and cannot exceed {MaxBodyLength} characters.");
        }

        var companyId = _currentUser.CompanyId;
        var senderId = _currentUser.EmployeeId;
        if (!companyId.HasValue || !senderId.HasValue)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Current employee or company is invalid.");
        }

        var requestedRecipientIds = request.RecipientEmployeeIds
            .Where(x => x != Guid.Empty && x != senderId.Value)
            .Distinct()
            .ToArray();
        if (requestedRecipientIds.Length == 0)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("At least one recipient is required.");
        }

        var recipients = await _dbContext.Employees
            .AsNoTracking()
            .Where(x =>
                requestedRecipientIds.Contains(x.EmployeeId) &&
                x.CompanyId == companyId.Value &&
                x.IsActive)
            .Select(x => x.EmployeeId)
            .ToListAsync(cancellationToken);

        if (recipients.Count != requestedRecipientIds.Length)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Some recipients do not exist or are inactive.");
        }

        var now = _dateTimeProvider.Now;
        var conversationId = Guid.CreateVersion7();
        var messageId = Guid.CreateVersion7();
        var conversation = new InternalConversation
        {
            InternalConversationId = conversationId,
            CompanyId = companyId.Value,
            Subject = subject,
            RelatedType = InternalMailRelatedType.Internal,
            CreatedBy = senderId.Value,
            CreatedAt = now,
            LastMessageAt = now,
            IsActive = true
        };

        await _dbContext.InternalConversations.AddAsync(conversation, cancellationToken);
        await AddParticipantAsync(conversationId, senderId.Value, InternalConversationParticipantRole.Owner, now, cancellationToken);
        foreach (var recipientId in recipients)
        {
            await AddParticipantAsync(conversationId, recipientId, InternalConversationParticipantRole.Member, now, cancellationToken);
        }

        // Luu conversation truoc de tranh chu trinh FK Conversation.LastMessageId <-> InternalMessage.ConversationId.
        await _dbContext.SaveChangesAsync(cancellationToken);

        var payload = new InternalMailNotificationPayload
        {
            ConversationId = conversationId,
            MessageId = messageId,
            RelatedType = InternalMailRelatedType.Internal.ToString(),
            IsUrgent = request.IsUrgent
        };

        await _dbContext.InternalMessages.AddAsync(new InternalMessage
        {
            InternalMessageId = messageId,
            InternalConversationId = conversationId,
            SenderEmployeeId = senderId.Value,
            MessageType = InternalMessageType.Text,
            Body = body,
            PayloadJson = JsonSerializer.Serialize(payload),
            IsUrgent = request.IsUrgent,
            SentAt = now
        }, cancellationToken);

        foreach (var participantId in recipients.Append(senderId.Value))
        {
            await _dbContext.InternalMessageReadStates.AddAsync(new InternalMessageReadState
            {
                InternalMessageId = messageId,
                EmployeeId = participantId,
                IsRead = participantId == senderId.Value,
                ReadAt = participantId == senderId.Value ? now : null
            }, cancellationToken);
        }

        conversation.LastMessageId = messageId;
        var senderName = await _dbContext.Employees
            .AsNoTracking()
            .Where(x => x.EmployeeId == senderId.Value)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var notificationId = await _notificationService.PublishAsync(new PublishNotificationRequest
        {
            CompanyId = companyId.Value,
            CreatedBy = senderId.Value,
            CreatedByNameSnapshot = senderName ?? _currentUser.UserName,
            Topic = TopicNotifications.InternalMailMessageCreated,
            Severity = request.IsUrgent ? NotificationSeverity.Warning : NotificationSeverity.Info,
            Title = subject,
            Message = body,
            Link = $"/internal-mail/conversations/{conversationId}",
            PayloadJson = JsonSerializer.Serialize(payload),
            TargetUserIds = recipients
        }, cancellationToken);

        return OperationResult<SendInternalMessageResultDto>.Ok(new SendInternalMessageResultDto
        {
            ConversationId = conversationId,
            MessageId = messageId,
            NotificationId = notificationId
        });
    }

    private async Task AddParticipantAsync(
        Guid conversationId,
        Guid employeeId,
        InternalConversationParticipantRole role,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await _dbContext.InternalConversationParticipants.AddAsync(new InternalConversationParticipant
        {
            InternalConversationId = conversationId,
            EmployeeId = employeeId,
            Role = role,
            JoinedAt = now,
            LastReadAt = role == InternalConversationParticipantRole.Owner ? now : null
        }, cancellationToken);
    }
}
