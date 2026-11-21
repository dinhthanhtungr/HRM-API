using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.Shared.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.SetLegacyWorkTracking;

internal sealed class SetLegacyWorkTrackingCommandHandler(IPLMWriteDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ICustomerVisibilityService visibility, IDateTimeProvider clock)
    : IRequestHandler<SetLegacyWorkTrackingCommand, OperationResult<Guid>>
{
    public async Task<OperationResult<Guid>> Handle(SetLegacyWorkTrackingCommand request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || !permissions.HasPermission(ApplicationPermissions.PLM.ManageLegacySampleWork) ||
            user.CompanyId is not { } company || company == Guid.Empty ||
            user.EmployeeId is not { } employee || employee == Guid.Empty)
            return OperationResult<Guid>.Fail("You cannot manage legacy sample work.");
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == employee && x.CompanyId == company && x.IsActive, ct))
            return OperationResult<Guid>.Fail("Current employee is not active in this company.");

        var scope = await visibility.BuildScopeAsync(ct);
        var eligible = visibility.ApplySampleRequestVisibility(db.SampleRequests, db.Customers, scope)
            .Where(x => x.SampleRequestId == request.SampleRequestId && x.CompanyId == company && x.IsActive &&
                x.CreatedDate < SampleRequestWorkScope.StartedAt &&
                !x.WorkflowEvents.Any(e => e.CompanyId == x.CompanyId && e.EventType == Services.Workflow.SampleFollowUpRules.Started) && x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId);
        var record = await eligible.AsNoTracking().SingleOrDefaultAsync(ct);
        if (record is null) return OperationResult<Guid>.Fail("Legacy sample request is outside your scope.");
        if (record.UpdatedDate != request.ExpectedUpdatedDate)
            return OperationResult<Guid>.Fail("Sample request changed. Reload before saving.");

        await using var transaction = await db.BeginTransactionAsync(ct);
        var now = clock.Now;
        // Compare-and-swap trên hồ sơ cha để các lựa chọn đồng thời không ghi đè âm thầm.
        var changed = await eligible.Where(x => x.UpdatedDate == request.ExpectedUpdatedDate)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UpdatedDate, now)
                .SetProperty(x => x.UpdatedBy, employee), ct);
        if (changed != 1) return OperationResult<Guid>.Fail("Sample request changed. Reload before saving.");
        db.SampleRequestWorkflowEvents.Add(SampleRequestWorkflowEvents.Create(record.SampleRequestId, company,
            employee, now, request.Included ? SampleRequestWorkScope.LegacyIncluded : SampleRequestWorkScope.LegacyExcluded));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return OperationResult<Guid>.Ok(record.SampleRequestId);
    }
}
