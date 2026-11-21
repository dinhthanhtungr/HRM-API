using System.Text.Json.Nodes;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed partial class QuotationSendService
{
    internal async Task AutoSendAfterPricingApprovalAsync(Guid quotationId,
        QuotationPricingApprovalAutoSend approval, CancellationToken cancellationToken)
    {
        using var lease = await _mutationLock.AcquireAsync(quotationId, cancellationToken);
        await using var transaction = await _writeDbContext.BeginQuotationTransactionAsync(cancellationToken);
        var committed = false;
        try
        {
            var quotation = await _writeDbContext.Quotations.AsTracking().Include(x => x.Customer)
                .Include(x => x.Lines).ThenInclude(x => x.PriceTiers).Include(x => x.Terms)
                .FirstOrDefaultAsync(x => x.QuotationId == quotationId && x.CompanyId == approval.CompanyId && x.IsActive,
                    cancellationToken);
            if (quotation?.Status != QuotationStatus.PendingSendConfirmation) return;
            var lines = quotation.Lines.Where(x => x.IsActive).ToArray();
            if (!lines.Any(x => x.ProductId == approval.ProductId) || lines.Any(x => !x.PriceTiers.Any(t => t.IsActive) ||
                x.PriceTiers.Any(t => t.IsActive && (t.UnitPrice < 0 || t.CommissionAmount < 0)))) return;

            var selection = await ResolveConfirmationRequestAsync(quotation, null, cancellationToken);
            if (selection.Error is not null || selection.Message is null || selection.Payload is null) return;
            var payload = selection.Payload;
            // Legacy requests without a commercial fingerprint remain available for manual approval only.
            if (payload.CommercialFingerprintVersion != 1 || payload.CommercialFingerprint is null ||
                selection.Message.SentAt > approval.ApprovedAt || payload.PricingEvaluatedAt > approval.ApprovedAt) return;

            IReadOnlyList<QuotationSendPriceSegment> comparison;
            try { comparison = await ComparePricesAsync(quotation, cancellationToken); }
            catch (QuotationSendComparisonException) { return; } // Missing/expired standards must never send automatically.
            if (comparison.Count == 0 || comparison.Any(x => x.IsBelowReference)) return;

            var actor = await _internalMailDbContext.Employees.AsNoTracking()
                .Where(x => x.CompanyId == approval.CompanyId && x.EmployeeId == approval.ApprovedBy && x.IsActive)
                .Select(x => new { x.FullName }).FirstOrDefaultAsync(cancellationToken);
            if (actor is null) return;
            var actorName = QuotationRules.TrimToNull(actor.FullName) ?? "Người duyệt giá chuẩn";
            var requestedRecipients = (await ResolveManagementRecipientsAsync(approval.CompanyId, quotation.SaleEmployeeId,
                    approval.ApprovedBy, cancellationToken))
                .Append(selection.Message.SenderEmployeeId).Append(quotation.SaleEmployeeId).Append(approval.ApprovedBy).Distinct().ToArray();
            var recipients = await _internalMailDbContext.Employees.AsNoTracking()
                .Where(x => x.CompanyId == approval.CompanyId && x.IsActive && requestedRecipients.Contains(x.EmployeeId))
                .Select(x => x.EmployeeId).ToArrayAsync(cancellationToken);
            var participants = recipients.Append(approval.ApprovedBy).Distinct().ToArray();
            var conversation = await _internalMailDbContext.InternalConversations.AsTracking()
                .FirstOrDefaultAsync(x => x.InternalConversationId == payload.ConversationId &&
                    x.CompanyId == approval.CompanyId && x.IsActive && x.RelatedType == InternalMailRelatedType.Quotation &&
                    x.RelatedId == quotationId, cancellationToken);
            if (conversation is null) return;
            await EnsureParticipantsAsync(conversation.InternalConversationId, approval.ApprovedBy, participants,
                _dateTimeProvider.Now, cancellationToken);
            var message = await LoadPendingMessageAsync(quotation, selection.Message.InternalMessageId, cancellationToken)
                ?? throw new InvalidOperationException("Pending request disappeared during automatic sending.");
            var now = _dateTimeProvider.Now;
            payload.Status = QuotationSendConfirmationPayload.ConfirmedStatus;
            payload.IsUrgent = false;
            payload.ConfirmedBy = approval.ApprovedBy;
            payload.ConfirmedAt = now;
            // Patch lifecycle fields only. Preserve the original Body, comparison and unknown historical fields verbatim.
            var json = JsonNode.Parse(message.PayloadJson!)!.AsObject();
            json["status"] = payload.Status;
            json["isUrgent"] = false;
            json["confirmedBy"] = approval.ApprovedBy.ToString();
            json["confirmedAt"] = JsonValue.Create(now);
            json["confirmationMethod"] = QuotationPricingApprovalAutoSend.ConfirmationMethod;
            message.PayloadJson = json.ToJsonString(PayloadJsonOptions);
            message.IsUrgent = false;
            await ResolvePendingNotificationsAsync(approval.CompanyId, quotationId, message.InternalMessageId, payload, cancellationToken, automatic: true);
            var result = await CompleteSentAsync(quotation, QuotationStatus.PendingSendConfirmation,
                CustomerInteractionType.Quotation,
                "Tự động đánh dấu đã gửi: tất cả giá khách không thấp hơn giá chuẩn hiện hành sau khi duyệt giá chuẩn.",
                conversation, participants, recipients, approval.ApprovedBy, actorName, now, cancellationToken, automatic: true);
            if (!result.Success) throw new InvalidOperationException("Automatic quotation sending did not persist.");
            await transaction.CommitAsync(cancellationToken);
            committed = true;
        }
        finally
        {
            if (!committed)
            {
                try { await transaction.RollbackAsync(CancellationToken.None); }
                finally { _writeDbContext.ClearTrackedChanges(); }
            }
        }
    }
}
