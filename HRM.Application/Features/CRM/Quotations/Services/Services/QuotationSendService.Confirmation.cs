using System.Globalization;
using System.Text;
using System.Text.Json;
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
    private const int MaximumReasonLength = 4000;
    private readonly ICurrentUserPermissionService _permissions;
    private readonly QuotationFeatureOptions _options;

    public async Task<OperationResult> ExecuteAsync(Guid quotationId, MarkQuotationSentRequest request,
        Guid? requestMessageId, CancellationToken cancellationToken, bool confirmationOnly = false)
    {
        if (confirmationOnly && (!request.ExpectedUpdatedDate.HasValue || request.ExpectedUpdatedDate == default(DateTime)))
            return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.TokenRequired, "ExpectedUpdatedDate is required.");
        if (confirmationOnly && requestMessageId == Guid.Empty)
            return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.InvalidRequest, "RequestMessageId cannot be empty.");
        if (requestMessageId.HasValue && (requestMessageId == Guid.Empty || !request.ExpectedUpdatedDate.HasValue))
            return OperationResult.Fail("RequestMessageId and ExpectedUpdatedDate are required.");
        using var lease = await _mutationLock.AcquireAsync(quotationId, cancellationToken);
        await using var transaction = await _writeDbContext.BeginQuotationTransactionAsync(cancellationToken);
        var committed = false;
        try
        {
            var result = await ApplyAsync(quotationId, request, requestMessageId, cancellationToken, confirmationOnly);
            if (!result.Success)
                return confirmationOnly && result is not QuotationConfirmationFailure && OptimisticConcurrencyHelper.IsConflictMessage(result.Message)
                    ? QuotationConfirmationFailure.Create(QuotationConfirmationErrors.Conflict, result.Message!) : result;
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            return result;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            if (confirmationOnly)
                return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.Conflict,
                    OptimisticConcurrencyHelper.CreateConflictMessage("Quotation", exception));
            return OperationResult.Fail(OptimisticConcurrencyHelper.CreateConflictMessage("Quotation", exception));
        }
        catch (QuotationSendComparisonException exception)
        {
            if (confirmationOnly)
                return QuotationConfirmationFailure.Create(QuotationConfirmationErrors.StandardUnavailable, exception.Message);
            return OperationResult.Fail(exception.Message);
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

    private async Task<IReadOnlyList<QuotationSendPriceSegment>> ComparePricesAsync(
        Quotation quotation, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.Now;
        if (!QuotationPricingCurrencyConverter.TryValidateQuotationCurrency(
                quotation.Currency, quotation.ExchangeRate, out var currencyError))
            throw new QuotationSendComparisonException(currencyError!);
        var ids = quotation.Lines.Where(x => x.IsActive).Select(x => x.ProductId).Distinct().ToArray();
        var versions = await _readDbContext.ProductPricingVersions.AsNoTracking().Include(x => x.PriceTiers)
            .Where(x => x.CompanyId == quotation.CompanyId && ids.Contains(x.ProductId)
                && x.Currency == ProductPricingSourceRules.StandardPricingCurrency && x.IsActive && x.Status == ProductPricingStatus.Approved)
            .ToListAsync(cancellationToken);
        var standards = versions.Where(x => IsEffective(x, now, _options.ApprovedPricingReviewAfterDays))
            .OrderByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
            .ThenByDescending(x => x.Version).ThenBy(x => x.ProductPricingVersionId)
            .GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.First());
        var comparisons = new List<QuotationSendPriceSegment>();
        foreach (var line in quotation.Lines.Where(x => x.IsActive))
        {
            if (!standards.TryGetValue(line.ProductId, out var standard))
                throw new QuotationSendComparisonException($"No effective approved standard price exists for {line.ProductExternalIdSnapshot}.");
            try
            {
                var approvedAt = standard.ApprovedAt ?? standard.UpdatedDate ?? standard.CreatedDate;
                comparisons.AddRange(QuotationSendPriceComparison.Compare(
                    line, standard, quotation.Currency, quotation.ExchangeRate).Select(x => x with
                {
                    StandardValidUntil = _options.ApprovedPricingReviewAfterDays > 0
                        ? approvedAt.AddDays(_options.ApprovedPricingReviewAfterDays) : null
                }));
            }
            catch (InvalidOperationException exception) { throw new QuotationSendComparisonException(exception.Message); }
        }
        return comparisons;
    }

    internal static bool IsEffective(ProductPricingVersion version, DateTime now, int validityDays)
    {
        var approvedAt = version.ApprovedAt ?? version.UpdatedDate ?? version.CreatedDate;
        return approvedAt <= now && (validityDays <= 0 || approvedAt.AddDays(validityDays) > now);
    }

    private Task<InternalMessage?> LoadPendingMessageAsync(Quotation quotation, Guid messageId, CancellationToken cancellationToken)
        => _internalMailDbContext.InternalMessages.AsTracking().FirstOrDefaultAsync(x =>
            x.InternalMessageId == messageId && !x.IsDeleted && x.MessageType == InternalMessageType.Action
            && x.Conversation.CompanyId == quotation.CompanyId && x.Conversation.IsActive
            && x.Conversation.RelatedType == InternalMailRelatedType.Quotation
            && x.Conversation.RelatedId == quotation.QuotationId, cancellationToken);

    internal static QuotationSendConfirmationPayload? ReadPayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("contentType", out var type)
                || type.ValueKind != JsonValueKind.String
                || type.GetString() != QuotationSendConfirmationPayload.ContentTypeCode) return null;
            var payload = JsonSerializer.Deserialize<QuotationSendConfirmationPayload>(json, PayloadJsonOptions);
            return payload?.ActionType == "quotation.confirm-sent" ? payload : null;
        }
        catch (JsonException) { return null; }
    }

    private async Task<OperationResult> RequestConfirmationAsync(Quotation quotation, MarkQuotationSentRequest request,
        IReadOnlyList<QuotationSendPriceSegment> comparison, IReadOnlyCollection<Guid> recipientIds,
        InternalConversation conversation, Guid[] participantIds, Guid actorId, string actorName, DateTime now,
        CancellationToken cancellationToken)
    {
        var previousStatus = quotation.Status;
        quotation.Status = QuotationStatus.PendingSendConfirmation;
        await SyncLinkedSampleQuoteDatesAsync(quotation, actorId, now, cancellationToken);
        quotation.UpdatedBy = actorId;
        quotation.UpdatedDate = QuotationWorkflowRules.NextUpdatedDate(now, quotation.UpdatedDate);
        var messageId = Guid.CreateVersion7();
        var payload = new QuotationSendConfirmationPayload
        {
            QuotationId = quotation.QuotationId, RequestMessageId = messageId,
            ConversationId = conversation.InternalConversationId, ExpectedUpdatedDate = quotation.UpdatedDate,
            Comparison = comparison,
            ComparisonRuleVersion = QuotationSendConfirmationPayload.CurrentComparisonRuleVersion,
            PricingEvaluatedAt = now,
            CommercialFingerprintVersion = 1,
            CommercialFingerprint = QuotationConfirmationCommercialRules.Fingerprint(quotation)
        };
        var body = new StringBuilder($"{actorName} yêu cầu BGĐ xác nhận gửi báo giá {quotation.ExternalId}.\nLý do: {request.Note!.Trim()}\nTiền tệ: {quotation.Currency}\n");
        foreach (var row in comparison)
        {
            var basisNote = row.ComparisonBasis switch
            {
                QuotationSendPriceSegment.AdjacentTierFallback => "; đối chiếu phần hở bằng giá cao hơn của hai mốc liền kề",
                QuotationSendPriceSegment.LastApprovedTierFallback => "; đối chiếu phần vượt mốc cuối bằng giá của mốc chuẩn cuối theo khối lượng",
                QuotationSendPriceSegment.HighestApprovedTierFallback => "; đối chiếu phần trước bảng bằng giá cao nhất bảng chuẩn",
                _ => string.Empty
            };
            body.AppendLine(FormattableString.Invariant($"{row.ProductCode}: {(row.MinInclusive ? "[" : "(")}{row.MinQuantity}; {row.MaxQuantity?.ToString(CultureInfo.InvariantCulture) ?? "∞"}{(row.MaxInclusive ? "]" : ")")}; chuẩn đối chiếu {row.StandardUnitPrice}; khách (gồm hoa hồng) {row.CustomerUnitPrice}; chênh {row.DifferenceAmount}; {row.DifferencePercent?.ToString(CultureInfo.InvariantCulture) ?? "N/A"}%{basisNote}"));
        }
        var message = new InternalMessage
        {
            InternalMessageId = messageId, InternalConversationId = conversation.InternalConversationId,
            SenderEmployeeId = actorId, MessageType = InternalMessageType.Action, Body = body.ToString(),
            PayloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions), IsUrgent = true, SentAt = now
        };
        _internalMailDbContext.InternalMessages.Add(message);
        conversation.LastMessageId = messageId;
        conversation.LastMessageAt = now;
        foreach (var employeeId in participantIds)
            _internalMailDbContext.InternalMessageReadStates.Add(new InternalMessageReadState
            {
                InternalMessageId = messageId, EmployeeId = employeeId, IsRead = employeeId == actorId,
                ReadAt = employeeId == actorId ? now : null
            });
        _writeDbContext.QuotationStatusHistories.Add(new QuotationStatusHistory
        {
            Id = Guid.CreateVersion7(), QuotationId = quotation.QuotationId, FromStatus = previousStatus,
            ToStatus = quotation.Status, ChangedBy = actorId, ChangedDate = now
        });
        await _writeDbContext.SaveChangesAsync(cancellationToken);
        await _notificationService.PublishAsync(new PublishNotificationRequest
        {
            CompanyId = quotation.CompanyId, CreatedBy = actorId, CreatedByNameSnapshot = actorName,
            Topic = TopicNotifications.QuotationSendConfirmationRequested, Severity = NotificationSeverity.Warning,
            Title = $"Khẩn: cần xác nhận giá báo giá {quotation.ExternalId}",
            Message = $"{actorName} yêu cầu xác nhận giá thấp hơn chuẩn. Xem lý do và bảng đối chiếu trong tin nhắn báo giá.",
            Link = $"/crm/quotations/{quotation.QuotationId}", AggregateId = quotation.QuotationId,
            AggregateCode = quotation.ExternalId, ConversationId = conversation.InternalConversationId,
            MessageId = messageId, PayloadJson = message.PayloadJson, TargetUserIds = recipientIds
        }, cancellationToken);
        return OperationResult.Ok("Quotation is awaiting board send confirmation.");
    }
}

internal sealed class QuotationSendComparisonException(string message) : Exception(message);
