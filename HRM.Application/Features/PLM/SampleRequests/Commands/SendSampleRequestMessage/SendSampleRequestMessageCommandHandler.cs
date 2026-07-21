using System.Text.Json;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;

/// <summary>
/// Tao hoac tai su dung thread InternalMail trong ngu canh SampleRequest,
/// sau do phat Notification de nguoi nhan duoc bao co tin moi.
/// </summary>
internal sealed class SendSampleRequestMessageCommandHandler
    : IRequestHandler<SendSampleRequestMessageCommand, OperationResult<SendInternalMessageResultDto>>
{
    private const int MaxMessageLength = 2000;

    private readonly IPLMWriteDbContext _dbContext;
    private readonly IInternalMailDbContext _internalMailDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationService _notificationService;

    public SendSampleRequestMessageCommandHandler(
        IPLMWriteDbContext dbContext,
        IInternalMailDbContext internalMailDbContext,
        ICurrentUser currentUser,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _internalMailDbContext = internalMailDbContext;
        _currentUser = currentUser;
        _notificationService = notificationService;
    }

    public async Task<OperationResult<SendInternalMessageResultDto>> Handle(
        SendSampleRequestMessageCommand request,
        CancellationToken cancellationToken)
    {
        if (request.SampleRequestId == Guid.Empty)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("SampleRequestId is invalid.");
        }

        if (request.ReminderAt.HasValue)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Reminder is not supported yet.");
        }

        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Message is required.");
        }

        if (message.Length > MaxMessageLength)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail($"Message cannot exceed {MaxMessageLength} characters.");
        }

        var currentEmployeeId = _currentUser.EmployeeId;
        if (!currentEmployeeId.HasValue || currentEmployeeId.Value == Guid.Empty)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Current employee is invalid.");
        }

        var companyId = _currentUser.CompanyId;
        if (!companyId.HasValue || companyId.Value == Guid.Empty)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Current company is invalid.");
        }

        var sampleRequest = await _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x =>
                x.SampleRequestId == request.SampleRequestId &&
                x.CompanyId == companyId.Value &&
                x.IsActive)
            .Select(x => new
            {
                x.SampleRequestId,
                x.ExternalId,
                x.CompanyId,
                x.ManagerBy
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sampleRequest is null)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Sample request was not found.");
        }

        var extraRecipientEmployeeIds = await ResolveExtraRecipientsAsync(
            request.ExtraRecipientEmployeeIds,
            sampleRequest.CompanyId,
            cancellationToken);

        if (!extraRecipientEmployeeIds.Success)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(extraRecipientEmployeeIds.Message ?? "Recipient is invalid.");
        }

        var conversation = await FindOrCreateConversationAsync(
            sampleRequest.SampleRequestId,
            sampleRequest.CompanyId,
            currentEmployeeId.Value,
            sampleRequest.ExternalId,
            cancellationToken);

        var targetUserIds = new HashSet<Guid>(extraRecipientEmployeeIds.Data ?? Array.Empty<Guid>());

        var roleRecipients = await ResolveRoleRecipientsAsync(
            ResolveDefaultRoles(request.Type),
            sampleRequest.CompanyId,
            cancellationToken);

        foreach (var employeeId in roleRecipients)
        {
            targetUserIds.Add(employeeId);
        }

        var existingParticipantIds = await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .Where(x => x.InternalConversationId == conversation.InternalConversationId)
            .Select(x => x.EmployeeId)
            .ToListAsync(cancellationToken);
        foreach (var employeeId in existingParticipantIds)
        {
            targetUserIds.Add(employeeId);
        }

        if (sampleRequest.ManagerBy != Guid.Empty)
        {
            targetUserIds.Add(sampleRequest.ManagerBy);
        }

        targetUserIds.Add(currentEmployeeId.Value);

        var replyToMessageId = request.ReplyToMessageId;
        if (replyToMessageId is { } replyId && replyId != Guid.Empty)
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

        await EnsureParticipantsAsync(
            conversation.InternalConversationId,
            currentEmployeeId.Value,
            targetUserIds,
            cancellationToken);

        var now = DateTime.Now;
        var messageType = request.Type == SampleRequestNotificationType.GeneralMessage
            ? InternalMessageType.Text
            : InternalMessageType.Action;

        var internalMessage = new InternalMessage
        {
            InternalMessageId = Guid.CreateVersion7(),
            InternalConversationId = conversation.InternalConversationId,
            SenderEmployeeId = currentEmployeeId.Value,
            MessageType = messageType,
            Body = message,
            ReplyToMessageId = replyToMessageId is { } parsedReplyId && parsedReplyId != Guid.Empty
                ? parsedReplyId
                : null,
            IsUrgent = request.IsUrgent,
            SentAt = now,
            IsEdited = false,
            IsDeleted = false
        };

        internalMessage.PayloadJson = JsonSerializer.Serialize(new SampleRequestThreadMessagePayload
        {
            ConversationId = conversation.InternalConversationId,
            MessageId = internalMessage.InternalMessageId,
            SampleRequestId = sampleRequest.SampleRequestId,
            ExternalId = sampleRequest.ExternalId,
            Type = request.Type.ToString(),
            SaleMessage = message,
            IsUrgent = request.IsUrgent,
            ReplyToMessageId = internalMessage.ReplyToMessageId
        });

        await _dbContext.InternalMessages.AddAsync(internalMessage, cancellationToken);

        conversation.LastMessageId = internalMessage.InternalMessageId;
        conversation.LastMessageAt = now;

        await AddReadStatesAsync(
            internalMessage.InternalMessageId,
            currentEmployeeId.Value,
            targetUserIds,
            now,
            cancellationToken);

        await UpdateParticipantStatesAsync(
            conversation.InternalConversationId,
            currentEmployeeId.Value,
            now,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var title = BuildTitle(request.Type);
        var sampleRequestLink = $"/plm/sample-requests/{sampleRequest.SampleRequestId}";

        var notificationPayload = JsonSerializer.Serialize(new SampleRequestThreadMessagePayload
        {
            ConversationId = conversation.InternalConversationId,
            MessageId = internalMessage.InternalMessageId,
            SampleRequestId = sampleRequest.SampleRequestId,
            ExternalId = sampleRequest.ExternalId,
            Type = request.Type.ToString(),
            SaleMessage = message,
            IsUrgent = request.IsUrgent,
            ReplyToMessageId = internalMessage.ReplyToMessageId
        });

        var createdByName = await _dbContext.Employees
            .AsNoTracking()
            .Where(x => x.EmployeeId == currentEmployeeId.Value)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        var notificationId = await _notificationService.PublishAsync(new PublishNotificationRequest
        {
            CompanyId = sampleRequest.CompanyId,
            CreatedBy = currentEmployeeId.Value,
            CreatedByNameSnapshot = createdByName ?? _currentUser.UserName,
            Topic = ResolveTopic(request.Type),
            Severity = request.IsUrgent ? NotificationSeverity.Warning : NotificationSeverity.Info,
            Title = title,
            Message = $"{sampleRequest.ExternalId}: {message}",
            Link = sampleRequestLink,
            PayloadJson = notificationPayload,
            TargetUserIds = await ResolveNotifiableParticipantsAsync(
                conversation.InternalConversationId,
                currentEmployeeId.Value,
                cancellationToken)
        }, cancellationToken);

        return OperationResult<SendInternalMessageResultDto>.Ok(new SendInternalMessageResultDto
        {
            ConversationId = conversation.InternalConversationId,
            MessageId = internalMessage.InternalMessageId,
            NotificationId = notificationId
        }, "Sent sample request message successfully.");
    }

    private async Task<OperationResult<IReadOnlyList<Guid>>> ResolveExtraRecipientsAsync(
        IReadOnlyList<Guid>? requestedEmployeeIds,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var employeeIds = requestedEmployeeIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();

        if (employeeIds.Length == 0)
        {
            return OperationResult<IReadOnlyList<Guid>>.Ok(Array.Empty<Guid>());
        }

        var validEmployeeIds = await _dbContext.Employees
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                employeeIds.Contains(x.EmployeeId))
            .Select(x => x.EmployeeId)
            .ToListAsync(cancellationToken);

        if (validEmployeeIds.Count != employeeIds.Length)
        {
            return OperationResult<IReadOnlyList<Guid>>.Fail("Some recipients do not exist or are inactive.");
        }

        return OperationResult<IReadOnlyList<Guid>>.Ok(validEmployeeIds);
    }

    private async Task<InternalConversation> FindOrCreateConversationAsync(
        Guid sampleRequestId,
        Guid companyId,
        Guid currentEmployeeId,
        string sampleRequestExternalId,
        CancellationToken cancellationToken)
    {
        var conversation = await _dbContext.InternalConversations
            .FirstOrDefaultAsync(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                x.RelatedType == InternalMailRelatedType.SampleRequest &&
                x.RelatedId == sampleRequestId,
                cancellationToken);

        if (conversation is not null)
        {
            return conversation;
        }

        conversation = new InternalConversation
        {
            InternalConversationId = Guid.CreateVersion7(),
            CompanyId = companyId,
            Subject = BuildConversationSubject(sampleRequestExternalId),
            RelatedType = InternalMailRelatedType.SampleRequest,
            RelatedId = sampleRequestId,
            RelatedExternalId = sampleRequestExternalId,
            CreatedBy = currentEmployeeId,
            CreatedAt = DateTime.Now,
            LastMessageAt = DateTime.Now,
            IsActive = true
        };

        await _dbContext.InternalConversations.AddAsync(conversation, cancellationToken);

        // Conversation phai ton tai truoc khi gan LastMessageId de tranh chu trinh hai FK luc insert lan dau.
        await _dbContext.SaveChangesAsync(cancellationToken);

        return conversation;
    }

    private async Task EnsureParticipantsAsync(
        Guid conversationId,
        Guid senderEmployeeId,
        IReadOnlyCollection<Guid> participantIds,
        CancellationToken cancellationToken)
    {
        var existingParticipantIds = await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .Where(x => x.InternalConversationId == conversationId)
            .Select(x => x.EmployeeId)
            .ToListAsync(cancellationToken);

        var missingParticipantIds = participantIds
            .Where(x => x != Guid.Empty && !existingParticipantIds.Contains(x))
            .Distinct()
            .ToList();

        foreach (var employeeId in missingParticipantIds)
        {
            await _dbContext.InternalConversationParticipants.AddAsync(new InternalConversationParticipant
            {
                InternalConversationId = conversationId,
                EmployeeId = employeeId,
                Role = employeeId == senderEmployeeId
                    ? InternalConversationParticipantRole.Owner
                    : InternalConversationParticipantRole.Member,
                JoinedAt = DateTime.Now,
                IsArchived = false,
                IsMuted = false
            }, cancellationToken);
        }
    }

    private async Task AddReadStatesAsync(
        Guid messageId,
        Guid senderEmployeeId,
        IReadOnlyCollection<Guid> participantIds,
        DateTime sentAt,
        CancellationToken cancellationToken)
    {
        foreach (var employeeId in participantIds.Where(x => x != Guid.Empty).Distinct())
        {
            await _dbContext.InternalMessageReadStates.AddAsync(new InternalMessageReadState
            {
                InternalMessageId = messageId,
                EmployeeId = employeeId,
                IsRead = employeeId == senderEmployeeId,
                ReadAt = employeeId == senderEmployeeId ? sentAt : null
            }, cancellationToken);
        }
    }

    private async Task UpdateParticipantStatesAsync(
        Guid conversationId,
        Guid senderEmployeeId,
        DateTime sentAt,
        CancellationToken cancellationToken)
    {
        await _dbContext.InternalConversationParticipants
            .Where(x =>
                x.InternalConversationId == conversationId &&
                x.EmployeeId == senderEmployeeId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.LastReadAt, sentAt)
                .SetProperty(x => x.IsArchived, false)
                .SetProperty(x => x.ArchivedAt, (DateTime?)null),
                cancellationToken);

        await _dbContext.InternalConversationParticipants
            .Where(x =>
                x.InternalConversationId == conversationId &&
                x.EmployeeId != senderEmployeeId &&
                x.IsArchived)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsArchived, false)
                .SetProperty(x => x.ArchivedAt, (DateTime?)null),
                cancellationToken);
    }

    private async Task<IReadOnlyCollection<Guid>> ResolveRoleRecipientsAsync(
        IReadOnlyList<string> roles,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (roles.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        return await (
                from role in _internalMailDbContext.Roles.AsNoTracking()
                join userRole in _internalMailDbContext.UserRoles.AsNoTracking()
                    on role.Id equals userRole.RoleId
                join user in _internalMailDbContext.Users.AsNoTracking()
                    on userRole.UserId equals user.Id
                join employee in _internalMailDbContext.Employees.AsNoTracking()
                    on user.EmployeeId equals employee.EmployeeId
                where role.NormalizedName != null &&
                      roles.Contains(role.NormalizedName) &&
                      userRole.IsActive &&
                      user.EmployeeId.HasValue &&
                      employee.CompanyId == companyId &&
                      employee.IsActive
                select employee.EmployeeId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static string BuildConversationSubject(string sampleRequestExternalId)
    {
        return $"Trao doi yeu cau phoi mau {sampleRequestExternalId}";
    }

    private static string BuildTitle(SampleRequestNotificationType type)
    {
        return type switch
        {
            SampleRequestNotificationType.PriceQuoteRequest => "Yeu cau bao gia mau",
            SampleRequestNotificationType.ChangeRequest => "Yeu cau thay doi mau",
            SampleRequestNotificationType.UpdateRequest => "Yeu cau cap nhat mau",
            SampleRequestNotificationType.GeneralMessage => "Tin nhan ve yeu cau phoi mau",
            _ => "Tin nhan ve yeu cau phoi mau"
        };
    }

    private async Task<IReadOnlyCollection<Guid>> ResolveNotifiableParticipantsAsync(
        Guid conversationId,
        Guid senderEmployeeId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .Where(x =>
                x.InternalConversationId == conversationId &&
                x.EmployeeId != senderEmployeeId &&
                !x.IsMuted)
            .Select(x => x.EmployeeId)
            .ToListAsync(cancellationToken);
    }

    private static TopicNotifications ResolveTopic(SampleRequestNotificationType type)
    {
        return type switch
        {
            SampleRequestNotificationType.PriceQuoteRequest => TopicNotifications.SampleRequestPriceQuoteRequested,
            SampleRequestNotificationType.ChangeRequest => TopicNotifications.SampleRequestChangeRequested,
            SampleRequestNotificationType.UpdateRequest => TopicNotifications.SampleRequestUpdateRequested,
            _ => TopicNotifications.SampleRequestMessageCreated
        };
    }

    private static IReadOnlyList<string> ResolveDefaultRoles(SampleRequestNotificationType type)
    {
        return type switch
        {
            SampleRequestNotificationType.PriceQuoteRequest => new[] { ApplicationRoles.Lab.LabUser },
            SampleRequestNotificationType.ChangeRequest => new[] { ApplicationRoles.Lab.LabUser },
            SampleRequestNotificationType.UpdateRequest => new[] { ApplicationRoles.Lab.LabUser },
            SampleRequestNotificationType.GeneralMessage => new[] { ApplicationRoles.Lab.LabUser },
            _ => new[] { ApplicationRoles.Lab.LabUser }
        };
    }
}
