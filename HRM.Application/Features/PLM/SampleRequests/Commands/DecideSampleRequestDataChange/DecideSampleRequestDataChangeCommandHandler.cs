using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;
using HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.DecideSampleRequestDataChange;

internal sealed class DecideSampleRequestDataChangeCommandHandler
    : IRequestHandler<DecideSampleRequestDataChangeCommand, OperationResult<SampleRequestDataChangeDecisionResultDto>>
{
    private const int MaxReasonLength = 2000;

    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ISender _sender;

    public DecideSampleRequestDataChangeCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        ISender sender)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<OperationResult<SampleRequestDataChangeDecisionResultDto>> Handle(
        DecideSampleRequestDataChangeCommand request,
        CancellationToken cancellationToken)
    {
        if (!SampleRequestDataChangeAuthorization.CanApprove(_currentUser))
        {
            return OperationResult<SampleRequestDataChangeDecisionResultDto>.Fail("Only Lab or an authorized administrator can decide this request.");
        }

        var employeeId = _currentUser.EmployeeId;
        var companyId = _currentUser.CompanyId;
        if (request.SampleRequestId == Guid.Empty || request.RequestMessageId == Guid.Empty ||
            !employeeId.HasValue || employeeId.Value == Guid.Empty ||
            !companyId.HasValue || companyId.Value == Guid.Empty)
        {
            return OperationResult<SampleRequestDataChangeDecisionResultDto>.Fail("Current user, sample request, or message is invalid.");
        }

        if (!Enum.IsDefined(request.Decision))
        {
            return OperationResult<SampleRequestDataChangeDecisionResultDto>.Fail("Decision is invalid.");
        }

        var reason = request.Reason?.Trim();
        if (reason is { Length: > MaxReasonLength })
        {
            return OperationResult<SampleRequestDataChangeDecisionResultDto>.Fail($"Reason cannot exceed {MaxReasonLength} characters.");
        }

        if (request.Decision == SampleRequestDataChangeDecision.Reject && string.IsNullOrWhiteSpace(reason))
        {
            return OperationResult<SampleRequestDataChangeDecisionResultDto>.Fail("Reason is required when rejecting changes.");
        }

        var requestMessage = await _dbContext.InternalMessages
            .Where(x =>
                x.InternalMessageId == request.RequestMessageId &&
                !x.IsDeleted &&
                x.Conversation.CompanyId == companyId.Value &&
                x.Conversation.IsActive &&
                x.Conversation.RelatedType == InternalMailRelatedType.SampleRequest &&
                x.Conversation.RelatedId == request.SampleRequestId &&
                x.Conversation.Participants.Any(participant =>
                    participant.EmployeeId == employeeId.Value && participant.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        if (requestMessage is null)
        {
            return OperationResult<SampleRequestDataChangeDecisionResultDto>.Fail("Data change request was not found or is not accessible.");
        }

        var threadPayload = DeserializePayload(requestMessage.PayloadJson);
        var dataChange = threadPayload?.DataChangeRequest;
        if (threadPayload is null ||
            !string.Equals(threadPayload.ContentType, SampleRequestDataChangePayloadTypes.Request, StringComparison.OrdinalIgnoreCase) ||
            dataChange is null ||
            dataChange.SampleRequestId != request.SampleRequestId)
        {
            return OperationResult<SampleRequestDataChangeDecisionResultDto>.Fail("Message is not a sample request data change request.");
        }

        var pendingChanges = dataChange.Changes
            .Where(SampleRequestDataChangeStatusRules.IsPending)
            .ToList();
        if (pendingChanges.Count == 0)
        {
            return OperationResult<SampleRequestDataChangeDecisionResultDto>.Fail("This request has no pending fields.");
        }

        var requestedCodes = request.FieldCodes?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        var selectedChanges = requestedCodes.Length == 0
            ? pendingChanges
            : pendingChanges
                .Where(x => requestedCodes.Contains(x.FieldCode, StringComparer.OrdinalIgnoreCase))
                .ToList();

        if (selectedChanges.Count == 0 ||
            (requestedCodes.Length > 0 && selectedChanges.Count != requestedCodes.Length))
        {
            return OperationResult<SampleRequestDataChangeDecisionResultDto>.Fail("Some selected fields do not exist or are no longer pending.");
        }

        if (request.Decision == SampleRequestDataChangeDecision.Approve)
        {
            var sampleRequest = await _dbContext.SampleRequests
                .Include(x => x.Product)
                .AsNoTracking()
                .Where(x =>
                    x.SampleRequestId == request.SampleRequestId &&
                    x.CompanyId == companyId.Value &&
                    x.IsActive)
                .FirstOrDefaultAsync(cancellationToken);

            if (sampleRequest is null ||
                !sampleRequest.Product.IsActive ||
                sampleRequest.Product.CompanyId != companyId.Value)
            {
                return OperationResult<SampleRequestDataChangeDecisionResultDto>.Fail("Sample request or product was not found.");
            }

            // Temporarily allow Lab approval to overwrite current values with the proposed values.
            // The message, permission, company and pending-field validations remain enforced.

            var patch = new PatchSampleRequestCommand
            {
                SampleRequestId = request.SampleRequestId,
                DeferSaveChanges = true
            };

            if (!SampleRequestDataChangeFieldCatalog.TryApplyToPatch(patch, selectedChanges, out var patchError))
            {
                return OperationResult<SampleRequestDataChangeDecisionResultDto>.Fail(patchError ?? "Approved values are invalid.");
            }

            var patchResult = await _sender.Send(patch, cancellationToken);
            if (!patchResult.Success)
            {
                return OperationResult<SampleRequestDataChangeDecisionResultDto>.Fail(
                    patchResult.Message ?? "Could not apply approved changes.");
            }
        }

        var now = DateTime.Now;
        var fieldStatus = request.Decision == SampleRequestDataChangeDecision.Approve
            ? SampleRequestDataChangeStatuses.Approved
            : SampleRequestDataChangeStatuses.Rejected;

        foreach (var change in selectedChanges)
        {
            change.Status = fieldStatus;
            change.DecidedByEmployeeId = employeeId.Value;
            change.DecidedAt = now;
            change.DecisionReason = reason;
        }

        requestMessage.PayloadJson = JsonSerializer.Serialize(threadPayload, PayloadJsonOptions);
        requestMessage.IsEdited = true;
        requestMessage.EditedAt = now;
        requestMessage.EditedByEmployeeId = employeeId.Value;

        var overallStatus = SampleRequestDataChangeStatusRules.GetOverallStatus(dataChange.Changes);
        var fieldLabels = string.Join(", ", selectedChanges.Select(x => x.Label));
        var approved = request.Decision == SampleRequestDataChangeDecision.Approve;
        var responseText = approved
            ? $"Đã duyệt thay đổi: {fieldLabels}."
            : $"Đã từ chối thay đổi: {fieldLabels}. Lý do: {reason}";

        OperationResult<SendInternalMessageResultDto> sendResult;
        try
        {
            sendResult = await _sender.Send(new SendSampleRequestMessageCommand
            {
                SampleRequestId = request.SampleRequestId,
                Type = SampleRequestNotificationType.GeneralMessage,
                Message = responseText,
                ReplyToMessageId = request.RequestMessageId,
                ExtraRecipientEmployeeIds = new[] { requestMessage.SenderEmployeeId },
                NotificationRecipientEmployeeIdsOverride = new[] { requestMessage.SenderEmployeeId },
                TopicOverride = approved
                    ? TopicNotifications.SampleRequestDataChangeApproved
                    : TopicNotifications.SampleRequestDataChangeRejected,
                TitleOverride = approved
                    ? "Yêu cầu cập nhật đã được duyệt"
                    : "Yêu cầu cập nhật đã bị từ chối"
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult<SampleRequestDataChangeDecisionResultDto>.Fail(
                "This request was decided by another user. Reload the conversation before continuing.");
        }

        if (!sendResult.Success || sendResult.Data is null)
        {
            return OperationResult<SampleRequestDataChangeDecisionResultDto>.Fail(
                sendResult.Message ?? "Could not send the decision response.");
        }

        return OperationResult<SampleRequestDataChangeDecisionResultDto>.Ok(
            new SampleRequestDataChangeDecisionResultDto
            {
                ConversationId = sendResult.Data.ConversationId,
                RequestMessageId = request.RequestMessageId,
                ResponseMessageId = sendResult.Data.MessageId,
                Status = overallStatus,
                ProcessedFieldCodes = selectedChanges.Select(x => x.FieldCode).ToArray()
            },
            approved ? "Approved selected changes successfully." : "Rejected selected changes successfully.");
    }

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
}
