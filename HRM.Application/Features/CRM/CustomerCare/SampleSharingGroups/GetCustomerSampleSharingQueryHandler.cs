using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;

internal sealed class GetCustomerSampleSharingQueryHandler(ICustomerSampleSharingDbContext db,
    ICurrentUser user, ICurrentUserPermissionService permissions, IOptions<CustomerSampleSharingOptions> options,
    CustomerSampleSharingService sharing)
    : IRequestHandler<GetCustomerSampleSharingQuery, OperationResult<SampleSharingWorkspaceDto>>
{
    public async Task<OperationResult<SampleSharingWorkspaceDto>> Handle(GetCustomerSampleSharingQuery request, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty || user.EmployeeId is null || user.EmployeeId == Guid.Empty)
            return OperationResult<SampleSharingWorkspaceDto>.Fail("Không có thông tin công ty/nhân viên hợp lệ.");
        var canManage = permissions.HasPermission(ApplicationPermissions.CRM.ManageCustomerSampleSharing);
        if (!options.Value.Enabled || !canManage)
            return OperationResult<SampleSharingWorkspaceDto>.Ok(new SampleSharingWorkspaceDto(options.Value.Enabled, false, []));
        var companyManager = permissions.HasPermission(ApplicationPermissions.CRM.AdminCustomerSampleSharing);
        var manageable = await sharing.ManageableCustomerIdsAsync(company, companyManager, ct);
        var groups = await db.CustomerSampleSharingGroups.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive
            && (companyManager || !db.CustomerSampleSharingMembers.Any(m => m.CompanyId == company && m.GroupId == x.Id && m.IsActive && !manageable.Contains(m.CustomerId))))
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name,
                CanEdit = !db.CustomerSampleSharingMembers.Any(m => m.CompanyId == company && m.GroupId == x.Id && m.IsActive && !manageable.Contains(m.CustomerId)) }).ToListAsync(ct);
        var availableGroups = groups.Select(x => x.Id).ToArray();
        var blockedGroups = groups.Where(x => !x.CanEdit).Select(x => x.Id).ToArray();
        var restricted = await db.CustomerSampleSharingMembers.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive
            && manageable.Contains(x.CustomerId) && (!availableGroups.Contains(x.GroupId) || blockedGroups.Contains(x.GroupId))).Select(x => x.CustomerId).Distinct().ToArrayAsync(ct);
        var members = await db.CustomerSampleSharingMembers.AsNoTracking().Where(x => x.CompanyId == company && x.IsActive && availableGroups.Contains(x.GroupId)
            && x.Group.CompanyId == company && x.Group.IsActive && x.Customer.CompanyId == company && x.Customer.IsActive == true)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new { x.GroupId, x.CustomerId, x.Customer.ExternalId, x.Customer.CustomerName }).ToListAsync(ct);
        var customers = await db.Customers.AsNoTracking().Where(x => x.CompanyId == company && manageable.Contains(x.CustomerId))
            .OrderBy(x => x.CustomerName).Select(x => new SampleSharingCustomerDto(x.CustomerId, x.ExternalId ?? "", x.CustomerName ?? "",
                x.CustomerAssignments.Where(a => a.IsActive && a.CompanyId == company).OrderByDescending(a => a.CreatedDate)
                    .Select(a => a.Employee.FullName).FirstOrDefault(), x.TaxNumber)).ToArrayAsync(ct);
        return OperationResult<SampleSharingWorkspaceDto>.Ok(new SampleSharingWorkspaceDto(true, true,
            groups.Select(g => new SampleSharingGroupDto(g.Id, g.Name, members.Where(m => m.GroupId == g.Id)
                .Select(m => new SampleSharingCustomerDto(m.CustomerId, m.ExternalId ?? "", m.CustomerName ?? "",
                    customers.FirstOrDefault(c => c.CustomerId == m.CustomerId)?.SaleName,
                    customers.FirstOrDefault(c => c.CustomerId == m.CustomerId)?.TaxNumber)).ToArray(), g.CanEdit)).ToArray(), customers, restricted,
            await SampleSharingRevision.ReadAsync(db, company, ct), companyManager));
    }
}
