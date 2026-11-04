using System.Text.Json;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal sealed partial class QuotationSendService
{
    /// <summary>Withdraw an unsent request without requiring effective pricing or a surviving pending message.</summary>
    public async Task<OperationResult<QuotationTotalsDto>> WithdrawConfirmationAsync(
        Guid quotationId, WithdrawQuotationPricingRequest request, CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        if (quotationId == Guid.Empty || !request.ExpectedUpdatedDate.HasValue ||
            string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            return OperationResult<QuotationTotalsDto>.Fail("ExpectedUpdatedDate and reason are required; reason cannot exceed 500 characters.");

        using var lease = await _mutationLock.AcquireAsync(quotationId, cancellationToken);
        await using var transaction = await _writeDbContext.BeginQuotationTransactionAsync(cancellationToken);
        var committed = false;
        try
        {
            var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
            var quotation = await _writeDbContext.Quotations.AsTracking().FirstOrDefaultAsync(x =>
                x.QuotationId == quotationId && x.CompanyId == scope.CompanyId && x.IsActive, cancellationToken);
            if (quotation is null || !await _visibilityService.ApplyCustomerVisibility(_readDbContext.Customers.AsNoTracking(), scope)
                    .AnyAsync(x => x.CustomerId == quotation.CustomerId, cancellationToken))
                return OperationResult<QuotationTotalsDto>.Fail("Quotation was not found or is outside your visibility scope.");
            var conflict = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(request.ExpectedUpdatedDate, quotation.UpdatedDate, "Quotation");
            if (conflict is not null) return OperationResult<QuotationTotalsDto>.Fail(conflict);
            if (quotation.Status != QuotationStatus.PendingSendConfirmation)
                return OperationResult<QuotationTotalsDto>.Fail("Only a send-pending quotation can withdraw its send confirmation.");

            var now = QuotationWorkflowRules.NextUpdatedDate(_dateTimeProvider.Now, quotation.UpdatedDate);
            var messages = await _internalMailDbContext.InternalMessages.AsTracking().Include(x => x.Conversation)
                .Where(x => !x.IsDeleted && x.MessageType == InternalMessageType.Action && x.PayloadJson != null &&
                    x.Conversation.CompanyId == scope.CompanyId && x.Conversation.IsActive &&
                    x.Conversation.RelatedType == InternalMailRelatedType.Quotation && x.Conversation.RelatedId == quotationId)
                .ToListAsync(cancellationToken);
            var pending = messages.Select(x => new { Message = x, Payload = ReadPayload(x.PayloadJson) })
                .Where(x => x.Payload?.QuotationId == quotationId && x.Payload.Status == QuotationSendConfirmationPayload.PendingStatus).ToArray();
            foreach (var item in pending)
            {
                item.Payload!.Status = QuotationSendConfirmationPayload.SupersededStatus;
                item.Payload.IsUrgent = false;
                item.Payload.SupersededAt = now;
                item.Payload.SupersededBy = scope.EmployeeId;
                item.Message.PayloadJson = JsonSerializer.Serialize(item.Payload, PayloadJsonOptions);
                item.Message.IsUrgent = false;
                await ResolvePendingNotificationsAsync(scope.CompanyId, quotationId, item.Message.InternalMessageId, item.Payload, cancellationToken);
            }
            quotation.Status = QuotationStatus.Draft;
            quotation.UpdatedDate = now;
            quotation.UpdatedBy = scope.EmployeeId;
            _writeDbContext.QuotationStatusHistories.Add(new QuotationStatusHistory
            {
                Id = Guid.CreateVersion7(), QuotationId = quotationId, FromStatus = QuotationStatus.PendingSendConfirmation,
                ToStatus = QuotationStatus.Draft, ChangedBy = scope.EmployeeId, ChangedDate = now, Note = reason
            });
            var boardIds = await _saleGroupRecipientResolver.ResolveActiveEmployeeIdsByRolesAsync(scope.CompanyId,
                HRM.Application.Commons.Authorization.ApplicationRoleSets.Quotations.SendConfirmers.Select(x => x.ToUpperInvariant()).ToArray(),
                Guid.Empty, cancellationToken);
            var recipientIds = boardIds.Concat(pending.Select(x => x.Message.SenderEmployeeId)).Append(quotation.SaleEmployeeId).Distinct().ToArray();
            var recipients = await _internalMailDbContext.Employees.AsNoTracking()
                .Where(x => x.CompanyId == scope.CompanyId && x.IsActive && recipientIds.Contains(x.EmployeeId))
                .Select(x => x.EmployeeId).ToArrayAsync(cancellationToken);
            var actorName = await _internalMailDbContext.Employees.AsNoTracking()
                .Where(x => x.CompanyId == scope.CompanyId && x.EmployeeId == scope.EmployeeId)
                .Select(x => x.FullName).FirstOrDefaultAsync(cancellationToken) ?? "Nhân viên kinh doanh";
            var conversation = await FindOrCreateConversationAsync(quotation, scope.EmployeeId, now, cancellationToken);
            var participants = recipients.Append(scope.EmployeeId).Distinct().ToArray();
            await EnsureParticipantsAsync(conversation.InternalConversationId, scope.EmployeeId, participants, now, cancellationToken);
            // Detect database concurrency before staging notification publication in the same transaction.
            await _writeDbContext.SaveChangesAsync(cancellationToken);
            await PublishRevisionNoticeAsync(quotation, conversation, participants, recipients, scope.EmployeeId, actorName, now,
                cancellationToken, $"{actorName} đã thu hồi yêu cầu xác nhận gửi báo giá {quotation.ExternalId}. Báo giá trở về Nháp, chưa gửi khách.\nLý do: {reason}",
                $"Đã thu hồi yêu cầu xác nhận gửi: {quotation.ExternalId}");
            await _writeDbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return OperationResult<QuotationTotalsDto>.Ok(ToSavedTotals(quotation, null), "Send confirmation withdrawn; quotation returned to Draft.");
        }
        catch (DbUpdateConcurrencyException exception)
        {
            return OperationResult<QuotationTotalsDto>.Fail(OptimisticConcurrencyHelper.CreateConflictMessage("Quotation", exception));
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
