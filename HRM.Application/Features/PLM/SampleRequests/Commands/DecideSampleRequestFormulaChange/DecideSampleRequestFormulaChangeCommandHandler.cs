using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;
using HRM.Application.Features.PLM.SampleRequests.FormulaChangeRequests;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.DecideSampleRequestFormulaChange;

internal sealed class DecideSampleRequestFormulaChangeCommandHandler
    : IRequestHandler<DecideSampleRequestFormulaChangeCommand, OperationResult<SampleRequestFormulaChangeDecisionResultDto>>
{
    private const int MaxReasonLength = 2000;
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ISender _sender;

    public DecideSampleRequestFormulaChangeCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        ISender sender)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<OperationResult<SampleRequestFormulaChangeDecisionResultDto>> Handle(
        DecideSampleRequestFormulaChangeCommand request,
        CancellationToken cancellationToken)
    {
        var employeeId = _currentUser.EmployeeId;
        var companyId = _currentUser.CompanyId;
        if (request.SampleRequestId == Guid.Empty ||
            request.RequestMessageId == Guid.Empty ||
            !employeeId.HasValue || employeeId.Value == Guid.Empty ||
            !companyId.HasValue || companyId.Value == Guid.Empty)
        {
            return OperationResult<SampleRequestFormulaChangeDecisionResultDto>.Fail("Current user, sample request, or message is invalid.");
        }

        if (!Enum.IsDefined(request.Decision))
        {
            return OperationResult<SampleRequestFormulaChangeDecisionResultDto>.Fail("Decision is invalid.");
        }

        var reason = request.Reason?.Trim();
        if (reason is { Length: > MaxReasonLength })
        {
            return OperationResult<SampleRequestFormulaChangeDecisionResultDto>.Fail($"Reason cannot exceed {MaxReasonLength} characters.");
        }

        if (request.Decision is SampleRequestFormulaChangeDecision.Reject or SampleRequestFormulaChangeDecision.Cancel &&
            string.IsNullOrWhiteSpace(reason))
        {
            return OperationResult<SampleRequestFormulaChangeDecisionResultDto>.Fail("Reason is required when rejecting or cancelling a formula update request.");
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
            return OperationResult<SampleRequestFormulaChangeDecisionResultDto>.Fail("Formula update request was not found or is not accessible.");
        }

        var threadPayload = DeserializePayload(requestMessage.PayloadJson);
        var formulaChange = threadPayload?.FormulaChangeRequest;
        if (threadPayload is null ||
            !string.Equals(threadPayload.ContentType, SampleRequestFormulaChangePayloadTypes.Request, StringComparison.OrdinalIgnoreCase) ||
            formulaChange is null ||
            formulaChange.SampleRequestId != request.SampleRequestId)
        {
            return OperationResult<SampleRequestFormulaChangeDecisionResultDto>.Fail("Message is not a sample request formula update request.");
        }

        if (!string.Equals(formulaChange.Status, SampleRequestFormulaChangeStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<SampleRequestFormulaChangeDecisionResultDto>.Fail("This formula update request is no longer pending.");
        }

        if (request.Decision is SampleRequestFormulaChangeDecision.Approve or SampleRequestFormulaChangeDecision.Reject &&
            !SampleRequestFormulaChangeAuthorization.CanApproveOrReject(_currentUser))
        {
            return OperationResult<SampleRequestFormulaChangeDecisionResultDto>.Fail("You are not allowed to decide formula update requests.");
        }

        if (request.Decision == SampleRequestFormulaChangeDecision.Cancel &&
            !SampleRequestFormulaChangeAuthorization.CanCancel(_currentUser, employeeId.Value, formulaChange.RequestedByEmployeeId))
        {
            return OperationResult<SampleRequestFormulaChangeDecisionResultDto>.Fail("You are not allowed to cancel this formula update request.");
        }

        var sampleRequest = await _dbContext.SampleRequests
            .Where(x =>
                x.SampleRequestId == request.SampleRequestId &&
                x.CompanyId == companyId.Value &&
                x.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (sampleRequest is null)
        {
            return OperationResult<SampleRequestFormulaChangeDecisionResultDto>.Fail("Sample request was not found.");
        }

        var formulas = await _dbContext.Formulas
            .Where(x =>
                x.ProductId == sampleRequest.ProductId &&
                x.CompanyId == companyId.Value &&
                x.IsActive &&
                (x.FormulaId == formulaChange.CurrentFormulaId || x.FormulaId == formulaChange.RequestedFormulaId))
            .ToListAsync(cancellationToken);

        var requestedFormula = formulas.FirstOrDefault(x => x.FormulaId == formulaChange.RequestedFormulaId);
        if (requestedFormula is null)
        {
            return OperationResult<SampleRequestFormulaChangeDecisionResultDto>.Fail("Requested formula was not found.");
        }

        var now = DateTime.Now;
        var oldStatus = sampleRequest.Status;
        var approved = request.Decision == SampleRequestFormulaChangeDecision.Approve;
        var cancelled = request.Decision == SampleRequestFormulaChangeDecision.Cancel;

        if (approved)
        {
            if (!IsStatus(sampleRequest.Status, SampleRequestStatus.FormulaUpdateRequested))
            {
                return OperationResult<SampleRequestFormulaChangeDecisionResultDto>.Fail("Sample request is not waiting for formula update confirmation.");
            }

            if (sampleRequest.FormulaId != formulaChange.CurrentFormulaId)
            {
                return OperationResult<SampleRequestFormulaChangeDecisionResultDto>.Fail("Current formula has changed. Reload before approving this request.");
            }

            sampleRequest.FormulaId = formulaChange.RequestedFormulaId;
            SampleRequestStatusTransitionRules.MarkFormulaUpdateDecided(sampleRequest);

            foreach (var formula in formulas)
            {
                formula.IsSelect = formula.FormulaId == formulaChange.RequestedFormulaId;
            }

            requestedFormula.Status = FormulaStatus.Completed.ToString();
        }
        else
        {
            SampleRequestStatusTransitionRules.MarkFormulaUpdateDecided(sampleRequest);
            requestedFormula.Status = cancelled
                ? FormulaStatus.Cancelled.ToString()
                : FormulaStatus.Rejected.ToString();
        }

        sampleRequest.UpdatedBy = employeeId.Value;
        sampleRequest.UpdatedDate = now;
        requestedFormula.UpdatedBy = employeeId.Value;
        requestedFormula.UpdatedDate = now;

        formulaChange.Status = approved
            ? SampleRequestFormulaChangeStatuses.Approved
            : cancelled
                ? SampleRequestFormulaChangeStatuses.Cancelled
                : SampleRequestFormulaChangeStatuses.Rejected;
        formulaChange.DecidedByEmployeeId = employeeId.Value;
        formulaChange.DecidedAt = now;
        formulaChange.DecisionReason = reason;

        requestMessage.PayloadJson = JsonSerializer.Serialize(threadPayload, PayloadJsonOptions);
        requestMessage.IsEdited = true;
        requestMessage.EditedAt = now;
        requestMessage.EditedByEmployeeId = employeeId.Value;

        await SampleRequestDataChangeAuditHelper.AddStatusTransitionAuditIfChangedAsync(
            _dbContext.AuditLogs,
            sampleRequest,
            oldStatus,
            employeeId.Value,
            now,
            approved
                ? "FormulaUpdateApproved"
                : cancelled
                    ? "FormulaUpdateCancelled"
                    : "FormulaUpdateRejected",
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var responseText = approved
            ? $"Sale đã chấp nhận đổi công thức cho yêu cầu phối mẫu {formulaChange.ExternalId}. Công thức mới: {formulaChange.RequestedFormulaExternalId}."
            : cancelled
                ? $"Yêu cầu cập nhật công thức cho {formulaChange.ExternalId} đã bị hủy. Lý do: {reason}"
                : $"Sale đã từ chối đổi công thức cho yêu cầu phối mẫu {formulaChange.ExternalId}. Lý do: {reason}";

        var topic = approved
            ? TopicNotifications.SampleRequestFormulaUpdateApproved
            : cancelled
                ? TopicNotifications.SampleRequestFormulaUpdateCancelled
                : TopicNotifications.SampleRequestFormulaUpdateRejected;

        var title = approved
            ? "Yêu cầu cập nhật công thức đã được chấp nhận"
            : cancelled
                ? "Yêu cầu cập nhật công thức đã bị hủy"
                : "Yêu cầu cập nhật công thức đã bị từ chối";

        var sendResult = await _sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = request.SampleRequestId,
            Type = SampleRequestNotificationType.GeneralMessage,
            Message = responseText,
            ReplyToMessageId = request.RequestMessageId,
            ExtraRecipientEmployeeIds = new[] { formulaChange.RequestedByEmployeeId },
            NotificationRecipientEmployeeIdsOverride = new[] { formulaChange.RequestedByEmployeeId },
            TopicOverride = topic,
            TitleOverride = title
        }, cancellationToken);

        if (!sendResult.Success || sendResult.Data is null)
        {
            return OperationResult<SampleRequestFormulaChangeDecisionResultDto>.Fail(
                sendResult.Message ?? "Could not send the formula update decision response.");
        }

        return OperationResult<SampleRequestFormulaChangeDecisionResultDto>.Ok(
            new SampleRequestFormulaChangeDecisionResultDto
            {
                ConversationId = sendResult.Data.ConversationId,
                RequestMessageId = request.RequestMessageId,
                ResponseMessageId = sendResult.Data.MessageId,
                Status = formulaChange.Status,
                SampleRequestId = request.SampleRequestId,
                CurrentFormulaId = formulaChange.CurrentFormulaId,
                RequestedFormulaId = formulaChange.RequestedFormulaId
            },
            "Decided formula update request successfully.");
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

    private static bool IsStatus(string? currentStatus, SampleRequestStatus expectedStatus)
        => string.Equals(currentStatus?.Trim(), expectedStatus.ToString(), StringComparison.OrdinalIgnoreCase);
}
