using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Queries.GetMessages;

internal sealed class GetInternalMessagesQueryHandler
    : IRequestHandler<GetInternalMessagesQuery, PagedResult<InternalMessageDto>?>
{
    private readonly ICurrentUserPermissionService _permissions;
    private const int MaxReplyPreviewLength = 300;
    private readonly IInternalMailDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly InternalMailAreaAccessService _areas;
    private readonly InternalMessageReactionReader? _reactions;

    public GetInternalMessagesQueryHandler(
        IInternalMailDbContext dbContext,
        ICurrentUser currentUser,
        InternalMailAreaAccessService areas, ICurrentUserPermissionService permissions, InternalMessageReactionReader? reactions = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _areas = areas;
        _permissions = permissions;
        _reactions = reactions;
    }

    public async Task<PagedResult<InternalMessageDto>?> Handle(
        GetInternalMessagesQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (request.ConversationId == Guid.Empty || !companyId.HasValue || !employeeId.HasValue)
        {
            return null;
        }

        var visibleIds = _areas.Messages().Select(m => m.InternalMessageId);
        var messageQuery = _areas.Messages(request.AreaCode)
            .AsNoTracking()
            .Where(x => x.InternalConversationId == request.ConversationId);

        // Preserve the authorized conversation check, including empty/wrong-area pages,
        // while counting messages in the same database round trip.
        var access = await _areas.Conversations()
            .Where(c => c.InternalConversationId == request.ConversationId)
            .Select(c => new { TotalCount = messageQuery.Count() })
            .FirstOrDefaultAsync(cancellationToken);
        if (access is null)
        {
            return null;
        }

        var totalCount = access.TotalCount;
        var pageNumber = request.NormalizedPageNumber;
        var pageSize = request.NormalizedPageSize;

        var items = await messageQuery
            .OrderByDescending(x => x.SentAt)
            .ThenByDescending(x => x.InternalMessageId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InternalMessageDto
            {
                MessageId = x.InternalMessageId,
                AreaCode = InternalMailAreaAccessService.AreaOf(x.Conversation.RelatedType),
                ConversationId = x.InternalConversationId,
                SenderEmployeeId = x.SenderEmployeeId,
                SenderName = x.SenderEmployee.FullName,
                MessageType = x.MessageType,
                Body = x.IsDeleted ? string.Empty : x.Body,
                PayloadJson = x.IsDeleted ? null : x.PayloadJson,
                ReplyToMessageId = x.ReplyToMessageId != null && (x.ReplyToMessage!.InternalConversationId == x.InternalConversationId || visibleIds.Contains(x.ReplyToMessageId.Value)) ? x.ReplyToMessageId : null,
                ReplyTo = x.ReplyToMessageId == null || (x.ReplyToMessage!.InternalConversationId != x.InternalConversationId && !visibleIds.Contains(x.ReplyToMessageId.Value)) ? null : new InternalMessageReplyDto
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
                // Missing receipt means this employee has no unread delivery for this message.
                IsRead = !x.ReadStates.Any(state => state.EmployeeId == employeeId.Value && !state.IsRead),
                ReadAt = x.ReadStates
                    .Where(state => state.EmployeeId == employeeId.Value)
                    .Select(state => state.ReadAt)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var confirmationMessages = items.Select(x => new { Message = x, Payload = QuotationSendService.ReadPayload(x.PayloadJson) })
            .Where(x => x.Payload is not null).ToArray();
        if (!_permissions.HasPermission(ApplicationPermissions.Quotations.ConfirmSent))
        {
            foreach (var item in confirmationMessages)
                item.Message.ConfirmSentBlockedReasonCode = QuotationConfirmationErrors.Forbidden;
        }
        else if (confirmationMessages.Length > 0)
        {
            var quotationIds = confirmationMessages.Select(x => x.Payload!.QuotationId).Distinct().ToArray();
            var awaiting = await _dbContext.Quotations.AsNoTracking()
                .Include(x => x.Lines).ThenInclude(x => x.PriceTiers).Include(x => x.Terms)
                .Where(x => quotationIds.Contains(x.QuotationId) && x.CompanyId == companyId && x.IsActive
                    && x.Status == QuotationStatus.PendingSendConfirmation)
                .ToArrayAsync(cancellationToken);
            var actionMessages = await _dbContext.InternalMessages.AsNoTracking()
                .Where(x => !x.IsDeleted && x.MessageType == HRM.Domain.Enums.InternalMailEnums.InternalMessageType.Action
                    && x.PayloadJson != null && x.Conversation.CompanyId == companyId && x.Conversation.IsActive
                    && x.Conversation.RelatedType == HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.Quotation
                    && quotationIds.Contains(x.Conversation.RelatedId!.Value))
                .Select(x => new { x.InternalMessageId, x.InternalConversationId, x.Conversation.RelatedId, x.PayloadJson })
                .ToArrayAsync(cancellationToken);
            var pending = actionMessages.Select(x => new { Message = x, Payload = QuotationSendService.ReadPayload(x.PayloadJson) })
                .Where(x => x.Payload?.Status == QuotationSendConfirmationPayload.PendingStatus).ToArray();
            foreach (var item in confirmationMessages)
            {
                var payload = item.Payload!;
                var quotation = awaiting.FirstOrDefault(x => x.QuotationId == payload.QuotationId);
                var candidates = pending.Where(x => x.Message.RelatedId == payload.QuotationId).ToArray();
                var current = candidates.Length == 1 ? candidates[0] : null;
                var reason = item.Message.IsDeleted || payload.Status != QuotationSendConfirmationPayload.PendingStatus
                    ? QuotationConfirmationErrors.RequestOutdated
                    : quotation is null ? QuotationConfirmationErrors.NotPending
                    : candidates.Length == 0 ? QuotationConfirmationErrors.NoPendingRequest
                    : candidates.Length > 1 ? QuotationConfirmationErrors.AmbiguousRequests
                    : current!.Message.InternalMessageId != item.Message.MessageId
                        || payload.RequestMessageId != item.Message.MessageId
                        || payload.ConversationId != item.Message.ConversationId
                        || current.Message.InternalConversationId != payload.ConversationId
                        ? QuotationConfirmationErrors.RequestOutdated
                        : QuotationConfirmationCommercialRules.BlockedReason(quotation, payload);
                item.Message.ConfirmSentBlockedReasonCode = reason;
                item.Message.CanConfirmSent = reason is null;
            }
        }

        var messageIds = items.Select(x => x.MessageId).ToArray();
        if (messageIds.Length > 0)
        {
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

            foreach (var item in items)
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

        if (_reactions is not null) await _reactions.AttachAsync(items, cancellationToken);
        await _areas.MaskPriceMessagesAsync(items, cancellationToken);
        await InternalMessageSampleWorkAudience.AttachAsync(items, _dbContext, _areas, companyId.Value, cancellationToken);
        return new PagedResult<InternalMessageDto>(items, totalCount, pageNumber, pageSize);
    }
}
