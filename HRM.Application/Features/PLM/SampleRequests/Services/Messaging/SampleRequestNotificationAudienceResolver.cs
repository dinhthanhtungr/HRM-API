using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Enums.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services;

/// <summary>
/// Resolve notification audience independently of conversation membership.
/// CustomerAssignments is the source of sales ownership, never the message sender.
/// </summary>
public sealed class SampleRequestNotificationAudienceResolver(
    ICRMReadDbContext crm, IInternalMailDbContext db)
{
    public async Task<IReadOnlyCollection<Guid>> ResolveAsync(
        Guid companyId, Guid sampleRequestId, TopicNotifications topic, CancellationToken ct, string? workflowEventType = null)
    {
        var audience = SampleRequestNotificationAudienceRules.For(topic, workflowEventType);
        if (audience == SampleRequestNotificationAudience.None) return Array.Empty<Guid>();

        var request = await db.SampleRequests.AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.IsActive && s.SampleRequestId == sampleRequestId &&
                s.Product.CompanyId == companyId && s.Product.IsActive &&
                (s.Product.Category == null || s.Product.Category.CompanyId == companyId))
            .Select(s => new { s.CustomerId, Category = s.Product.Category != null ? s.Product.Category.ExternalId : null })
            .SingleOrDefaultAsync(ct);
        if (request is null) return Array.Empty<Guid>();

        var recipients = new HashSet<Guid>();
        if (audience.HasFlag(SampleRequestNotificationAudience.CustomerSales))
        {
            var assignments = await crm.CustomerAssignments.AsNoTracking()
                .Where(a => a.CompanyId == companyId && a.IsActive && a.CustomerId == request.CustomerId &&
                    a.Customer.CompanyId == companyId && a.Customer.IsActive == true &&
                    a.Employee.CompanyId == companyId && a.Employee.IsActive && a.Group.CompanyId == companyId)
                .Select(a => new { a.EmployeeId, a.GroupId }).ToArrayAsync(ct);
            recipients.UnionWith(assignments.Select(a => a.EmployeeId));
            var groupIds = assignments.Select(a => a.GroupId).Distinct().ToArray();
            var admins = RoleEmployees(companyId, ApplicationRoleSets.Notifications.SampleRequestSalesAdmins);
            recipients.UnionWith(await db.MemberInGroups.AsNoTracking()
                .Where(m => m.IsActive && groupIds.Contains(m.GroupId) && m.Group.CompanyId == companyId &&
                    m.Profile.HasValue && admins.Contains(m.Profile.Value))
                .Select(m => m.Profile!.Value).Distinct().ToArrayAsync(ct));
        }

        if (audience.HasFlag(SampleRequestNotificationAudience.LabLeaders))
        {
            var groupTypes = SampleRequestRecipientRules.ResolveDefaultLeaderGroupTypes(request.Category);
            recipients.UnionWith(await db.MemberInGroups.AsNoTracking()
                .Where(m => m.IsActive && m.Group.CompanyId == companyId &&
                    m.Group.GroupType != null && groupTypes.Contains(m.Group.GroupType) &&
                    m.ProfileNavigation != null && m.ProfileNavigation.CompanyId == companyId && m.ProfileNavigation.IsActive)
                .Select(m => m.Profile!.Value).Distinct().ToArrayAsync(ct));
        }

        var roles = new List<string>();
        if (audience.HasFlag(SampleRequestNotificationAudience.LabAdmins)) roles.AddRange(ApplicationRoleSets.Notifications.SampleRequestLabAdmins);
        if (audience.HasFlag(SampleRequestNotificationAudience.Presidents)) roles.AddRange(ApplicationRoleSets.Notifications.SampleRequestPriceReviewers);
        if (roles.Count > 0) recipients.UnionWith(await RoleEmployees(companyId, roles).ToArrayAsync(ct));
        return recipients.ToArray();
    }

    private IQueryable<Guid> RoleEmployees(Guid companyId, IReadOnlyCollection<string> roleNames)
    {
        var normalized = roleNames.Select(r => r.ToUpperInvariant()).ToArray();
        return (from employee in db.Employees.AsNoTracking()
                join user in db.Users.AsNoTracking() on employee.EmployeeId equals user.EmployeeId
                join membership in db.UserRoles.AsNoTracking() on user.Id equals membership.UserId
                join role in db.Roles.AsNoTracking() on membership.RoleId equals role.Id
                where employee.CompanyId == companyId && employee.IsActive && membership.IsActive &&
                    ((role.Name != null && roleNames.Contains(role.Name)) ||
                     (role.NormalizedName != null && normalized.Contains(role.NormalizedName)))
                select employee.EmployeeId).Distinct();
    }
}
