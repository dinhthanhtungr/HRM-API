using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Commands.CreateSampleTrialInteraction;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RecordSampleTrialCustomerFeedbackInteraction;

internal sealed class RecordSampleTrialCustomerFeedbackInteractionCommandHandler
    : IRequestHandler<RecordSampleTrialCustomerFeedbackInteractionCommand, OperationResult<Guid>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ISender _sender;

    public RecordSampleTrialCustomerFeedbackInteractionCommandHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser,
        ISender sender)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async Task<OperationResult<Guid>> Handle(
        RecordSampleTrialCustomerFeedbackInteractionCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SampleRequestId == Guid.Empty ||
            command.SampleRequestSampleTrialId == Guid.Empty ||
            command.IdempotencyKey == Guid.Empty)
        {
            return OperationResult<Guid>.Fail(
                "SampleRequestId, SampleRequestSampleTrialId and IdempotencyKey are required.");
        }

        if (!_currentUser.IsInAnyRole(ApplicationRoleSets.Modules.Sales))
        {
            return OperationResult<Guid>.Fail(
                "Only Sale users are allowed to record sample-trial customer feedback.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("Current company is invalid.");
        }

        var customerId = await _dbContext.SampleRequestSampleTrials
            .AsNoTracking()
            .Where(x =>
                x.SampleRequestSampleTrialId == command.SampleRequestSampleTrialId &&
                x.SampleRequestId == command.SampleRequestId &&
                x.IsActive &&
                x.SampleRequest.IsActive &&
                x.SampleRequest.CompanyId == companyId)
            .Select(x => (Guid?)x.SampleRequest.CustomerId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!customerId.HasValue)
        {
            return OperationResult<Guid>.Fail(
                "Sample trial was not found or is outside the current company.");
        }

        return await _sender.Send(new CreateSampleTrialInteractionCommand
        {
            Request = SampleTrialCustomerFeedbackInteractionMapper.ToCrmRequest(
                command,
                customerId.Value)
        }, cancellationToken);
    }
}
