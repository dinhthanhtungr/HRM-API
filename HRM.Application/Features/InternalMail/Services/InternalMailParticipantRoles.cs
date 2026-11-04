using HRM.Application.Commons.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

internal sealed record InternalMailParticipantRoles(HashSet<Guid> Developers, HashSet<Guid> Leaders, HashSet<Guid> LabUsers);

public sealed partial class InternalMailAreaAccessService
{
    // Snapshot riêng cho một lần tải detail; không cache quyền giữa request hoặc giữa công ty.
    internal async Task<InternalMailParticipantRoles> ParticipantRolesAsync(IEnumerable<Guid> employeeIds, CancellationToken ct)
    {
        var ids = employeeIds.Distinct().ToArray();
        var company = user.CompanyId ?? Guid.Empty;
        if (company == Guid.Empty || ids.Length == 0) return new([], [], []);
        var developerNames = ApplicationRoleSets.InternalMail.CompanyConversationObservers;
        var names = developerNames.Concat([ApplicationRoles.Leader, ApplicationRoles.Lab.LabUser]).Distinct().ToArray();
        var normalized = names.Select(n => n.ToUpperInvariant()).ToArray();
        var roles = await (from employee in db.Employees.AsNoTracking()
            join account in db.Users on employee.EmployeeId equals account.EmployeeId
            join membership in db.UserRoles on account.Id equals membership.UserId
            join role in db.Roles on membership.RoleId equals role.Id
            where employee.CompanyId == company && ids.Contains(employee.EmployeeId) && membership.IsActive &&
                ((role.Name != null && names.Contains(role.Name)) ||
                 (role.NormalizedName != null && normalized.Contains(role.NormalizedName)))
            select new { employee.EmployeeId, employee.IsActive, role.Name, role.NormalizedName }).ToListAsync(ct);
        static bool Matches(string? name, string? normalizedName, string wanted) =>
            string.Equals(name, wanted, StringComparison.Ordinal) ||
            string.Equals(normalizedName, wanted.ToUpperInvariant(), StringComparison.Ordinal);
        return new(
            roles.Where(r => developerNames.Any(n => Matches(r.Name, r.NormalizedName, n))).Select(r => r.EmployeeId).ToHashSet(),
            roles.Where(r => r.IsActive && Matches(r.Name, r.NormalizedName, ApplicationRoles.Leader)).Select(r => r.EmployeeId).ToHashSet(),
            roles.Where(r => r.IsActive && Matches(r.Name, r.NormalizedName, ApplicationRoles.Lab.LabUser)).Select(r => r.EmployeeId).ToHashSet());
    }
}
