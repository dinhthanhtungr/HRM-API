using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.Dtos.InternalMail;
using HRM.Application.Features.PLM.SampleRequests.FormulaChangeRequests;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestFormulaChangeRequest;

internal sealed class CreateSampleRequestFormulaChangeRequestCommandHandler
    : IRequestHandler<CreateSampleRequestFormulaChangeRequestCommand, OperationResult<SendInternalMessageResultDto>>
{
    private const int MaxMessageLength = 2000;
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ISender _sender;

    public CreateSampleRequestFormulaChangeRequestCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        ISender sender)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<OperationResult<SendInternalMessageResultDto>> Handle(
        CreateSampleRequestFormulaChangeRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!SampleRequestFormulaChangeAuthorization.CanRequest(_currentUser))
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("You are not allowed to request formula changes.");
        }

        var employeeId = _currentUser.EmployeeId;
        var companyId = _currentUser.CompanyId;
        if (request.SampleRequestId == Guid.Empty ||
            request.RequestedFormulaId == Guid.Empty ||
            !employeeId.HasValue || employeeId.Value == Guid.Empty ||
            !companyId.HasValue || companyId.Value == Guid.Empty)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Current user, sample request, or formula is invalid.");
        }

        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message) || message.Length > MaxMessageLength)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail($"Message is required and cannot exceed {MaxMessageLength} characters.");
        }

        var sampleRequest = await _dbContext.SampleRequests
            .Include(x => x.Formula)
            .Where(x =>
                x.SampleRequestId == request.SampleRequestId &&
                x.CompanyId == companyId.Value &&
                x.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (sampleRequest is null)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Sample request was not found.");
        }

        if (!IsStatus(sampleRequest.Status, SampleRequestStatus.Completed))
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Formula update can only be requested after the sample request is completed.");
        }

        if (!sampleRequest.FormulaId.HasValue || sampleRequest.FormulaId.Value == Guid.Empty || sampleRequest.Formula is null)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Sample request does not have a completed formula.");
        }

        if (sampleRequest.FormulaId.Value == request.RequestedFormulaId)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Requested formula must be different from the current formula.");
        }

        var requestedFormula = await _dbContext.Formulas
            .FirstOrDefaultAsync(x =>
                x.FormulaId == request.RequestedFormulaId &&
                x.ProductId == sampleRequest.ProductId &&
                x.CompanyId == companyId.Value &&
                x.IsActive,
                cancellationToken);

        if (requestedFormula is null)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Requested formula was not found or does not belong to this sample request product.");
        }

        if (IsFormulaStatus(requestedFormula.Status, FormulaStatus.Completed) ||
            IsFormulaStatus(requestedFormula.Status, FormulaStatus.Cancelled) ||
            IsFormulaStatus(requestedFormula.Status, FormulaStatus.Rejected))
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Requested formula status cannot be used for a new update request.");
        }

        if (await HasPendingFormulaChangeRequestAsync(sampleRequest.SampleRequestId, companyId.Value, cancellationToken))
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("This sample request already has a pending formula update request.");
        }

        var now = DateTime.Now;
        sampleRequest.Status = SampleRequestStatus.FormulaUpdateRequested.ToString();
        sampleRequest.UpdatedBy = employeeId.Value;
        sampleRequest.UpdatedDate = now;

        requestedFormula.Status = FormulaStatus.PendingSaleConfirmation.ToString();
        requestedFormula.UpdatedBy = employeeId.Value;
        requestedFormula.UpdatedDate = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await _sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = sampleRequest.SampleRequestId,
            Type = SampleRequestNotificationType.UpdateRequest,
            Message = message,
            IsUrgent = request.IsUrgent,
            ExtraRecipientEmployeeIds = request.RecipientEmployeeIds,
            TopicOverride = TopicNotifications.SampleRequestFormulaUpdateRequested,
            TitleOverride = "Lab yêu cầu cập nhật công thức",
            FormulaChangeRequest = new SampleRequestFormulaChangePayload
            {
                SampleRequestId = sampleRequest.SampleRequestId,
                ExternalId = sampleRequest.ExternalId,
                CurrentFormulaId = sampleRequest.FormulaId.Value,
                CurrentFormulaExternalId = sampleRequest.Formula.ExternalId,
                RequestedFormulaId = requestedFormula.FormulaId,
                RequestedFormulaExternalId = requestedFormula.ExternalId,
                RequestedByEmployeeId = employeeId.Value,
                RequestedAt = now
            }
        }, cancellationToken);
    }

    private async Task<bool> HasPendingFormulaChangeRequestAsync(
        Guid sampleRequestId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var payloads = await _dbContext.InternalMessages
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.Conversation.CompanyId == companyId &&
                x.Conversation.IsActive &&
                x.Conversation.RelatedType == InternalMailRelatedType.SampleRequest &&
                x.Conversation.RelatedId == sampleRequestId)
            .Select(x => x.PayloadJson)
            .ToListAsync(cancellationToken);

        return payloads.Any(payloadJson =>
        {
            var payload = DeserializePayload(payloadJson);
            return payload?.FormulaChangeRequest is { } formulaChange &&
                   string.Equals(
                       formulaChange.Status,
                       SampleRequestFormulaChangeStatuses.Pending,
                       StringComparison.OrdinalIgnoreCase);
        });
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

    private static bool IsFormulaStatus(string? currentStatus, FormulaStatus expectedStatus)
        => string.Equals(currentStatus?.Trim(), expectedStatus.ToString(), StringComparison.OrdinalIgnoreCase);
}
