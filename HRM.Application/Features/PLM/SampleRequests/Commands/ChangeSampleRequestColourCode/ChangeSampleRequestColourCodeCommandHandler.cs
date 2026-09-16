using System.Text.Json;
using HRM.Application.Abstractions.Persistence.Notifications;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestDirectPatchNotification;
using HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;
using HRM.Application.Features.PLM.SampleRequests.DirectPatchNotifications;
using HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.ChangeSampleRequestColourCode;

internal sealed class ChangeSampleRequestColourCodeCommandHandler
    : IRequestHandler<ChangeSampleRequestColourCodeCommand, OperationResult<ChangeSampleRequestColourCodeResultDto>>
{
    private const string AuditReason = "SampleRequestColourCodeChanged";
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPLMWriteDbContext _dbContext;
    private readonly INotificationDbContext _notificationDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly SampleRequestConversationSubjectService _conversationSubjectService;
    private readonly DraftQuotationProductSnapshotSyncService _draftQuotationProductSnapshotSyncService;
    private readonly ISender _sender;

    public ChangeSampleRequestColourCodeCommandHandler(
        IPLMWriteDbContext dbContext,
        INotificationDbContext notificationDbContext,
        ICurrentUser currentUser,
        ICustomerVisibilityService visibilityService,
        SampleRequestConversationSubjectService conversationSubjectService,
        DraftQuotationProductSnapshotSyncService draftQuotationProductSnapshotSyncService,
        ISender sender)
    {
        _dbContext = dbContext;
        _notificationDbContext = notificationDbContext;
        _currentUser = currentUser;
        _visibilityService = visibilityService;
        _conversationSubjectService = conversationSubjectService;
        _draftQuotationProductSnapshotSyncService = draftQuotationProductSnapshotSyncService;
        _sender = sender;
    }

    public async Task<OperationResult<ChangeSampleRequestColourCodeResultDto>> Handle(
        ChangeSampleRequestColourCodeCommand request,
        CancellationToken cancellationToken)
    {
        if (request.SampleRequestId == Guid.Empty)
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail("SampleRequestId is invalid.");
        }

        if (request.IdempotencyKey == Guid.Empty)
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail("IdempotencyKey is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ColourCode))
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail("ColourCode is required.");
        }

        if (!_currentUser.IsInAnyRole(ApplicationRoleSets.PLM.ProductTechnicalEditors))
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail(
                "You are not allowed to change the product colour code.");
        }

        var companyId = _currentUser.CompanyId;
        var employeeId = _currentUser.EmployeeId;
        if (!companyId.HasValue || companyId.Value == Guid.Empty ||
            !employeeId.HasValue || employeeId.Value == Guid.Empty)
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail("Current user is invalid.");
        }

        var visibilityScope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var sampleRequest = await _visibilityService.ApplySampleRequestVisibility(
                _dbContext.SampleRequests.Where(x => x.SampleRequestId == request.SampleRequestId),
                _dbContext.Customers.AsNoTracking(),
                visibilityScope)
            .FirstOrDefaultAsync(cancellationToken);

        if (sampleRequest is null)
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail("Sample request was not found.");
        }

        var product = await _dbContext.Products
            .FirstOrDefaultAsync(x =>
                x.ProductId == sampleRequest.ProductId &&
                x.CompanyId == companyId.Value &&
                x.IsActive,
                cancellationToken);

        if (product is null)
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail(
                "Product does not exist or is inactive.");
        }

        var existingResult = await FindExistingResultAsync(
            sampleRequest.SampleRequestId,
            companyId.Value,
            product.ProductId,
            request.IdempotencyKey,
            cancellationToken);
        if (existingResult is not null)
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Ok(
                existingResult,
                "ColourCode change was already processed.");
        }

        if (!SampleRequestColourCodeChangeRules.IsEditableStatus(sampleRequest.Status))
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail(
                "ColourCode can only be changed before the sample request is sent.");
        }

        if (request.ExpectedUpdatedDate.HasValue &&
            sampleRequest.UpdatedDate.HasValue &&
            request.ExpectedUpdatedDate.Value.Ticks != sampleRequest.UpdatedDate.Value.Ticks)
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail(
                "Sample request was changed by another user. Please reload before saving.");
        }

        var hasSentHistory = await _dbContext.SampleRequestSampleTrials
            .AsNoTracking()
            .AnyAsync(x =>
                x.IsActive &&
                x.SampleRequest.CompanyId == companyId.Value &&
                x.SampleRequest.ProductId == product.ProductId &&
                (x.SentDate.HasValue || x.Status != SampleTrialStatus.Draft),
                cancellationToken);

        if (hasSentHistory)
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail(
                "ColourCode cannot be changed because this product already has sample-trial history.");
        }

        var lockedSampleRequestStatuses = new[]
        {
            SampleRequestStatus.SampleSent.ToString(),
            SampleRequestStatus.Completed.ToString(),
            SampleRequestStatus.FormulaUpdateRequested.ToString()
        };
        var hasLockedRelatedSampleRequest = await _dbContext.SampleRequests
            .AsNoTracking()
            .AnyAsync(x =>
                x.CompanyId == companyId.Value &&
                x.ProductId == product.ProductId &&
                x.IsActive &&
                lockedSampleRequestStatuses.Contains(x.Status),
                cancellationToken);

        if (hasLockedRelatedSampleRequest)
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail(
                "ColourCode cannot be changed because this product is used by a sent or completed sample request.");
        }

        var oldColourCode = product.ColourCode?.Trim();
        if (string.IsNullOrWhiteSpace(oldColourCode))
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail(
                "The product does not have a current ColourCode.");
        }

        var resolvedResult = await SampleRequestColourCodeGenerator.ResolveAsync(
            _dbContext,
            request.ColourCode,
            excludedProductId: product.ProductId,
            currentColourCode: null,
            cancellationToken);

        if (!resolvedResult.Success || string.IsNullOrWhiteSpace(resolvedResult.Data?.ColourCode))
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail(
                resolvedResult.Message ?? "ColourCode is invalid.");
        }

        var newColourCode = resolvedResult.Data.ColourCode.Trim();
        if (newColourCode.Length > SampleRequestColourCodeChangeRules.MaxColourCodeLength)
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail(
                $"ColourCode cannot exceed {SampleRequestColourCodeChangeRules.MaxColourCodeLength} characters.");
        }

        if (string.Equals(oldColourCode, newColourCode, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail(
                "The new ColourCode is the same as the current ColourCode.");
        }

        var duplicated = await _dbContext.Products
            .AsNoTracking()
            .AnyAsync(x =>
                x.ProductId != product.ProductId &&
                x.IsActive &&
                x.ColourCode == newColourCode,
                cancellationToken);

        if (duplicated)
        {
            return OperationResult<ChangeSampleRequestColourCodeResultDto>.Fail("ColourCode already exists.");
        }

        var changedAt = DateTime.Now;
        var oldProductSnapshot = SampleRequestDataChangeAuditHelper.BuildProductAuditSnapshot(product);

        product.ColourCode = newColourCode;
        if (!string.IsNullOrWhiteSpace(resolvedResult.Data.AdditiveCode))
        {
            product.Additive = resolvedResult.Data.AdditiveCode;
        }
        product.UpdatedBy = employeeId.Value;
        product.UpdatedDate = changedAt;

        var draftTrials = await _dbContext.SampleRequestSampleTrials
            .Where(x =>
                x.IsActive &&
                x.Status == SampleTrialStatus.Draft &&
                x.SampleRequest.CompanyId == companyId.Value &&
                x.SampleRequest.ProductId == product.ProductId)
            .ToListAsync(cancellationToken);

        foreach (var draftTrial in draftTrials)
        {
            draftTrial.ColourCodeSnapshot = newColourCode;
            draftTrial.UpdatedBy = employeeId.Value;
            draftTrial.UpdatedDate = changedAt;
        }

        var relatedSampleRequests = await _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId.Value &&
                x.ProductId == product.ProductId &&
                x.IsActive)
            .Select(x => new
            {
                x.SampleRequestId,
                x.CompanyId,
                x.ExternalId
            })
            .ToListAsync(cancellationToken);

        var relatedSampleRequestIds = relatedSampleRequests
            .Select(x => x.SampleRequestId)
            .ToArray();
        var relatedConversations = await _dbContext.InternalConversations
            .Where(x =>
                x.CompanyId == companyId.Value &&
                x.IsActive &&
                x.RelatedType == InternalMailRelatedType.SampleRequest &&
                x.RelatedId.HasValue &&
                relatedSampleRequestIds.Contains(x.RelatedId.Value))
            .Select(x => new
            {
                x.InternalConversationId
            })
            .ToListAsync(cancellationToken);
        var updatedConversationCount = relatedConversations.Count;

        foreach (var relatedSampleRequest in relatedSampleRequests)
        {
            await _conversationSubjectService.SyncSubjectAsync(
                relatedSampleRequest.SampleRequestId,
                relatedSampleRequest.CompanyId,
                relatedSampleRequest.ExternalId,
                newColourCode,
                cancellationToken);
        }

        await _draftQuotationProductSnapshotSyncService.SyncColourCodeAsync(
            product.ProductId,
            companyId.Value,
            newColourCode,
            employeeId.Value,
            changedAt,
            cancellationToken);

        var historySyncResult = await ReplaceHistoricalColourCodeAsync(
            companyId.Value,
            relatedConversations.Select(x => x.InternalConversationId).ToArray(),
            relatedSampleRequestIds,
            oldColourCode,
            newColourCode,
            employeeId.Value,
            changedAt,
            cancellationToken);

        await SampleRequestDataChangeAuditHelper.AddProductAuditIfChangedAsync(
            _dbContext,
            product,
            employeeId.Value,
            changedAt,
            request.IdempotencyKey,
            oldProductSnapshot,
            AuditReason,
            cancellationToken);

        sampleRequest.UpdatedBy = employeeId.Value;
        sampleRequest.UpdatedDate = changedAt;
        _dbContext.SampleRequests.Update(sampleRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var message = SampleRequestColourCodeChangeRules.BuildMessage(oldColourCode, newColourCode);
        var notificationResult = await _sender.Send(
            new CreateSampleRequestDirectPatchNotificationCommand
            {
                SampleRequestId = sampleRequest.SampleRequestId,
                IdempotencyKey = request.IdempotencyKey,
                Message = message,
                IsUrgent = request.IsUrgent,
                RecipientEmployeeIds = request.RecipientEmployeeIds,
                TitleOverride = SampleRequestColourCodeChangeRules.BuildNotificationTitle(newColourCode),
                Changes =
                [
                    new SampleRequestDirectPatchChangeDto
                    {
                        FieldCode = "product.colour_code",
                        Label = "Mã màu",
                        OldValue = JsonSerializer.SerializeToElement(oldColourCode),
                        NewValue = JsonSerializer.SerializeToElement(newColourCode)
                    }
                ]
            },
            cancellationToken);

        var notification = notificationResult.Data;
        var result = new ChangeSampleRequestColourCodeResultDto
        {
            SampleRequestId = sampleRequest.SampleRequestId,
            ProductId = product.ProductId,
            OldColourCode = oldColourCode,
            NewColourCode = newColourCode,
            UpdatedDraftTrialCount = draftTrials.Count,
            UpdatedConversationCount = updatedConversationCount,
            UpdatedMessageCount = historySyncResult.UpdatedMessageCount,
            UpdatedNotificationCount = historySyncResult.UpdatedNotificationCount,
            ConversationId = notification?.ConversationId ?? Guid.Empty,
            MessageId = notification?.MessageId ?? Guid.Empty,
            NotificationId = notification?.NotificationId ?? Guid.Empty
        };

        return notificationResult.Success
            ? OperationResult<ChangeSampleRequestColourCodeResultDto>.Ok(
                result,
                "Changed ColourCode and sent the update notification successfully.")
            : OperationResult<ChangeSampleRequestColourCodeResultDto>.Ok(
                result,
                $"Changed ColourCode successfully, but could not send the update notification: {notificationResult.Message}");
    }

    private async Task<ChangeSampleRequestColourCodeResultDto?> FindExistingResultAsync(
        Guid sampleRequestId,
        Guid companyId,
        Guid productId,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var audit = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.RecordId == productId &&
                x.Reason == AuditReason &&
                x.CorrelationId == idempotencyKey)
            .OrderByDescending(x => x.ChangedAt)
            .Select(x => x.ChangedValues)
            .FirstOrDefaultAsync(cancellationToken);

        var conversationId = await _dbContext.InternalConversations
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                x.RelatedType == InternalMailRelatedType.SampleRequest &&
                x.RelatedId == sampleRequestId)
            .Select(x => (Guid?)x.InternalConversationId)
            .FirstOrDefaultAsync(cancellationToken);
        var messages = conversationId.HasValue
            ? await _dbContext.InternalMessages
            .AsNoTracking()
            .Where(x =>
                x.InternalConversationId == conversationId.Value &&
                !x.IsDeleted &&
                x.PayloadJson != null)
            .OrderByDescending(x => x.SentAt)
            .Select(x => new { x.InternalMessageId, x.PayloadJson })
            .ToListAsync(cancellationToken)
            : [];

        foreach (var message in messages)
        {
            var payload = DeserializePayload(message.PayloadJson);
            var directPatch = payload?.DirectPatchNotification;
            if (directPatch?.IdempotencyKey != idempotencyKey)
            {
                continue;
            }

            var colourChange = directPatch.Changes.FirstOrDefault(x =>
                string.Equals(x.FieldCode, "product.colour_code", StringComparison.OrdinalIgnoreCase));
            if (colourChange is null)
            {
                continue;
            }

            return new ChangeSampleRequestColourCodeResultDto
            {
                SampleRequestId = sampleRequestId,
                ProductId = productId,
                OldColourCode = ReadJsonString(colourChange.OldValue),
                NewColourCode = ReadJsonString(colourChange.NewValue),
                ConversationId = conversationId.GetValueOrDefault(),
                MessageId = message.InternalMessageId
            };
        }

        if (TryReadAuditColourChange(audit, out var auditOldColourCode, out var auditNewColourCode))
        {
            return new ChangeSampleRequestColourCodeResultDto
            {
                SampleRequestId = sampleRequestId,
                ProductId = productId,
                OldColourCode = auditOldColourCode,
                NewColourCode = auditNewColourCode,
                ConversationId = conversationId ?? Guid.Empty
            };
        }

        return null;
    }

    private async Task<HistorySyncResult> ReplaceHistoricalColourCodeAsync(
        Guid companyId,
        IReadOnlyCollection<Guid> conversationIds,
        IReadOnlyCollection<Guid> sampleRequestIds,
        string oldColourCode,
        string newColourCode,
        Guid editedByEmployeeId,
        DateTime editedAt,
        CancellationToken cancellationToken)
    {
        var updatedMessageCount = 0;
        if (conversationIds.Count > 0)
        {
            var messages = await _dbContext.InternalMessages
                .Where(x => conversationIds.Contains(x.InternalConversationId) && !x.IsDeleted)
                .ToListAsync(cancellationToken);
            foreach (var message in messages)
            {
                if (IsColourCodeChangeAudit(message.PayloadJson))
                {
                    continue;
                }

                var body = ReplaceCode(message.Body, oldColourCode, newColourCode);
                var payloadJson = ReplaceCode(message.PayloadJson, oldColourCode, newColourCode);
                if (body == message.Body && payloadJson == message.PayloadJson)
                {
                    continue;
                }

                message.Body = body ?? string.Empty;
                message.PayloadJson = payloadJson;
                message.IsEdited = true;
                message.EditedAt = editedAt;
                message.EditedByEmployeeId = editedByEmployeeId;
                updatedMessageCount++;
            }
        }

        var notificationById = new Dictionary<Guid, HRM.Domain.Entities.Notifications.Notification>();
        var textMatches = await _notificationDbContext.Notifications
            .Where(x =>
                x.CompanyId == companyId &&
                (x.Title.Contains(oldColourCode) ||
                 x.Message.Contains(oldColourCode) ||
                 (x.Link != null && x.Link.Contains(oldColourCode))))
            .ToListAsync(cancellationToken);
        foreach (var notification in textMatches)
        {
            notificationById[notification.Id] = notification;
        }

        foreach (var sampleRequestId in sampleRequestIds)
        {
            var payloadMarker = JsonSerializer.Serialize(new { sampleRequestId });
            var payloadMatches = await _notificationDbContext.Notifications
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.PayloadJson != null &&
                    EF.Functions.JsonContains(x.PayloadJson, payloadMarker))
                .ToListAsync(cancellationToken);
            foreach (var notification in payloadMatches)
            {
                notificationById[notification.Id] = notification;
            }
        }

        var updatedNotificationCount = 0;
        foreach (var notification in notificationById.Values)
        {
            if (IsColourCodeChangeAudit(notification.PayloadJson))
            {
                continue;
            }

            var title = ReplaceCode(notification.Title, oldColourCode, newColourCode);
            var message = ReplaceCode(notification.Message, oldColourCode, newColourCode);
            var link = ReplaceCode(notification.Link, oldColourCode, newColourCode);
            var payloadJson = ReplaceCode(notification.PayloadJson, oldColourCode, newColourCode);
            if (title == notification.Title &&
                message == notification.Message &&
                link == notification.Link &&
                payloadJson == notification.PayloadJson)
            {
                continue;
            }

            notification.Title = title ?? string.Empty;
            notification.Message = message ?? string.Empty;
            notification.Link = link;
            notification.PayloadJson = payloadJson;
            updatedNotificationCount++;
        }

        return new HistorySyncResult(updatedMessageCount, updatedNotificationCount);
    }

    private static bool IsColourCodeChangeAudit(string? payloadJson)
        => DeserializePayload(payloadJson)?.DirectPatchNotification?.Changes.Any(x =>
            string.Equals(x.FieldCode, "product.colour_code", StringComparison.OrdinalIgnoreCase)) == true;

    private static SampleRequestThreadMessagePayload? DeserializePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SampleRequestThreadMessagePayload>(payloadJson, PayloadJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ReadJsonString(JsonElement value)
        => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();

    private static bool TryReadAuditColourChange(
        JsonDocument? changedValues,
        out string oldColourCode,
        out string newColourCode)
    {
        oldColourCode = string.Empty;
        newColourCode = string.Empty;
        if (changedValues is null ||
            !changedValues.RootElement.TryGetProperty("ColourCode", out var colourCodeChange) ||
            !colourCodeChange.TryGetProperty("Old", out var oldValue) ||
            !colourCodeChange.TryGetProperty("New", out var newValue))
        {
            return false;
        }

        oldColourCode = ReadJsonString(oldValue);
        newColourCode = ReadJsonString(newValue);
        return !string.IsNullOrWhiteSpace(oldColourCode) && !string.IsNullOrWhiteSpace(newColourCode);
    }

    private static string? ReplaceCode(string? value, string oldColourCode, string newColourCode)
        => string.IsNullOrEmpty(value)
            ? value
            : value.Replace(oldColourCode, newColourCode, StringComparison.OrdinalIgnoreCase);

    private sealed record HistorySyncResult(int UpdatedMessageCount, int UpdatedNotificationCount);
}
