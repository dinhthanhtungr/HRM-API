using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;
using HRM.Application.Features.PLM.SampleRequests.SampleReceiptConfirmations;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.SampleRequests;
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

        if (!SampleReceiptConfirmationRules.CanConfirm(_currentUser))
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

        if (IsConfirmed(receiptAction))
        {
            return OperationResult<SampleReceiptConfirmationDto>.Ok(
                ToDto(request.MessageId, trial, receiptAction),
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

        trial.RequestReceivedDate = receivedDate;
        trial.Status = SampleTrialStatus.WaitingCustomerFeedback;
        trial.UpdatedBy = employeeId;
        trial.UpdatedDate = now;

        ApplyConfirmedPayload(message, payload, trial, currentEmployeeName, now, employeeId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<SampleReceiptConfirmationDto>.Ok(
            ToDto(request.MessageId, trial, payload.SampleReceiptAction!),
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

    private static bool IsConfirmed(SampleReceiptActionPayload action)
        => string.Equals(
               action.Status,
               SampleReceiptActionStatuses.Confirmed,
               StringComparison.OrdinalIgnoreCase) &&
           action.SampleReceivedDate.HasValue &&
           action.SampleReceivedByEmployeeId.HasValue &&
           action.SampleReceiptConfirmedAt.HasValue;

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
        action.SampleReceivedDate = trial.RequestReceivedDate;
        action.SampleReceivedByEmployeeId = employeeId;
        action.SampleReceivedByName = receivedByName;
        action.SampleReceiptConfirmedAt = now;

        message.PayloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions);
        message.IsEdited = true;
        message.EditedAt = now;
        message.EditedByEmployeeId = employeeId;
    }

    private static SampleReceiptConfirmationDto ToDto(
        Guid messageId,
        SampleRequestSampleTrial trial,
        SampleReceiptActionPayload action)
        => new()
        {
            SampleRequestId = trial.SampleRequestId,
            SampleRequestSampleTrialId = trial.SampleRequestSampleTrialId,
            MessageId = messageId,
            Status = SampleReceiptActionStatuses.Confirmed,
            SampleReceivedDate = action.SampleReceivedDate!.Value,
            SampleReceivedByEmployeeId = action.SampleReceivedByEmployeeId!.Value,
            SampleReceivedByName = action.SampleReceivedByName,
            SampleReceiptConfirmedAt = action.SampleReceiptConfirmedAt!.Value,
            UpdatedDate = trial.UpdatedDate ?? action.SampleReceiptConfirmedAt.Value
        };
}
