using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Domain.Enums.WorkTaskEnums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.ArchiveCustomerWorkPlan;

internal sealed class ArchiveCustomerWorkPlanCommandHandler
    : IRequestHandler<ArchiveCustomerWorkPlanCommand, OperationResult>
{
    private readonly ICRMWriteDbContext _writeDbContext;
    private readonly CustomerCrmAccessService _accessService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ArchiveCustomerWorkPlanCommandHandler(
        ICRMWriteDbContext writeDbContext,
        CustomerCrmAccessService accessService,
        IDateTimeProvider dateTimeProvider)
    {
        _writeDbContext = writeDbContext;
        _accessService = accessService;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Archive mềm WorkPlan, giữ nguyên reference và lịch sử người phụ trách.
    /// </summary>
    public async Task<OperationResult> Handle(ArchiveCustomerWorkPlanCommand request, CancellationToken cancellationToken)
    {
        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var visibleIds = _accessService.VisibleCustomers(scope).Select(x => x.CustomerId);

        var plan = await _writeDbContext.WorkPlans.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == request.PlanId &&
            x.CompanyId == scope.CompanyId &&
            x.References.Any(r => r.ReferenceType == WorkReferenceType.Customer && visibleIds.Contains(r.ReferenceId)), cancellationToken);

        if (plan is null)
        {
            return OperationResult.Fail("Work plan was not found.");
        }

        var affected = await _writeDbContext.WorkPlans
            .Where(x => x.Id == request.PlanId && x.CompanyId == scope.CompanyId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.UpdatedDate, _dateTimeProvider.Now)
                .SetProperty(x => x.UpdatedBy, scope.EmployeeId),
                cancellationToken);

        return affected > 0
            ? OperationResult.Ok()
            : OperationResult.Fail("Work plan was already archived or no change was saved.");
    }
}
