using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;

public sealed record SampleWorkSaleGroup(Guid Id, string Name);
public sealed record SampleWorkSaleEmployee(Guid Id, string Name, Guid[] GroupIds);
public sealed record SampleWorkSaleFilters(SampleWorkSaleGroup[] Groups, SampleWorkSaleEmployee[] Employees);
public sealed record GetSampleWorkSaleFiltersQuery(SampleRequestWorkAudience Audience) : IRequest<SampleWorkSaleFilters>;

internal sealed class GetSampleWorkSaleFiltersQueryHandler(IPLMReadDbContext db, ICRMReadDbContext crm,
    ICurrentUser user, ICustomerVisibilityService visibility, Services.SampleWorkPreviewSession? preview = null)
    : IRequestHandler<GetSampleWorkSaleFiltersQuery, SampleWorkSaleFilters>
{
    public async Task<SampleWorkSaleFilters> Handle(GetSampleWorkSaleFiltersQuery request, CancellationToken ct)
    {
        using var timings = HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.SampleWorkDiagnosticTags.Start("sale_filters");
        if (!SampleRequestWorkQueueAccess.CanRead(user, request.Audience) || user.CompanyId is not { } company)
            throw new UnauthorizedAccessException("Sample work access is required.");
        var scope = await visibility.BuildReadScopeAsync(ct);
        timings?.Mark("visibility-scope");
        var visible = visibility.ApplySampleRequestVisibility(db.SampleRequests.AsNoTracking()
            .Where(x => x.IsActive && x.CompanyId == company), db.Customers.AsNoTracking(), scope)
            .Where(x => x.CustomerId != CustomerVisibilityConstants.RestrictedCustomerId &&
                x.Customer.ExternalId != PLMCustomerRules.InternalCustomerExternalId);
        if (preview?.ActorReadScope is { } actorScope)
            visible = visibility.ApplySampleRequestVisibility(visible, db.Customers.AsNoTracking(), actorScope);
        visible = SampleRequestWorkScope.Apply(visible, db.SampleRequestWorkflowEvents);
        var owners = visible.Select(x => x.ManagerBy).Distinct();
        var employees = await crm.Employees.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive && owners.Contains(x.EmployeeId))
            .OrderBy(x => x.FullName).Select(x => new { Id = x.EmployeeId, Name = x.FullName }).ToArrayAsync(ct);
        var ids = employees.Select(x => x.Id).ToArray();
        timings?.Mark("employees");
        var memberships = await crm.MemberInGroups.AsNoTracking().Where(m => m.IsActive && m.Profile.HasValue &&
            ids.Contains(m.Profile.Value) && m.Group.CompanyId == company &&
            (m.Group.GroupType == "CMR" || m.Group.GroupType != null && m.Group.GroupType.StartsWith("CMR.")))
            .Select(m => new { EmployeeId = m.Profile!.Value, m.GroupId, Name = m.Group.Name ?? m.Group.ExternalId }).Distinct().ToArrayAsync(ct);
        timings?.Mark("groups");
        return new(memberships.Select(x => new SampleWorkSaleGroup(x.GroupId, x.Name)).Distinct().OrderBy(x => x.Name).ToArray(),
            employees.Select(x => new SampleWorkSaleEmployee(x.Id, x.Name,
                memberships.Where(m => m.EmployeeId == x.Id).Select(m => m.GroupId).Distinct().ToArray())).ToArray());
    }
}
