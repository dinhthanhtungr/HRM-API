using System.Text.Json;
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
    /// <summary>Called under the caller's quotation lock and atomic transaction, after staging the complete form.</summary>
    public async Task<OperationResult<QuotationTotalsDto>> ReevaluatePendingAsync(
        Guid quotationId, string? reason, DateTime? originalUpdatedDate, CancellationToken cancellationToken)
    {
        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var quotation = await _writeDbContext.Quotations.AsTracking().Include(x => x.Lines)
            .ThenInclude(x => x.PriceTiers)
            .Include(x => x.Terms)
            .FirstOrDefaultAsync(x => x.QuotationId == quotationId && x.CompanyId == scope.CompanyId && x.IsActive, cancellationToken);
        if (quotation is null || !await _visibilityService.ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
                .AnyAsync(x => x.CustomerId == quotation.CustomerId, cancellationToken))
            return OperationResult<QuotationTotalsDto>.Fail("Quotation was not found or is outside your visibility scope.");
        if (quotation.Status != QuotationStatus.PendingSendConfirmation || !quotation.Lines.Any(x => x.IsActive))
            return OperationResult<QuotationTotalsDto>.Fail("Only an active send-pending quotation can have its confirmation revised.");

        var comparison = await ComparePricesAsync(quotation, cancellationToken);
        var needsConfirmation = comparison.Any(x => x.IsBelowReference);
        if (reason?.Trim().Length > MaximumReasonLength || (needsConfirmation && string.IsNullOrWhiteSpace(reason)))
            return OperationResult<QuotationTotalsDto>.Fail(
                $"sendConfirmationNote is required for below-standard prices after editing a pending send request and cannot exceed {MaximumReasonLength} characters.");

        var messages = await _internalMailDbContext.InternalMessages.AsTracking().Include(x => x.Conversation)
            .Where(x => !x.IsDeleted && x.MessageType == InternalMessageType.Action && x.PayloadJson != null
                && x.Conversation.CompanyId == scope.CompanyId && x.Conversation.IsActive
                && x.Conversation.RelatedType == InternalMailRelatedType.Quotation && x.Conversation.RelatedId == quotationId)
            .ToListAsync(cancellationToken);
        var pending = messages.Select(x => new { Message = x, Payload = ReadPayload(x.PayloadJson) })
            .Where(x => x.Payload?.QuotationId == quotationId && x.Payload.Status == QuotationSendConfirmationPayload.PendingStatus)
            .ToArray();
        if (pending.Length == 0)
            return OperationResult<QuotationTotalsDto>.Fail("No pending confirmation message exists for this quotation. Reload the quotation before editing.");

        var boardIds = await _saleGroupRecipientResolver.ResolveActiveEmployeeIdsByRolesAsync(quotation.CompanyId,
            HRM.Application.Commons.Authorization.ApplicationRoleSets.Quotations.SendConfirmers.Select(x => x.ToUpperInvariant()).ToArray(),
            Guid.Empty, cancellationToken);
        if (needsConfirmation && boardIds.Count == 0)
            return OperationResult<QuotationTotalsDto>.Fail("No active board approver was found in the current company.");
        var requestedRecipients = boardIds.Concat(pending.Select(x => x.Message.SenderEmployeeId))
            .Append(quotation.SaleEmployeeId).Distinct().ToArray();
        var recipients = await _internalMailDbContext.Employees.AsNoTracking()
            .Where(x => x.CompanyId == quotation.CompanyId && x.IsActive && requestedRecipients.Contains(x.EmployeeId))
            .Select(x => x.EmployeeId).ToArrayAsync(cancellationToken);
        var previousDate = quotation.UpdatedDate.HasValue && (!originalUpdatedDate.HasValue || quotation.UpdatedDate > originalUpdatedDate)
            ? quotation.UpdatedDate : originalUpdatedDate;
        var now = QuotationWorkflowRules.NextUpdatedDate(_dateTimeProvider.Now, previousDate);
        foreach (var item in pending)
        {
            item.Payload!.Status = QuotationSendConfirmationPayload.SupersededStatus;
            item.Payload.IsUrgent = false;
            item.Payload.SupersededAt = now;
            item.Payload.SupersededBy = scope.EmployeeId;
            item.Message.PayloadJson = JsonSerializer.Serialize(item.Payload, PayloadJsonOptions);
            item.Message.IsUrgent = false;
            await ResolvePendingNotificationsAsync(quotation.CompanyId, quotationId, item.Message.InternalMessageId,
                item.Payload, cancellationToken);
        }

        var actorName = await _internalMailDbContext.Employees.AsNoTracking()
            .Where(x => x.CompanyId == scope.CompanyId && x.EmployeeId == scope.EmployeeId)
            .Select(x => x.FullName).FirstOrDefaultAsync(cancellationToken) ?? "Nhân viên kinh doanh";
        var conversation = await FindOrCreateConversationAsync(quotation, scope.EmployeeId, now, cancellationToken);
        var participants = recipients.Append(scope.EmployeeId).Distinct().ToArray();
        await EnsureParticipantsAsync(conversation.InternalConversationId, scope.EmployeeId, participants, now, cancellationToken);

        if (needsConfirmation)
        {
            var request = new MarkQuotationSentRequest { Note = reason };
            var result = await RequestConfirmationAsync(quotation, request, comparison, boardIds, conversation, participants,
                scope.EmployeeId, actorName, now, cancellationToken);
            if (!result.Success) return OperationResult<QuotationTotalsDto>.Fail(result.Message!);
            return OperationResult<QuotationTotalsDto>.Ok(ToSavedTotals(quotation, conversation.LastMessageId),
                "Customer prices saved; the old confirmation is superseded and a new board confirmation is pending.");
        }

        quotation.Status = QuotationStatus.Approved;
        quotation.UpdatedBy = scope.EmployeeId;
        quotation.UpdatedDate = now;
        _writeDbContext.QuotationStatusHistories.Add(new QuotationStatusHistory
        {
            Id = Guid.CreateVersion7(), QuotationId = quotationId, FromStatus = QuotationStatus.PendingSendConfirmation,
            ToStatus = quotation.Status, ChangedBy = scope.EmployeeId, ChangedDate = now
        });
        await PublishRevisionNoticeAsync(quotation, conversation, participants, recipients, scope.EmployeeId, actorName, now, cancellationToken);
        return OperationResult<QuotationTotalsDto>.Ok(ToSavedTotals(quotation, null),
            "Customer prices saved; no below-standard confirmation is required. Confirm sending separately.");
    }

    private async Task PublishRevisionNoticeAsync(Quotation quotation, InternalConversation conversation,
        Guid[] participants, Guid[] recipients, Guid actorId, string actorName, DateTime now, CancellationToken cancellationToken,
        string? body = null, string? title = null)
    {
        var message = new InternalMessage
        {
            InternalMessageId = Guid.CreateVersion7(), InternalConversationId = conversation.InternalConversationId,
            SenderEmployeeId = actorId, MessageType = InternalMessageType.System, SentAt = now,
            Body = body ?? $"{actorName} đã cập nhật báo giá {quotation.ExternalId}. Giá khách không thấp hơn mức đối chiếu; yêu cầu xác nhận giá cũ đã hết hiệu lực. Báo giá chưa được đánh dấu đã gửi."
        };
        message.PayloadJson = JsonSerializer.Serialize(new
        {
            contentType = "QuotationSendConfirmationSuperseded", quotationId = quotation.QuotationId,
            conversationId = conversation.InternalConversationId, messageId = message.InternalMessageId,
            status = QuotationSendConfirmationPayload.SupersededStatus, isUrgent = false
        }, PayloadJsonOptions);
        _internalMailDbContext.InternalMessages.Add(message);
        conversation.LastMessageId = message.InternalMessageId;
        conversation.LastMessageAt = now;
        foreach (var employeeId in participants)
            _internalMailDbContext.InternalMessageReadStates.Add(new InternalMessageReadState
            {
                InternalMessageId = message.InternalMessageId, EmployeeId = employeeId,
                IsRead = employeeId == actorId, ReadAt = employeeId == actorId ? now : null
            });
        await _notificationService.PublishAsync(new PublishNotificationRequest
        {
            CompanyId = quotation.CompanyId, CreatedBy = actorId, CreatedByNameSnapshot = actorName,
            Topic = TopicNotifications.QuotationMessageCreated, Severity = NotificationSeverity.Info,
            Title = title ?? $"Đã cập nhật giá khách: {quotation.ExternalId}", Message = message.Body,
            Link = $"/crm/quotations/{quotation.QuotationId}", AggregateId = quotation.QuotationId,
            AggregateCode = quotation.ExternalId, ConversationId = conversation.InternalConversationId,
            MessageId = message.InternalMessageId, PayloadJson = message.PayloadJson,
            TargetUserIds = recipients.Where(x => x != actorId).ToArray(), SilentUserIds = [actorId]
        }, cancellationToken);
    }

    private static QuotationTotalsDto ToSavedTotals(Quotation quotation, Guid? messageId)
        => new()
        {
            Status = quotation.Status, SendConfirmationMessageId = messageId, UpdatedDate = quotation.UpdatedDate,
            SubTotal = quotation.SubTotal, DiscountAmount = quotation.DiscountAmount, TaxPercent = quotation.TaxPercent,
            TaxAmount = quotation.TaxAmount, TotalAmount = quotation.TotalAmount
        };
}
