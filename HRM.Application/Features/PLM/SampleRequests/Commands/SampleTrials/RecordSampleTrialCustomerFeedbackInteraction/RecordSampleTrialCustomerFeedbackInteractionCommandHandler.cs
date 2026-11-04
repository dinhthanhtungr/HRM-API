using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Rules;
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
    private readonly ICurrentUserPermissionService _permissions;
    private readonly ICustomerVisibilityService _visibility;

    public RecordSampleTrialCustomerFeedbackInteractionCommandHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser,
        ISender sender, ICurrentUserPermissionService permissions, ICustomerVisibilityService visibility)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _sender = sender;
        _permissions = permissions;
        _visibility = visibility;
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

        if (!_currentUser.IsAuthenticated || !_permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleSaleWorkflow))
        {
            return OperationResult<Guid>.Fail(
                "Only Sale users are allowed to record sample-trial customer feedback.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("Current company is invalid.");
        }

        if (_currentUser.EmployeeId is not { } employee || !await _dbContext.Employees.AsNoTracking().AnyAsync(x =>
                x.EmployeeId == employee && x.CompanyId == companyId && x.IsActive, cancellationToken))
            return OperationResult<Guid>.Fail("Current employee is not active in this company.");
        var scope = await _visibility.BuildScopeAsync(cancellationToken);
        var allowedOwners = SampleSaleWorkflowAccess.GetAllowedOwnerIds(_currentUser, _permissions, scope);
        var parent = await _visibility.ApplySampleRequestVisibility(_dbContext.SampleRequests.AsNoTracking(), _dbContext.Customers.AsNoTracking(), scope)
            .Where(x => x.SampleRequestId == command.SampleRequestId && x.CompanyId == companyId && x.IsActive && allowedOwners.Contains(x.ManagerBy))
            .Select(x => new { x.CustomerId }).SingleOrDefaultAsync(cancellationToken);
        if (parent is null || !await _dbContext.SampleRequestSampleTrials.AsNoTracking().AnyAsync(x =>
                x.SampleRequestSampleTrialId == command.SampleRequestSampleTrialId && x.SampleRequestId == command.SampleRequestId && x.IsActive, cancellationToken))
            return OperationResult<Guid>.Fail("Sample trial was not found or is outside your managed Sale scope.");

        return await _sender.Send(new CreateSampleTrialInteractionCommand
        {
            Request = SampleTrialCustomerFeedbackInteractionMapper.ToCrmRequest(
                command,
                parent.CustomerId)
        }, cancellationToken);
    }
}
