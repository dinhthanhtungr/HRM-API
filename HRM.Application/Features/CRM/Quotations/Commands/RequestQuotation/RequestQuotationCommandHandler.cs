using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.RequestQuotation;

internal sealed class RequestQuotationCommandHandler
    : IRequestHandler<RequestQuotationCommand, OperationResult<RequestQuotationResultDto>>
{
    private const int MaxMessageLength = 2000;
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] GlobalManagementRoles =
    [
        ApplicationRoles.President.ToUpperInvariant(),
        ApplicationRoles.Developer.ToUpperInvariant(),
    ];

    private readonly ICRMReadDbContext _readDbContext;
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly IInternalMailDbContext _internalMailDbContext;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly ISaleGroupRecipientResolver _saleGroupRecipientResolver;
    private readonly INotificationService _notificationService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly KeyedMutationLock<Guid> _mutationLock;
    private readonly QuotationPricingApprovalStateService _pricingApprovalStateService;

    public RequestQuotationCommandHandler(
        ICRMReadDbContext readDbContext,
        ICRMWriteDbContext writeDbContext,
        IInternalMailDbContext internalMailDbContext,
        ICustomerVisibilityService visibilityService,
        ISaleGroupRecipientResolver saleGroupRecipientResolver,
        INotificationService notificationService,
        IDateTimeProvider dateTimeProvider,
        KeyedMutationLock<Guid> mutationLock,
        QuotationPricingApprovalStateService pricingApprovalStateService)
    {
        _readDbContext = readDbContext;
        _writeDbContext = writeDbContext;
        _internalMailDbContext = internalMailDbContext;
        _visibilityService = visibilityService;
        _saleGroupRecipientResolver = saleGroupRecipientResolver;
        _notificationService = notificationService;
        _dateTimeProvider = dateTimeProvider;
        _mutationLock = mutationLock;
        _pricingApprovalStateService = pricingApprovalStateService;
    }

    public async Task<OperationResult<RequestQuotationResultDto>> Handle(
        RequestQuotationCommand command,
        CancellationToken cancellationToken)
    {
        var requestedMessage = QuotationRules.TrimToNull(command.Request.Message);
        if (command.QuotationId == Guid.Empty ||
            requestedMessage is null ||
            requestedMessage.Length > MaxMessageLength)
        {
            return OperationResult<RequestQuotationResultDto>.Fail(
                $"Message is required and cannot exceed {MaxMessageLength} characters.");
        }

        using var mutationLease = await _mutationLock.AcquireAsync(
            command.QuotationId,
            cancellationToken);

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var quotation = await _visibilityService
            .ApplyQuotationVisibility(
                _readDbContext.Quotations.AsNoTracking(),
                _readDbContext.Customers.AsNoTracking(),
                scope)
            .Where(x =>
                x.QuotationId == command.QuotationId &&
                x.CompanyId == scope.CompanyId &&
                x.IsActive)
            .Select(x => new QuotationRequestProjection(
                x.QuotationId,
                x.ExternalId,
                x.CompanyId,
                x.CustomerId,
                x.Customer.CustomerName,
                x.SaleEmployeeId,
                x.Status,
                x.Lines.Count(line => line.IsActive)))
            .FirstOrDefaultAsync(cancellationToken);

        if (quotation is null)
        {
            return OperationResult<RequestQuotationResultDto>.Fail(
                "Quotation was not found or is outside your visibility scope.");
        }

        if (!QuotationWorkflowRules.CanRequestPricing(quotation.Status))
        {
            return OperationResult<RequestQuotationResultDto>.Fail(
                "Only a draft quotation can request pricing.");
        }

        if (quotation.SaleEmployeeId != scope.EmployeeId)
        {
            return OperationResult<RequestQuotationResultDto>.Fail(
                "Only the assigned sale employee can request quotation pricing.");
        }

        if (quotation.ActiveLineCount == 0)
        {
            return OperationResult<RequestQuotationResultDto>.Fail(
                "A quotation must have at least one active line before pricing can be requested.");
        }

        var recipientEmployeeIds = await ResolveManagementRecipientsAsync(
            quotation.CompanyId,
            quotation.SaleEmployeeId,
            scope.EmployeeId,
            cancellationToken);
        if (recipientEmployeeIds.Count == 0)
        {
            return OperationResult<RequestQuotationResultDto>.Fail(
                "No active sale group leader, President or Developer employee was found in the current company.");
        }

        var now = _dateTimeProvider.Now;
        var trackedQuotation = await _writeDbContext.Quotations
            .AsTracking()
            .FirstAsync(x =>
                x.QuotationId == quotation.QuotationId &&
                x.CompanyId == quotation.CompanyId &&
                x.IsActive,
                cancellationToken);
        trackedQuotation.Status = QuotationStatus.PendingApproval;
        trackedQuotation.UpdatedBy = scope.EmployeeId;
        trackedQuotation.UpdatedDate = now;
        _writeDbContext.QuotationStatusHistories.Add(new QuotationStatusHistory
        {
            Id = Guid.CreateVersion7(),
            QuotationId = quotation.QuotationId,
            FromStatus = QuotationStatus.Draft,
            ToStatus = QuotationStatus.PendingApproval,
            Note = requestedMessage,
            ChangedBy = scope.EmployeeId,
            ChangedDate = now
        });
        await _writeDbContext.SaveChangesAsync(cancellationToken);

        var approvalState = await _pricingApprovalStateService.ReconcileLockedAsync(
            quotation.QuotationId,
            quotation.CompanyId,
            scope.EmployeeId,
            cancellationToken);

        var actorName = await _internalMailDbContext.Employees
            .AsNoTracking()
            .Where(x => x.EmployeeId == scope.EmployeeId)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(cancellationToken);
        actorName = QuotationRules.TrimToNull(actorName) ?? "Nhân viên kinh doanh";

        var productCodes = await _readDbContext.QuotationLines
            .AsNoTracking()
            .Where(x => x.QuotationId == quotation.QuotationId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.QuotationLineId)
            .Select(x => x.ProductExternalIdSnapshot)
            .ToListAsync(cancellationToken);

        var conversation = await FindOrCreateConversationAsync(
            quotation,
            productCodes,
            scope.EmployeeId,
            now,
            cancellationToken);

        var currentParticipantIds = recipientEmployeeIds
            .Append(scope.EmployeeId)
            .Distinct()
            .ToArray();
        await EnsureParticipantsAsync(
            conversation.InternalConversationId,
            scope.EmployeeId,
            currentParticipantIds,
            now,
            cancellationToken);

        var persistedParticipantIds = await _internalMailDbContext.InternalConversationParticipants
            .AsNoTracking()
            .Where(x => x.InternalConversationId == conversation.InternalConversationId && x.IsActive)
            .Select(x => x.EmployeeId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var allParticipantIds = persistedParticipantIds
            .Concat(currentParticipantIds)
            .Distinct()
            .ToArray();

        var message = new InternalMessage
        {
            InternalMessageId = Guid.CreateVersion7(),
            InternalConversationId = conversation.InternalConversationId,
            SenderEmployeeId = scope.EmployeeId,
            MessageType = InternalMessageType.Action,
            Body = requestedMessage,
            IsUrgent = command.Request.IsUrgent,
            SentAt = now,
            IsEdited = false,
            IsDeleted = false
        };

        var payload = new QuotationRequestedMessagePayload
        {
            RelatedId = quotation.QuotationId,
            RelatedExternalId = quotation.ExternalId,
            ConversationId = conversation.InternalConversationId,
            MessageId = message.InternalMessageId,
            IsUrgent = command.Request.IsUrgent,
            Action = new QuotationRequestedMessageActionDto
            {
                Parameters = new QuotationRequestedMessageActionParametersDto
                {
                    QuotationId = quotation.QuotationId,
                    QuotationExternalId = quotation.ExternalId
                }
            }
        };
        message.PayloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions);

        await _internalMailDbContext.InternalMessages.AddAsync(message, cancellationToken);
        await _internalMailDbContext.InternalMessageReferences.AddAsync(
            new InternalMessageReference
            {
                InternalMessageReferenceId = Guid.CreateVersion7(),
                InternalMessageId = message.InternalMessageId,
                RelatedType = InternalMailRelatedType.Quotation,
                RelatedId = quotation.QuotationId,
                RelatedExternalId = quotation.ExternalId,
                RelatedNameSnapshot = conversation.Subject,
                IsPrimary = true
            },
            cancellationToken);

        conversation.LastMessageId = message.InternalMessageId;
        conversation.LastMessageAt = now;

        foreach (var employeeId in allParticipantIds)
        {
            await _internalMailDbContext.InternalMessageReadStates.AddAsync(
                new InternalMessageReadState
                {
                    InternalMessageId = message.InternalMessageId,
                    EmployeeId = employeeId,
                    IsRead = employeeId == scope.EmployeeId,
                    ReadAt = employeeId == scope.EmployeeId ? now : null
                },
                cancellationToken);
        }

        await _internalMailDbContext.SaveChangesAsync(cancellationToken);

        var notificationId = await _notificationService.PublishAsync(
            new PublishNotificationRequest
            {
                CompanyId = quotation.CompanyId,
                CreatedBy = scope.EmployeeId,
                CreatedByNameSnapshot = actorName,
                Topic = TopicNotifications.QuotationRequested,
                Severity = command.Request.IsUrgent
                    ? NotificationSeverity.Warning
                    : NotificationSeverity.Info,
                Title = $"Yêu cầu báo giá {quotation.ExternalId}",
                Message =
                    $"{actorName} yêu cầu báo giá cho {quotation.CustomerName}: " +
                    requestedMessage,
                Link = null,
                AggregateId = quotation.QuotationId,
                AggregateCode = quotation.ExternalId,
                ConversationId = conversation.InternalConversationId,
                MessageId = message.InternalMessageId,
                PayloadJson = message.PayloadJson,
                TargetUserIds = recipientEmployeeIds
            },
            cancellationToken);

        return OperationResult<RequestQuotationResultDto>.Ok(
            new RequestQuotationResultDto
            {
                QuotationId = quotation.QuotationId,
                ConversationId = conversation.InternalConversationId,
                MessageId = message.InternalMessageId,
                NotificationId = notificationId,
                RequestedAt = now,
                QuotationStatus = approvalState.Status,
                UpdatedDate = approvalState.UpdatedDate ?? trackedQuotation.UpdatedDate
            },
            "Quotation request sent successfully.");
    }

    private async Task<InternalConversation> FindOrCreateConversationAsync(
        QuotationRequestProjection quotation,
        IReadOnlyCollection<string> productCodes,
        Guid currentEmployeeId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var subject = QuotationConversationSubjectService.BuildSubject(
            quotation.ExternalId,
            productCodes);
        var conversation = await _internalMailDbContext.InternalConversations
            .AsTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.CompanyId == quotation.CompanyId &&
                    x.IsActive &&
                    x.RelatedType == InternalMailRelatedType.Quotation &&
                    x.RelatedId == quotation.QuotationId,
                cancellationToken);
        if (conversation is not null)
        {
            if (!string.Equals(conversation.Subject, subject, StringComparison.Ordinal))
            {
                conversation.Subject = subject;
            }

            return conversation;
        }

        conversation = new InternalConversation
        {
            InternalConversationId = Guid.CreateVersion7(),
            CompanyId = quotation.CompanyId,
            Subject = subject,
            RelatedType = InternalMailRelatedType.Quotation,
            RelatedId = quotation.QuotationId,
            RelatedExternalId = quotation.ExternalId,
            CreatedBy = currentEmployeeId,
            CreatedAt = now,
            LastMessageAt = now,
            IsActive = true
        };

        await _internalMailDbContext.InternalConversations.AddAsync(
            conversation,
            cancellationToken);

        // Lưu conversation trước để tránh chu trình FK với LastMessageId của message đầu tiên.
        await _internalMailDbContext.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    private async Task EnsureParticipantsAsync(
        Guid conversationId,
        Guid senderEmployeeId,
        IReadOnlyCollection<Guid> participantIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existingParticipants = await _internalMailDbContext.InternalConversationParticipants
            .AsTracking()
            .Where(x =>
                x.InternalConversationId == conversationId &&
                participantIds.Contains(x.EmployeeId))
            .ToListAsync(cancellationToken);
        var participantByEmployeeId = existingParticipants.ToDictionary(x => x.EmployeeId);

        foreach (var employeeId in participantIds)
        {
            if (participantByEmployeeId.TryGetValue(employeeId, out var participant))
            {
                participant.IsActive = true;
                participant.DeletedAt = null;
                participant.DeletedByEmployeeId = null;
                participant.IsArchived = false;
                participant.ArchivedAt = null;
                participant.IsMuted = false;
                if (employeeId == senderEmployeeId)
                {
                    participant.Role = InternalConversationParticipantRole.Owner;
                    participant.LastReadAt = now;
                }

                continue;
            }

            await _internalMailDbContext.InternalConversationParticipants.AddAsync(
                new InternalConversationParticipant
                {
                    InternalConversationId = conversationId,
                    EmployeeId = employeeId,
                    Role = employeeId == senderEmployeeId
                        ? InternalConversationParticipantRole.Owner
                        : InternalConversationParticipantRole.Member,
                    JoinedAt = now,
                    LastReadAt = employeeId == senderEmployeeId ? now : null,
                    IsArchived = false,
                    IsMuted = false
                },
                cancellationToken);
        }
    }

    private async Task<IReadOnlyCollection<Guid>> ResolveManagementRecipientsAsync(
        Guid companyId,
        Guid saleEmployeeId,
        Guid senderEmployeeId,
        CancellationToken cancellationToken)
    {
        var saleGroupLeaderIds = await _saleGroupRecipientResolver.ResolveSaleGroupLeaderIdsAsync(
            companyId,
            saleEmployeeId,
            senderEmployeeId,
            cancellationToken);
        var globalManagementIds = await _saleGroupRecipientResolver.ResolveActiveEmployeeIdsByRolesAsync(
            companyId,
            GlobalManagementRoles,
            senderEmployeeId,
            cancellationToken);

        return saleGroupLeaderIds
            .Concat(globalManagementIds)
            .Distinct()
            .ToArray();
    }

    private sealed record QuotationRequestProjection(
        Guid QuotationId,
        string ExternalId,
        Guid CompanyId,
        Guid CustomerId,
        string CustomerName,
        Guid SaleEmployeeId,
        QuotationStatus Status,
        int ActiveLineCount);
}
