using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;
using HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;
using HRM.Application.Features.PLM.SampleRequests.DirectPatchNotifications;
using HRM.Application.Features.PLM.SampleRequests.FormulaChangeRequests;
using HRM.Application.Features.PLM.SampleRequests.PriceQuoteRequests;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services;
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

    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationService _notificationService;
    private readonly SampleRequestRecipientResolver _sampleRequestRecipientResolver;

    public SendSampleRequestMessageCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        INotificationService notificationService,
        SampleRequestRecipientResolver sampleRequestRecipientResolver)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _notificationService = notificationService;
        _sampleRequestRecipientResolver = sampleRequestRecipientResolver;
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
                x.ManagerBy,
                x.RequestType,
                CustomerExternalId = x.Customer.ExternalId,
                ColourCode = x.Product.ColourCode,
                CategoryExternalId = x.Product.Category != null ? x.Product.Category.ExternalId : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sampleRequest is null)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Sample request was not found.");
        }

        if (SampleRequestMessageRules.ShouldSuppressMessages(sampleRequest.RequestType, sampleRequest.CustomerExternalId))
        {
            return OperationResult<SendInternalMessageResultDto>.Ok(new SendInternalMessageResultDto
            {
                ConversationId = Guid.Empty,
                MessageId = Guid.Empty,
                NotificationId = Guid.Empty
            }, "Sample request does not require an internal message or notification.");
        }

        var extraRecipientEmployeeIds = await ResolveExtraRecipientsAsync(
            request.ExtraRecipientEmployeeIds,
            sampleRequest.CompanyId,
            cancellationToken);

        if (!extraRecipientEmployeeIds.Success)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(extraRecipientEmployeeIds.Message ?? "Recipient is invalid.");
        }

        var useDefaultSilentWatchers = request.UseDefaultSilentWatchersWhenOmitted &&
            request.SilentWatcherEmployeeIds is null;
        IReadOnlyCollection<Guid> requestedSilentWatcherIds;
        if (useDefaultSilentWatchers)
        {
            requestedSilentWatcherIds = (await _sampleRequestRecipientResolver.ResolveDefaultSilentWatchersAsync(
                    sampleRequest.CompanyId,
                    currentEmployeeId.Value,
                    cancellationToken))
                .Select(x => x.EmployeeId)
                .ToArray();
        }
        else
        {
            var silentWatcherEmployeeIds = await ResolveExtraRecipientsAsync(
                request.SilentWatcherEmployeeIds,
                sampleRequest.CompanyId,
                cancellationToken);
            if (!silentWatcherEmployeeIds.Success)
            {
                return OperationResult<SendInternalMessageResultDto>.Fail(
                    silentWatcherEmployeeIds.Message ?? "Silent watcher is invalid.");
            }

            requestedSilentWatcherIds = silentWatcherEmployeeIds.Data ?? Array.Empty<Guid>();
        }

        if (requestedSilentWatcherIds.Contains(currentEmployeeId.Value))
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(
                "The current employee cannot be a silent watcher.");
        }

        var conversation = await FindOrCreateConversationAsync(
            sampleRequest.SampleRequestId,
            sampleRequest.CompanyId,
            currentEmployeeId.Value,
            sampleRequest.ExternalId,
            sampleRequest.ColourCode,
            cancellationToken);

        var targetUserIds = new HashSet<Guid>(extraRecipientEmployeeIds.Data ?? Array.Empty<Guid>());

        var defaultRecipients = await _sampleRequestRecipientResolver.ResolveDefaultMessageRecipientsAsync(
            sampleRequest.CompanyId,
            sampleRequest.CategoryExternalId,
            cancellationToken);

        foreach (var recipient in defaultRecipients.Where(x => x.Locked))
        {
            targetUserIds.Add(recipient.EmployeeId);
        }

        var salesGroupLeaderRecipients = await _sampleRequestRecipientResolver.ResolveSalesGroupLeaderRecipientsAsync(
            sampleRequest.CompanyId,
            currentEmployeeId.Value,
            cancellationToken);
        foreach (var recipient in salesGroupLeaderRecipients)
        {
            targetUserIds.Add(recipient.EmployeeId);
        }

        var salesGroupAdminRecipients = await _sampleRequestRecipientResolver.ResolveSalesGroupAdminRecipientsAsync(
            sampleRequest.CompanyId,
            currentEmployeeId.Value,
            cancellationToken);
        foreach (var recipient in salesGroupAdminRecipients)
        {
            targetUserIds.Add(recipient.EmployeeId);
        }

        var existingParticipantIds = await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .Where(x => x.InternalConversationId == conversation.InternalConversationId && x.IsActive)
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

        var silentWatcherIds = requestedSilentWatcherIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();
        if (useDefaultSilentWatchers)
        {
            // LabAdmin hoặc người đã là recipient thường phải giữ vai trò recipient thường, không thành watcher.
            silentWatcherIds = silentWatcherIds
                .Where(x => !targetUserIds.Contains(x))
                .ToArray();
        }
        else if (silentWatcherIds.Any(targetUserIds.Contains))
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(
                "A silent watcher cannot also be a message recipient.");
        }

        var participantIds = targetUserIds
            .Concat(silentWatcherIds)
            .Distinct()
            .ToArray();

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
            silentWatcherIds,
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
            ContentType = ResolveContentType(request),
            ConversationId = conversation.InternalConversationId,
            MessageId = internalMessage.InternalMessageId,
            SampleRequestId = sampleRequest.SampleRequestId,
            ExternalId = sampleRequest.ExternalId,
            Type = request.Type.ToString(),
            SaleMessage = message,
            IsUrgent = request.IsUrgent,
            ReplyToMessageId = internalMessage.ReplyToMessageId,
            DataChangeRequest = request.DataChangeRequest,
            FormulaChangeRequest = request.FormulaChangeRequest,
            DirectPatchNotification = request.DirectPatchNotification,
            SampleReceiptAction = request.SampleReceiptAction,
            PriceQuoteRequest = request.PriceQuoteRequest
        }, PayloadJsonOptions);

        await _dbContext.InternalMessages.AddAsync(internalMessage, cancellationToken);

        conversation.LastMessageId = internalMessage.InternalMessageId;
        conversation.LastMessageAt = now;

        await AddReadStatesAsync(
            internalMessage.InternalMessageId,
            currentEmployeeId.Value,
            participantIds,
            now,
            cancellationToken);

        await UpdateParticipantStatesAsync(
            conversation.InternalConversationId,
            currentEmployeeId.Value,
            now,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var title = request.TitleOverride ?? BuildTitle(request.Type);
        var sampleRequestLink = request.LinkOverride ?? $"/plm/sample-requests/{sampleRequest.SampleRequestId}";

        var notificationPayload = JsonSerializer.Serialize(new SampleRequestThreadMessagePayload
        {
            ContentType = ResolveContentType(request),
            ConversationId = conversation.InternalConversationId,
            MessageId = internalMessage.InternalMessageId,
            SampleRequestId = sampleRequest.SampleRequestId,
            ExternalId = sampleRequest.ExternalId,
            Type = request.Type.ToString(),
            SaleMessage = message,
            IsUrgent = request.IsUrgent,
            ReplyToMessageId = internalMessage.ReplyToMessageId,
            DataChangeRequest = request.DataChangeRequest,
            FormulaChangeRequest = request.FormulaChangeRequest,
            DirectPatchNotification = request.DirectPatchNotification,
            SampleReceiptAction = request.SampleReceiptAction,
            PriceQuoteRequest = request.PriceQuoteRequest
        }, PayloadJsonOptions);

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
            Topic = request.TopicOverride ?? ResolveTopic(request.Type),
            Severity = request.IsUrgent ? NotificationSeverity.Warning : NotificationSeverity.Info,
            Title = title,
            Message = $"{sampleRequest.ExternalId}: {message}",
            Link = sampleRequestLink,
            AggregateId = sampleRequest.SampleRequestId,
            AggregateCode = sampleRequest.ExternalId,
            ConversationId = conversation.InternalConversationId,
            MessageId = internalMessage.InternalMessageId,
            PayloadJson = notificationPayload,
            TargetUserIds = request.NotificationRecipientEmployeeIdsOverride is null
                ? (await ResolveNotifiableParticipantsAsync(
                        conversation.InternalConversationId,
                        currentEmployeeId.Value,
                        cancellationToken))
                    // Participant mới chỉ đang được EF theo dõi trước SaveChanges, nên query database
                    // phía trên chưa thấy ở notification đầu tiên. Dùng thêm targetUserIds đã resolve
                    // trong command để cả normal recipient lẫn silent watcher có state Hub ngay lập tức.
                    .Concat(targetUserIds)
                    .Where(x => x != Guid.Empty && x != currentEmployeeId.Value && !silentWatcherIds.Contains(x))
                    .Distinct()
                    .ToArray()
                : request.NotificationRecipientEmployeeIdsOverride
                    .Where(x => x != Guid.Empty && x != currentEmployeeId.Value)
                    .Distinct()
                    .ToArray(),
            // Ở lần tạo Sample Request đầu tiên, silent watcher vừa được Add vào DbContext nên chưa
            // query được từ database trước SaveChanges. Union với danh sách request để state Hub được
            // tạo ngay cho tin đầu tiên; các tin tiếp theo vẫn resolve muted participant từ database.
            SilentUserIds = (await ResolveSilentParticipantsAsync(
                    conversation.InternalConversationId,
                    currentEmployeeId.Value,
                    cancellationToken))
                .Concat(silentWatcherIds)
                .Where(x => x != Guid.Empty && x != currentEmployeeId.Value)
                .Distinct()
                .ToArray()
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
        string? colourCode,
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
            Subject = SampleRequestConversationSubjectService.BuildSubject(sampleRequestExternalId, colourCode),
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
        IReadOnlyCollection<Guid> memberParticipantIds,
        IReadOnlyCollection<Guid> silentWatcherIds,
        CancellationToken cancellationToken)
    {
        var memberIds = memberParticipantIds
            .Where(x => x != Guid.Empty)
            .ToHashSet();
        var silentWatcherIdSet = silentWatcherIds
            .Where(x => x != Guid.Empty && !memberIds.Contains(x))
            .ToHashSet();
        var participantIds = memberIds
            .Concat(silentWatcherIdSet)
            .ToHashSet();

        var existingParticipants = await _dbContext.InternalConversationParticipants
            .Where(x => x.InternalConversationId == conversationId)
            .ToListAsync(cancellationToken);

        foreach (var participant in existingParticipants.Where(x =>
                     !x.IsActive && participantIds.Contains(x.EmployeeId)))
        {
            participant.IsActive = true;
            participant.DeletedAt = null;
            participant.DeletedByEmployeeId = null;
            participant.IsArchived = false;
            participant.ArchivedAt = null;
            participant.Role = participant.EmployeeId == senderEmployeeId
                ? InternalConversationParticipantRole.Owner
                : silentWatcherIdSet.Contains(participant.EmployeeId)
                    ? InternalConversationParticipantRole.Watcher
                    : InternalConversationParticipantRole.Member;
            participant.IsMuted = silentWatcherIdSet.Contains(participant.EmployeeId);
        }

        var missingParticipantIds = participantIds
            .Where(x => x != Guid.Empty && !existingParticipants.Any(participant => participant.EmployeeId == x))
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
                    : silentWatcherIdSet.Contains(employeeId)
                        ? InternalConversationParticipantRole.Watcher
                        : InternalConversationParticipantRole.Member,
                JoinedAt = DateTime.Now,
                IsArchived = false,
                IsMuted = silentWatcherIdSet.Contains(employeeId)
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
                x.EmployeeId == senderEmployeeId &&
                x.IsActive)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.LastReadAt, sentAt)
                .SetProperty(x => x.IsArchived, false)
                .SetProperty(x => x.ArchivedAt, (DateTime?)null),
                cancellationToken);

        await _dbContext.InternalConversationParticipants
            .Where(x =>
                x.InternalConversationId == conversationId &&
                x.EmployeeId != senderEmployeeId &&
                x.IsActive &&
                x.IsArchived)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsArchived, false)
                .SetProperty(x => x.ArchivedAt, (DateTime?)null),
                cancellationToken);
    }

    private static string BuildTitle(SampleRequestNotificationType type)
    {
        return type switch
        {
            SampleRequestNotificationType.PriceQuoteRequest => "Yêu cầu báo giá",
            SampleRequestNotificationType.ChangeRequest => "Yêu cầu thay đổi",
            SampleRequestNotificationType.UpdateRequest => "Yeu cau cap nhat mau",
            SampleRequestNotificationType.GeneralMessage => "Tin nhan ve yeu cau phoi mau",
            _ => "Tin nhan ve yeu cau phoi mau"
        };
    }

    private static string ResolveContentType(SendSampleRequestMessageCommand request)
    {
        if (request.DataChangeRequest is not null)
        {
            return SampleRequestDataChangePayloadTypes.Request;
        }

        if (request.FormulaChangeRequest is not null)
        {
            return SampleRequestFormulaChangePayloadTypes.Request;
        }

        if (request.DirectPatchNotification is not null)
        {
            return SampleRequestDirectPatchNotificationPayloadTypes.Notification;
        }

        if (request.PriceQuoteRequest is not null)
        {
            return SampleRequestPriceQuotePayloadTypes.Request;
        }

        return "InternalMailMessage";
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
                x.IsActive &&
                !x.IsMuted)
            .Select(x => x.EmployeeId)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyCollection<Guid>> ResolveSilentParticipantsAsync(
        Guid conversationId,
        Guid senderEmployeeId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.InternalConversationParticipants
            .AsNoTracking()
            .Where(x =>
                x.InternalConversationId == conversationId &&
                x.EmployeeId != senderEmployeeId &&
                x.IsActive &&
                x.IsMuted)
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
}
