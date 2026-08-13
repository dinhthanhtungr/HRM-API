using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;
using HRM.Application.Features.PLM.SampleRequests.SampleReceiptConfirmations;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.InternalMailEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.ConfirmSampleRequestSampleReceipt;

internal sealed class ConfirmSampleRequestSampleReceiptCommandHandler
    : IRequestHandler<ConfirmSampleRequestSampleReceiptCommand, OperationResult<SampleReceiptConfirmationDto>>
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ConfirmSampleRequestSampleReceiptCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        ICustomerVisibilityService visibilityService,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _visibilityService = visibilityService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<SampleReceiptConfirmationDto>> Handle(
        ConfirmSampleRequestSampleReceiptCommand request,
        CancellationToken cancellationToken)
    {
        if (request.SampleRequestId == Guid.Empty ||
            request.SampleRequestSampleTrialId == Guid.Empty ||
            request.MessageId == Guid.Empty)
        {
            return OperationResult<SampleReceiptConfirmationDto>.Fail(
                "SampleRequestId, SampleRequestSampleTrialId or MessageId is invalid.");
        }

        if (!_currentUser.IsInAnyRole(ApplicationRoleSets.PLM.FormulaSelectors))
        {
            return OperationResult<SampleReceiptConfirmationDto>.Fail(
                "You are not allowed to confirm sample receipt.");
        }

        if (_currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<SampleReceiptConfirmationDto>.Fail("Current employee is invalid.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visibleSampleRequests = _visibilityService.ApplySampleRequestVisibility(
            _dbContext.SampleRequests.AsQueryable(),
            _dbContext.Customers.AsNoTracking(),
            scope);

        var sampleRequestExists = await visibleSampleRequests.AnyAsync(
            x => x.SampleRequestId == request.SampleRequestId,
            cancellationToken);
        if (!sampleRequestExists)
        {
            return OperationResult<SampleReceiptConfirmationDto>.Fail(
                "Sample request was not found or is outside your scope.");
        }

        var trial = await _dbContext.SampleRequestSampleTrials
            .AsTracking()
            .FirstOrDefaultAsync(x =>
                x.SampleRequestSampleTrialId == request.SampleRequestSampleTrialId &&
                x.SampleRequestId == request.SampleRequestId &&
                x.IsActive,
                cancellationToken);
        if (trial is null)
        {
            return OperationResult<SampleReceiptConfirmationDto>.Fail(
                "Sample trial was not found or does not belong to this sample request.");
        }

        var message = await _dbContext.InternalMessages
            .Include(x => x.Conversation)
            .AsTracking()
            .FirstOrDefaultAsync(x =>
                x.InternalMessageId == request.MessageId &&
                !x.IsDeleted &&
                x.Conversation.CompanyId == scope.CompanyId &&
                x.Conversation.IsActive &&
                x.Conversation.RelatedType == InternalMailRelatedType.SampleRequest &&
                x.Conversation.RelatedId == request.SampleRequestId &&
                x.Conversation.Participants.Any(participant =>
                    participant.EmployeeId == employeeId && participant.IsActive),
                cancellationToken);
        if (message is null)
        {
            return OperationResult<SampleReceiptConfirmationDto>.Fail(
                "The sample-sent message was not found in this sample request conversation.");
        }

        var payload = DeserializePayload(message.PayloadJson);
        if (payload?.SampleReceiptAction is not { } receiptAction ||
            payload.SampleRequestId != request.SampleRequestId ||
            receiptAction.SampleRequestSampleTrialId != request.SampleRequestSampleTrialId)
        {
            return OperationResult<SampleReceiptConfirmationDto>.Fail(
                "Message does not contain the matching sample receipt action.");
        }

        var currentEmployeeName = await _dbContext.Employees
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.CompanyId == scope.CompanyId && x.IsActive)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(cancellationToken);
        if (currentEmployeeName is null)
        {
            return OperationResult<SampleReceiptConfirmationDto>.Fail(
                "Current employee was not found in this company.");
        }

        if (IsConfirmed(trial))
        {
            var originalReceiverName = await _dbContext.Employees
                .AsNoTracking()
                .Where(x =>
                    x.EmployeeId == trial.SampleReceivedByEmployeeId!.Value &&
                    x.CompanyId == scope.CompanyId)
                .Select(x => x.FullName)
                .FirstOrDefaultAsync(cancellationToken);
            originalReceiverName ??= receiptAction.SampleReceivedByName;

            var payloadChanged = SynchronizePayload(message, payload, trial, originalReceiverName);
            if (payloadChanged)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return OperationResult<SampleReceiptConfirmationDto>.Ok(
                ToDto(request.MessageId, trial, originalReceiverName),
                "Sample receipt was already confirmed.");
        }

        if (request.ExpectedUpdatedDate.HasValue && trial.UpdatedDate != request.ExpectedUpdatedDate.Value)
        {
            return OperationResult<SampleReceiptConfirmationDto>.Fail(
                "Sample trial was changed by another user. Reload before confirming receipt.");
        }

        var now = _dateTimeProvider.Now;
        var receivedDate = SampleReceiptConfirmationRules.ResolveReceivedDate(
            request.SampleReceivedDate,
            now);
        var validationError = SampleReceiptConfirmationRules.Validate(trial.Status, receivedDate, now);
        if (validationError is not null)
        {
            return OperationResult<SampleReceiptConfirmationDto>.Fail(validationError);
        }

        trial.SampleReceivedDate = receivedDate;
        trial.SampleReceivedByEmployeeId = employeeId;
        trial.SampleReceiptConfirmedAt = now;
        trial.UpdatedBy = employeeId;
        trial.UpdatedDate = now;

        ApplyConfirmedPayload(message, payload, trial, currentEmployeeName, now, employeeId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<SampleReceiptConfirmationDto>.Ok(
            ToDto(request.MessageId, trial, currentEmployeeName),
            "Confirmed sample receipt successfully.");
    }

    private static SampleRequestThreadMessagePayload? DeserializePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SampleRequestThreadMessagePayload>(
                payloadJson,
                PayloadJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsConfirmed(SampleRequestSampleTrial trial)
        => trial.SampleReceivedDate.HasValue &&
           trial.SampleReceivedByEmployeeId.HasValue &&
           trial.SampleReceiptConfirmedAt.HasValue;

    private static bool SynchronizePayload(
        InternalMessage message,
        SampleRequestThreadMessagePayload payload,
        SampleRequestSampleTrial trial,
        string? receivedByName)
    {
        var action = payload.SampleReceiptAction!;
        if (string.Equals(action.Status, SampleReceiptActionStatuses.Confirmed, StringComparison.Ordinal) &&
            action.SampleReceivedDate == trial.SampleReceivedDate &&
            action.SampleReceivedByEmployeeId == trial.SampleReceivedByEmployeeId &&
            action.SampleReceiptConfirmedAt == trial.SampleReceiptConfirmedAt &&
            string.Equals(action.SampleReceivedByName, receivedByName, StringComparison.Ordinal))
        {
            return false;
        }

        action.Status = SampleReceiptActionStatuses.Confirmed;
        action.SampleReceivedDate = trial.SampleReceivedDate;
        action.SampleReceivedByEmployeeId = trial.SampleReceivedByEmployeeId;
        action.SampleReceivedByName = receivedByName;
        action.SampleReceiptConfirmedAt = trial.SampleReceiptConfirmedAt;
        message.PayloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions);
        return true;
    }

    private static void ApplyConfirmedPayload(
        InternalMessage message,
        SampleRequestThreadMessagePayload payload,
        SampleRequestSampleTrial trial,
        string? receivedByName,
        DateTime now,
        Guid employeeId)
    {
        var action = payload.SampleReceiptAction!;
        action.Status = SampleReceiptActionStatuses.Confirmed;
        action.SampleReceivedDate = trial.SampleReceivedDate;
        action.SampleReceivedByEmployeeId = trial.SampleReceivedByEmployeeId;
        action.SampleReceivedByName = receivedByName;
        action.SampleReceiptConfirmedAt = trial.SampleReceiptConfirmedAt;

        message.PayloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions);
        message.IsEdited = true;
        message.EditedAt = now;
        message.EditedByEmployeeId = employeeId;
    }

    private static SampleReceiptConfirmationDto ToDto(
        Guid messageId,
        SampleRequestSampleTrial trial,
        string? receivedByName)
        => new()
        {
            SampleRequestId = trial.SampleRequestId,
            SampleRequestSampleTrialId = trial.SampleRequestSampleTrialId,
            MessageId = messageId,
            Status = SampleReceiptActionStatuses.Confirmed,
            SampleReceivedDate = trial.SampleReceivedDate!.Value,
            SampleReceivedByEmployeeId = trial.SampleReceivedByEmployeeId!.Value,
            SampleReceivedByName = receivedByName,
            SampleReceiptConfirmedAt = trial.SampleReceiptConfirmedAt!.Value,
            UpdatedDate = trial.UpdatedDate ?? trial.SampleReceiptConfirmedAt.Value
        };
}
