using System.Text.Json;
using System.Text.Json.Nodes;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed partial class QuotationSendService
{
    public async Task<OperationResult> RejectConfirmationAsync(Guid quotationId, RejectQuotationSentRequest request,
        CancellationToken cancellationToken)
    {
        if (!_permissions.HasPermission(ApplicationPermissions.Quotations.ConfirmSent))
            return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.Forbidden, "You cannot reject quotation sending.");
        var reason = request.Reason?.Trim();
        if (quotationId == Guid.Empty || request.RequestMessageId == Guid.Empty || !request.ExpectedUpdatedDate.HasValue || request.ExpectedUpdatedDate == default(DateTime) ||
            string.IsNullOrWhiteSpace(reason) || reason.Length > MaximumReasonLength)
            return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.InvalidRequest,
                "Canonical expectedUpdatedDate and a rejection reason of 1 to 4000 characters are required.");
        using var lease = await _mutationLock.AcquireAsync(quotationId, cancellationToken);
        await using var transaction = await _writeDbContext.BeginQuotationTransactionAsync(cancellationToken);
        var committed = false;
        try
        {
            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var quote = await _writeDbContext.Quotations.AsTracking().Include(x => x.Lines).ThenInclude(x => x.PriceTiers)
                .Include(x => x.Terms).FirstOrDefaultAsync(x => x.QuotationId == quotationId && x.CompanyId == scope.CompanyId && x.IsActive, cancellationToken);
            if (quote is null || !await _visibilityService.ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
                    .AnyAsync(x => x.CustomerId == quote.CustomerId, cancellationToken))
                return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.NotFound, "Quotation was not found or is outside your visibility scope.");
            if (quote.Status != QuotationStatus.PendingSendConfirmation)
                return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.NotPending, "Quotation is no longer awaiting send confirmation.");
            var conflict = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(request.ExpectedUpdatedDate, quote.UpdatedDate, "Quotation");
            if (conflict is not null) return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.Conflict, conflict);
            var selected = await ResolveConfirmationRequestAsync(quote, request.RequestMessageId, cancellationToken);
            if (selected.Error is not null) return selected.Error;
            var payload = selected.Payload!;
            var message = await LoadPendingMessageAsync(quote, selected.Message!.InternalMessageId, cancellationToken);
            if (message is null) return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.RequestOutdated, "The pending request no longer exists.");
            var now = QuotationWorkflowRules.NextUpdatedDate(_dateTimeProvider.Now, quote.UpdatedDate);
            payload.Status = QuotationSendConfirmationPayload.RejectedStatus;
            payload.IsUrgent = false;
            payload.RejectedAt = now;
            payload.RejectedBy = scope.EmployeeId;
            payload.RejectionReason = reason;
            payload.RejectedPricingFingerprint = QuotationSendRejectionRules.PricingFingerprint(quote);
            var json = JsonNode.Parse(message.PayloadJson!)!.AsObject();
            json["status"] = payload.Status;
            json["isUrgent"] = false;
            json["rejectedAt"] = JsonValue.Create(now);
            json["rejectedBy"] = scope.EmployeeId.ToString();
            json["rejectionReason"] = reason;
            json["rejectedPricingFingerprint"] = payload.RejectedPricingFingerprint;
            message.PayloadJson = json.ToJsonString(PayloadJsonOptions);
            message.IsUrgent = false;
            quote.Status = QuotationStatus.SendRejected;
            quote.UpdatedDate = now;
            quote.UpdatedBy = scope.EmployeeId;
            _writeDbContext.QuotationStatusHistories.Add(new QuotationStatusHistory
            {
                Id = Guid.CreateVersion7(), QuotationId = quotationId, FromStatus = QuotationStatus.PendingSendConfirmation,
                ToStatus = QuotationStatus.SendRejected, ChangedBy = scope.EmployeeId, ChangedDate = now, Note = reason
            });
            await ResolvePendingNotificationsAsync(scope.CompanyId, quotationId, message.InternalMessageId, payload, cancellationToken);
            var conversation = await _internalMailDbContext.InternalConversations.AsTracking().SingleAsync(x =>
                x.InternalConversationId == payload.ConversationId && x.CompanyId == scope.CompanyId && x.IsActive, cancellationToken);
            var actorName = await _internalMailDbContext.Employees.AsNoTracking().Where(x => x.CompanyId == scope.CompanyId && x.EmployeeId == scope.EmployeeId)
                .Select(x => x.FullName).FirstOrDefaultAsync(cancellationToken) ?? "Ban Giám đốc";
            var requestedRecipients = (await ResolveManagementRecipientsAsync(scope.CompanyId, quote.SaleEmployeeId, scope.EmployeeId, cancellationToken))
                .Append(quote.SaleEmployeeId).Append(message.SenderEmployeeId).Append(scope.EmployeeId).Distinct().ToArray();
            var recipients = await _internalMailDbContext.Employees.AsNoTracking()
                .Where(x => x.CompanyId == scope.CompanyId && x.IsActive && requestedRecipients.Contains(x.EmployeeId))
                .Select(x => x.EmployeeId).ToArrayAsync(cancellationToken);
            await EnsureParticipantsAsync(conversation.InternalConversationId, scope.EmployeeId, recipients, now, cancellationToken);
            var notice = new InternalMessage
            {
                InternalMessageId = Guid.CreateVersion7(), InternalConversationId = conversation.InternalConversationId,
                SenderEmployeeId = scope.EmployeeId, MessageType = InternalMessageType.System, SentAt = now,
                Body = $"{actorName} đã từ chối gửi báo giá {quote.ExternalId}.\nLý do: {reason}\nSale cần chỉnh giá và gửi lại. PDF bị khóa đến khi báo giá được đánh dấu đã gửi."
            };
            notice.PayloadJson = JsonSerializer.Serialize(new { contentType = "QuotationSendRejected", quotationId,
                conversationId = conversation.InternalConversationId, messageId = notice.InternalMessageId, status = "rejected" }, PayloadJsonOptions);
            _internalMailDbContext.InternalMessages.Add(notice);
            conversation.LastMessageId = notice.InternalMessageId;
            conversation.LastMessageAt = now;
            foreach (var id in recipients)
                _internalMailDbContext.InternalMessageReadStates.Add(new InternalMessageReadState
                { InternalMessageId = notice.InternalMessageId, EmployeeId = id, IsRead = id == scope.EmployeeId, ReadAt = id == scope.EmployeeId ? now : null });
            await _writeDbContext.SaveChangesAsync(cancellationToken);
            await _notificationService.PublishAsync(new PublishNotificationRequest
            {
                CompanyId = scope.CompanyId, CreatedBy = scope.EmployeeId, CreatedByNameSnapshot = actorName,
                Topic = TopicNotifications.QuotationSendConfirmationRejected, Severity = NotificationSeverity.Error,
                Title = $"Bị từ chối gửi: {quote.ExternalId}", Message = "BGĐ đã từ chối mức giá gửi khách. Xem lý do trong báo giá và chỉnh giá trước khi gửi lại.",
                Link = $"/crm/quotations/{quotationId}", AggregateId = quotationId, AggregateCode = quote.ExternalId,
                ConversationId = conversation.InternalConversationId, MessageId = notice.InternalMessageId,
                PayloadJson = notice.PayloadJson, TargetUserIds = recipients
            }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return OperationResult.Ok("Quotation sending rejected.");
        }
        catch (DbUpdateConcurrencyException exception)
        { return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.Conflict, OptimisticConcurrencyHelper.CreateConflictMessage("Quotation", exception)); }
        finally
        {
            if (!committed)
            {
                try { await transaction.RollbackAsync(CancellationToken.None); }
                finally { _writeDbContext.ClearTrackedChanges(); }
            }
        }
    }

    private async Task<bool> RejectedPricesUnchangedAsync(Quotation quote, CancellationToken cancellationToken)
    {
        var rejected = await _internalMailDbContext.InternalMessages.AsNoTracking().Where(x => x.PayloadJson != null &&
            x.Conversation.CompanyId == quote.CompanyId && x.Conversation.IsActive && x.Conversation.RelatedType == InternalMailRelatedType.Quotation &&
            x.Conversation.RelatedId == quote.QuotationId).Select(x => x.PayloadJson).ToArrayAsync(cancellationToken);
        var latest = rejected.Select(ReadPayload).Where(x => x?.QuotationId == quote.QuotationId && x.Status == QuotationSendConfirmationPayload.RejectedStatus)
            .OrderByDescending(x => x!.RejectedAt).FirstOrDefault();
        // Old refusals before the most recent Sent are history, not a gate for a later workflow.
        if (latest is null) return quote.Status == QuotationStatus.SendRejected;
        if (!latest.RejectedAt.HasValue) return true;
        return (!quote.SentDate.HasValue || latest.RejectedAt > quote.SentDate) &&
            (latest.RejectedPricingFingerprint is null || latest.RejectedPricingFingerprint == QuotationSendRejectionRules.PricingFingerprint(quote));
    }
}
