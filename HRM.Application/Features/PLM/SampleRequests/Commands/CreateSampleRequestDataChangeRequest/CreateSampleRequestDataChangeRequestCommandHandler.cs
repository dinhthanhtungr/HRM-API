using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;
using HRM.Domain.Enums.SampleRequests;
using HRM.Domain.Enums.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.CreateSampleRequestDataChangeRequest;

internal sealed class CreateSampleRequestDataChangeRequestCommandHandler
    : IRequestHandler<CreateSampleRequestDataChangeRequestCommand, OperationResult<SendInternalMessageResultDto>>
{
    private const int MaxChanges = 50;
    private const int MaxMessageLength = 2000;

    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly ISender _sender;

    public CreateSampleRequestDataChangeRequestCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        ICustomerVisibilityService visibilityService,
        ISender sender)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _visibilityService = visibilityService;
        _sender = sender;
    }

    public async Task<OperationResult<SendInternalMessageResultDto>> Handle(
        CreateSampleRequestDataChangeRequestCommand request,
        CancellationToken cancellationToken)
    {
        var employeeId = _currentUser.EmployeeId;
        var companyId = _currentUser.CompanyId;
        if (request.SampleRequestId == Guid.Empty ||
            !employeeId.HasValue || employeeId.Value == Guid.Empty ||
            !companyId.HasValue || companyId.Value == Guid.Empty)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Current user or sample request is invalid.");
        }

        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message) || message.Length > MaxMessageLength)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail($"Message is required and cannot exceed {MaxMessageLength} characters.");
        }

        if (request.Changes is null || request.Changes.Count == 0 || request.Changes.Count > MaxChanges)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail($"Changes must contain between 1 and {MaxChanges} fields.");
        }

        var duplicateCode = request.Changes
            .GroupBy(x => x.FieldCode?.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => string.IsNullOrWhiteSpace(x.Key) || x.Count() > 1);
        if (duplicateCode is not null)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Field codes must be valid and unique.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var sampleRequest = await _visibilityService.ApplySampleRequestVisibility(
                _dbContext.SampleRequests
                    .Include(x => x.Product)
                    .Where(x => x.SampleRequestId == request.SampleRequestId)
                    .AsNoTracking(),
                _dbContext.Customers.AsNoTracking(),
                scope)
            .FirstOrDefaultAsync(cancellationToken);

        if (sampleRequest is null || !sampleRequest.Product.IsActive || sampleRequest.Product.CompanyId != companyId.Value)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("Sample request or product was not found.");
        }

        var proposals = new List<SampleRequestDataChangeFieldPayload>(request.Changes.Count);
        foreach (var requestedChange in request.Changes)
        {
            if (!SampleRequestDataChangeFieldCatalog.TryCreateProposal(
                    sampleRequest,
                    requestedChange,
                    out var proposal,
                    out var error))
            {
                return OperationResult<SendInternalMessageResultDto>.Fail(error ?? "Proposed change is invalid.");
            }

            proposals.Add(proposal);
        }

        return await _sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = sampleRequest.SampleRequestId,
            Type = SampleRequestNotificationType.UpdateRequest,
            Message = message,
            IsUrgent = request.IsUrgent,
            ExtraRecipientEmployeeIds = request.RecipientEmployeeIds,
            TopicOverride = TopicNotifications.SampleRequestDataChangeRequested,
            TitleOverride = "Yêu cầu thay đổi dữ liệu phối mẫu",
            DataChangeRequest = new SampleRequestDataChangePayload
            {
                SampleRequestId = sampleRequest.SampleRequestId,
                ExternalId = sampleRequest.ExternalId,
                RequestedByEmployeeId = employeeId.Value,
                RequestedAt = DateTime.Now,
                BaseUpdatedDate = sampleRequest.UpdatedDate,
                Changes = proposals
            }
        }, cancellationToken);
    }
}
