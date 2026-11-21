using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Application.Features.PLM.SampleRequests.Services.Workflow;
using HRM.Application.Features.PLM.Shared.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetLabNotes;

internal sealed class GetLabNotesQueryHandler(IPLMReadDbContext db, ICurrentUser user,
    ICurrentUserPermissionService permissions, ICustomerVisibilityService visibility) : IRequestHandler<GetLabNotesQuery, LabNotesPage?>
{
    public async Task<LabNotesPage?> Handle(GetLabNotesQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty) return null;
        var scope = await visibility.BuildReadScopeAsync(ct);
        scope = await SampleWorkPlanningVisibility.ForPlanningAsync(scope, user, db.Employees, ct);
        var parent = await visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking(), db.Customers.AsNoTracking(), scope)
            .Where(x => x.SampleRequestId == request.SampleRequestId && x.CompanyId == company && x.IsActive &&
                x.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId && x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId)
            .Select(x => new { x.SampleRequestId, x.Status, x.UpdatedDate }).SingleOrDefaultAsync(ct);
        if (parent is null) return null;
        var canWrite = SampleFlowRules.IsOpen(parent.Status) && permissions.HasPermission(ApplicationPermissions.PLM.ManageSampleLabWorkflow) &&
            user.EmployeeId is { } actor && await db.Employees.AsNoTracking().AnyAsync(x => x.EmployeeId == actor && x.CompanyId == company && x.IsActive, ct);
        var rows = await db.SampleRequestWorkflowEvents.AsNoTracking().Where(x => x.CompanyId == company &&
                x.SampleRequestId == parent.SampleRequestId && x.EventType == SampleRequestWorkflowEvents.LabNote)
            .OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id)
            .Skip((Math.Clamp(request.PageNumber, 1, 1000000) - 1) * 20).Take(21)
            .Select(x => new LabNoteDto(x.Id, x.Note ?? "", x.RecordedAt,
                db.Employees.Where(e => e.CompanyId == company && e.EmployeeId == x.RecordedByEmployeeId).Select(e => e.FullName).FirstOrDefault()))
            .ToListAsync(ct);
        return new(rows.Take(20).ToArray(), rows.Count > 20, canWrite, parent.UpdatedDate);
    }
}
