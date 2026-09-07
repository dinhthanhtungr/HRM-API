using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

/// <summary>
/// Biến trạng thái RepricingRequired thành action message trong đúng thread báo giá và notification cho người xử lý.
/// Payload đã lưu là dấu chống lặp, nên không cần thêm cột hay migration chỉ để lưu reminder flag.
/// </summary>
internal sealed class QuotationPricingExpiryReminderProcessor
    : IQuotationPricingExpiryReminderProcessor
{
    private const int BatchSize = 100;
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] GlobalManagementRoles =
    [
        ApplicationRoles.President.ToUpperInvariant(),
        ApplicationRoles.Developer.ToUpperInvariant(),
    ];

    private readonly ICRMWriteDbContext _crmDbContext;
    private readonly IInternalMailDbContext _internalMailDbContext;
    private readonly INotificationDbContext _notificationDbContext;
    private readonly ISaleGroupRecipientResolver _saleGroupRecipientResolver;
    private readonly INotificationService _notificationService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly QuotationFeatureOptions _featureOptions;
    private readonly KeyedMutationLock<Guid> _mutationLock;
    private readonly QuotationPricingApprovalStateService _approvalStateService;

    public QuotationPricingExpiryReminderProcessor(
        ICRMWriteDbContext crmDbContext,
        IInternalMailDbContext internalMailDbContext,
        INotificationDbContext notificationDbContext,
        ISaleGroupRecipientResolver saleGroupRecipientResolver,
        INotificationService notificationService,
        IDateTimeProvider dateTimeProvider,
        QuotationFeatureOptions featureOptions,
        KeyedMutationLock<Guid> mutationLock,
        QuotationPricingApprovalStateService approvalStateService)
    {
        _crmDbContext = crmDbContext;
        _internalMailDbContext = internalMailDbContext;
        _notificationDbContext = notificationDbContext;
        _saleGroupRecipientResolver = saleGroupRecipientResolver;
        _notificationService = notificationService;
        _dateTimeProvider = dateTimeProvider;
        _featureOptions = featureOptions;
        _mutationLock = mutationLock;
        _approvalStateService = approvalStateService;
    }

    public async Task<int> ProcessExpiredPricingAsync(
        CancellationToken cancellationToken = default)
    {
        var reconciledCount = await _approvalStateService.ReconcilePendingAsync(
            cancellationToken);
        if (_featureOptions.ApprovedPricingReviewAfterDays <= 0)
        {
            return reconciledCount;
        }

        var now = _dateTimeProvider.Now;
        var reviewCutoff = now.AddDays(-_featureOptions.ApprovedPricingReviewAfterDays);
        var candidates = await _crmDbContext.QuotationLines
            .AsNoTracking()
            .Where(line =>
                line.IsActive &&
                line.Quotation.IsActive &&
                line.Quotation.Status == QuotationStatus.Approved &&
                line.ProductPricingVersionId.HasValue &&
                line.ProductPricingVersion != null &&
                line.ProductPricingVersion.IsActive &&
                line.ProductPricingVersion.Status == ProductPricingStatus.Approved &&
                (line.ProductPricingVersion.ApprovedAt ??
                    line.ProductPricingVersion.UpdatedDate ??
                    line.ProductPricingVersion.CreatedDate) <= reviewCutoff)
            .Select(line => new PricingExpiryCandidate(
                line.QuotationId,
                line.Quotation.ExternalId,
                line.Quotation.CompanyId,
                line.Quotation.SaleEmployeeId,
                line.ProductId,
                line.ProductExternalIdSnapshot,
                line.ProductPricingVersionId!.Value,
                line.ProductPricingVersion!.Version,
                line.ProductPricingVersion.ApprovedAt ??
                    line.ProductPricingVersion.UpdatedDate ??
                    line.ProductPricingVersion.CreatedDate,
                line.ProductPricingVersion.ApprovedBy ?? line.ProductPricingVersion.CreatedBy))
            .OrderBy(x => x.PricingApprovedAt)
            .ThenBy(x => x.QuotationId)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var targets = candidates
            .GroupBy(x => x.QuotationId)
            .Select(x => x.First())
            .ToArray();
        var processedCount = 0;

        foreach (var target in targets)
        {
            using var mutationLease = await _mutationLock.AcquireAsync(
                target.QuotationId,
                cancellationToken);
            if (await ProcessTargetAsync(target, now, cancellationToken))
            {
                processedCount++;
            }
        }

        return reconciledCount + processedCount;
    }

    private async Task<bool> ProcessTargetAsync(
        PricingExpiryCandidate target,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var quotation = await _crmDbContext.Quotations
            .AsTracking()
            .FirstOrDefaultAsync(x =>
                x.QuotationId == target.QuotationId &&
                x.CompanyId == target.CompanyId &&
                x.IsActive &&
                x.Status == QuotationStatus.Approved,
                cancellationToken);
        if (quotation is null)
        {
            return false;
        }

        quotation.Status = QuotationStatus.PendingApproval;
        quotation.UpdatedBy = target.SystemSenderEmployeeId;
        quotation.UpdatedDate = now;
        _crmDbContext.QuotationStatusHistories.Add(new QuotationStatusHistory
        {
            Id = Guid.CreateVersion7(),
            QuotationId = quotation.QuotationId,
            FromStatus = QuotationStatus.Approved,
            ToStatus = QuotationStatus.PendingApproval,
            Note = "Giá chuẩn hết hiệu lực; cần duyệt lại trước khi gửi khách.",
            ChangedBy = target.SystemSenderEmployeeId,
            ChangedDate = now
        });

        var dueDate = target.PricingApprovedAt.AddDays(
            _featureOptions.ApprovedPricingReviewAfterDays);
        var markerJson = JsonSerializer.Serialize(
            new
            {
                contentType = "QuotationPricingExpired",
                relatedId = target.QuotationId,
                productPricingVersionId = target.ProductPricingVersionId
            },
            PayloadJsonOptions);

        var existingMessage = await _internalMailDbContext.InternalMessages
            .AsNoTracking()
            .Where(message =>
                !message.IsDeleted &&
                message.Conversation.CompanyId == target.CompanyId &&
                message.Conversation.IsActive &&
                message.Conversation.RelatedType == InternalMailRelatedType.Quotation &&
                message.Conversation.RelatedId == target.QuotationId &&
                message.PayloadJson != null &&
                EF.Functions.JsonContains(message.PayloadJson, markerJson))
            .Select(message => new ExistingMessage(
                message.InternalMessageId,
                message.InternalConversationId,
                message.PayloadJson!))
            .FirstOrDefaultAsync(cancellationToken);
        var hasNotification = await _notificationDbContext.Notifications
            .AsNoTracking()
            .AnyAsync(notification =>
                notification.CompanyId == target.CompanyId &&
                notification.Topic == TopicNotifications.QuotationPricingExpired &&
                notification.PayloadJson != null &&
                EF.Functions.JsonContains(notification.PayloadJson, markerJson),
                cancellationToken);

        if (existingMessage is not null && hasNotification)
        {
            await _crmDbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        var recipientIds = await ResolveRecipientIdsAsync(target, cancellationToken);
        var message = existingMessage;
        if (message is null)
        {
            var productCodes = await _crmDbContext.QuotationLines
                .AsNoTracking()
                .Where(line => line.QuotationId == target.QuotationId && line.IsActive)
                .OrderBy(line => line.SortOrder)
                .ThenBy(line => line.QuotationLineId)
                .Select(line => line.ProductExternalIdSnapshot)
                .ToListAsync(cancellationToken);
            var conversation = await FindOrCreateConversationAsync(
                target, productCodes, now, cancellationToken);
            var participants = recipientIds
                .Append(target.SystemSenderEmployeeId)
                .Distinct()
                .ToArray();
            await EnsureParticipantsAsync(
                conversation.InternalConversationId,
                target.SystemSenderEmployeeId,
                participants,
                now,
                cancellationToken);

            var internalMessage = new InternalMessage
            {
                InternalMessageId = Guid.CreateVersion7(),
                InternalConversationId = conversation.InternalConversationId,
                SenderEmployeeId = target.SystemSenderEmployeeId,
                MessageType = InternalMessageType.Action,
                Body = $"Giá chuẩn của [{target.ProductCode}] đã hết hạn rà soát từ " +
                    $"{dueDate:dd/MM/yyyy}. Vui lòng báo giá lại cho {target.QuotationExternalId}.",
                IsUrgent = true,
                SentAt = now,
                IsEdited = false,
                IsDeleted = false
            };
            internalMessage.PayloadJson = JsonSerializer.Serialize(
                CreatePayload(target, conversation.InternalConversationId,
                    internalMessage.InternalMessageId, dueDate),
                PayloadJsonOptions);
            await _internalMailDbContext.InternalMessages.AddAsync(internalMessage, cancellationToken);
            await _internalMailDbContext.InternalMessageReferences.AddAsync(
                new InternalMessageReference
                {
                    InternalMessageReferenceId = Guid.CreateVersion7(),
                    InternalMessageId = internalMessage.InternalMessageId,
                    RelatedType = InternalMailRelatedType.Quotation,
                    RelatedId = target.QuotationId,
                    RelatedExternalId = target.QuotationExternalId,
                    RelatedNameSnapshot = conversation.Subject,
                    IsPrimary = true
                },
                cancellationToken);
            conversation.LastMessageId = internalMessage.InternalMessageId;
            conversation.LastMessageAt = now;

            foreach (var employeeId in participants)
            {
                await _internalMailDbContext.InternalMessageReadStates.AddAsync(
                    new InternalMessageReadState
                    {
                        InternalMessageId = internalMessage.InternalMessageId,
                        EmployeeId = employeeId,
                        IsRead = employeeId == target.SystemSenderEmployeeId,
                        ReadAt = employeeId == target.SystemSenderEmployeeId ? now : null
                    },
                    cancellationToken);
            }

            await _internalMailDbContext.SaveChangesAsync(cancellationToken);
            message = new ExistingMessage(
                internalMessage.InternalMessageId,
                conversation.InternalConversationId,
                internalMessage.PayloadJson);
        }

        if (hasNotification || recipientIds.Count == 0)
        {
            await _crmDbContext.SaveChangesAsync(cancellationToken);
            return existingMessage is null;
        }

        var senderName = await _internalMailDbContext.Employees
            .AsNoTracking()
            .Where(employee => employee.EmployeeId == target.SystemSenderEmployeeId)
            .Select(employee => employee.FullName)
            .FirstOrDefaultAsync(cancellationToken);
        senderName = QuotationRules.TrimToNull(senderName) ?? "Hệ thống";

        await _notificationService.PublishAsync(
            new PublishNotificationRequest
            {
                CompanyId = target.CompanyId,
                CreatedBy = target.SystemSenderEmployeeId,
                CreatedByNameSnapshot = senderName,
                Topic = TopicNotifications.QuotationPricingExpired,
                Severity = NotificationSeverity.Warning,
                Title = $"Giá hết hiệu lực: {target.QuotationExternalId}",
                Message = $"Giá chuẩn [{target.ProductCode}] đã hết hạn rà soát. " +
                    $"Vui lòng báo giá lại cho {target.QuotationExternalId}.",
                Link = null,
                AggregateId = target.QuotationId,
                AggregateCode = target.QuotationExternalId,
                ConversationId = message.ConversationId,
                MessageId = message.MessageId,
                PayloadJson = message.PayloadJson,
                TargetUserIds = recipientIds
            },
            cancellationToken);
        return true;
    }

    private async Task<IReadOnlyCollection<Guid>> ResolveRecipientIdsAsync(
        PricingExpiryCandidate target,
        CancellationToken cancellationToken)
    {
        var saleGroupLeaderIds = await _saleGroupRecipientResolver.ResolveSaleGroupLeaderIdsAsync(
            target.CompanyId,
            target.SaleEmployeeId,
            cancellationToken: cancellationToken);
        var managementIds = await _saleGroupRecipientResolver.ResolveActiveEmployeeIdsByRolesAsync(
            target.CompanyId,
            GlobalManagementRoles,
            cancellationToken: cancellationToken);
        var requestedRecipientIds = saleGroupLeaderIds
            .Append(target.SaleEmployeeId)
            .Concat(managementIds)
            .Distinct()
            .ToArray();

        return await _internalMailDbContext.Employees
            .AsNoTracking()
            .Where(employee =>
                employee.CompanyId == target.CompanyId &&
                employee.IsActive &&
                requestedRecipientIds.Contains(employee.EmployeeId))
            .Select(employee => employee.EmployeeId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    private async Task<InternalConversation> FindOrCreateConversationAsync(
        PricingExpiryCandidate target,
        IReadOnlyCollection<string> productCodes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var subject = QuotationConversationSubjectService.BuildSubject(
            target.QuotationExternalId,
            productCodes);
        var conversation = await _internalMailDbContext.InternalConversations
            .AsTracking()
            .FirstOrDefaultAsync(x =>
                x.CompanyId == target.CompanyId &&
                x.IsActive &&
                x.RelatedType == InternalMailRelatedType.Quotation &&
                x.RelatedId == target.QuotationId,
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
            CompanyId = target.CompanyId,
            Subject = subject,
            RelatedType = InternalMailRelatedType.Quotation,
            RelatedId = target.QuotationId,
            RelatedExternalId = target.QuotationExternalId,
            CreatedBy = target.SystemSenderEmployeeId,
            CreatedAt = now,
            LastMessageAt = now,
            IsActive = true
        };
        await _internalMailDbContext.InternalConversations.AddAsync(conversation, cancellationToken);
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
            .Where(participant =>
                participant.InternalConversationId == conversationId &&
                participantIds.Contains(participant.EmployeeId))
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

    private static QuotationPricingExpiredMessagePayload CreatePayload(
        PricingExpiryCandidate target,
        Guid conversationId,
        Guid messageId,
        DateTime dueDate)
        => new()
        {
            RelatedId = target.QuotationId,
            RelatedExternalId = target.QuotationExternalId,
            ConversationId = conversationId,
            MessageId = messageId,
            ProductId = target.ProductId,
            ProductCode = target.ProductCode,
            ProductPricingVersionId = target.ProductPricingVersionId,
            ProductPricingVersion = target.ProductPricingVersion,
            PricingReviewDueDate = dueDate,
            Action = new QuotationPricingExpiredMessageActionDto
            {
                Parameters = new QuotationPricingExpiredMessageActionParametersDto
                {
                    QuotationId = target.QuotationId,
                    QuotationExternalId = target.QuotationExternalId,
                    ProductId = target.ProductId,
                    ProductPricingVersionId = target.ProductPricingVersionId
                }
            }
        };

    private sealed record PricingExpiryCandidate(
        Guid QuotationId,
        string QuotationExternalId,
        Guid CompanyId,
        Guid SaleEmployeeId,
        Guid ProductId,
        string ProductCode,
        Guid ProductPricingVersionId,
        int ProductPricingVersion,
        DateTime PricingApprovedAt,
        Guid SystemSenderEmployeeId);

    private sealed record ExistingMessage(
        Guid MessageId,
        Guid ConversationId,
        string PayloadJson);
}
