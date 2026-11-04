using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed partial class QuotationSendService
{
    private async Task<(InternalMessage? Message, QuotationSendConfirmationPayload? Payload,
        QuotationConfirmationFailure? Error)> ResolveConfirmationRequestAsync(
        Quotation quotation, Guid? requestedMessageId, CancellationToken cancellationToken)
    {
        var messages = await _internalMailDbContext.InternalMessages.AsNoTracking()
            .Include(x => x.SenderEmployee)
            .Where(x => !x.IsDeleted && x.MessageType == InternalMessageType.Action && x.PayloadJson != null &&
                x.Conversation.CompanyId == quotation.CompanyId && x.Conversation.IsActive &&
                x.Conversation.RelatedType == InternalMailRelatedType.Quotation &&
                x.Conversation.RelatedId == quotation.QuotationId)
            .ToListAsync(cancellationToken);
        var pending = messages.Select(message => new { Message = message, Payload = ReadPayload(message.PayloadJson) })
            .Where(x => x.Payload?.Status == QuotationSendConfirmationPayload.PendingStatus).ToArray();
        if (pending.Length == 0)
            return (null, null, QuotationConfirmationFailure.Create(QuotationConfirmationErrors.NoPendingRequest,
                "No pending send confirmation request exists for this quotation."));
        if (pending.Length != 1)
            return (null, null, QuotationConfirmationFailure.Create(QuotationConfirmationErrors.AmbiguousRequests,
                "Multiple pending send confirmation requests exist. Withdraw and submit a new request."));
        var current = pending[0];
        var payload = current.Payload!;
        if ((requestedMessageId.HasValue && current.Message.InternalMessageId != requestedMessageId) ||
            payload.QuotationId != quotation.QuotationId || payload.RequestMessageId != current.Message.InternalMessageId ||
            payload.ConversationId != current.Message.InternalConversationId ||
            payload.ExpectedUpdatedDate != quotation.UpdatedDate)
            return (null, null, QuotationConfirmationFailure.Create(QuotationConfirmationErrors.RequestOutdated,
                "The requested send confirmation is not the current request. Reload and review again."));
        var blocked = QuotationConfirmationCommercialRules.BlockedReason(quotation, payload);
        if (blocked is not null)
            return (null, null, QuotationConfirmationFailure.Create(blocked,
                "The customer commercial terms changed or the stored request is invalid. Review and submit a new request."));
        return (current.Message, payload, null);
    }

    /// <summary>Enrich an already visibility-scoped detail with the viewer's action and immutable request snapshot.</summary>
    public async Task EnrichConfirmationAsync(QuotationDetailDto detail, Guid companyId, CancellationToken cancellationToken)
    {
        detail.CanConfirmSent = false;
        if (!_permissions.HasPermission(ApplicationPermissions.Quotations.ConfirmSent))
        {
            detail.ConfirmSentBlockedReasonCode = QuotationConfirmationErrors.Forbidden;
            return;
        }
        if (detail.Status != QuotationStatus.PendingSendConfirmation)
        {
            detail.ConfirmSentBlockedReasonCode = QuotationConfirmationErrors.NotPending;
            return;
        }
        var quotation = await _readDbContext.Quotations.AsNoTracking()
            .Include(x => x.Lines).ThenInclude(x => x.PriceTiers)
            .Include(x => x.Terms)
            .FirstOrDefaultAsync(x => x.QuotationId == detail.QuotationId && x.CompanyId == companyId && x.IsActive,
                cancellationToken);
        if (quotation is null || quotation.UpdatedDate != detail.UpdatedDate)
        {
            detail.ConfirmSentBlockedReasonCode = QuotationConfirmationErrors.Conflict;
            return;
        }
        var selection = await ResolveConfirmationRequestAsync(quotation, null, cancellationToken);
        if (selection.Error is not null)
        {
            detail.ConfirmSentBlockedReasonCode = selection.Error.Code;
            return;
        }
        var message = selection.Message!;
        var payload = selection.Payload!;
        detail.PendingSendConfirmation = new QuotationPendingSendConfirmationDto
        {
            RequestMessageId = message.InternalMessageId, RequestedAt = message.SentAt,
            RequesterName = message.SenderEmployee?.FullName ?? string.Empty, Body = message.Body,
            ComparisonRuleVersion = payload.ComparisonRuleVersion, Comparison = payload.Comparison
        };
        detail.CanConfirmSent = true;
        detail.ConfirmSentBlockedReasonCode = null;
    }
}
