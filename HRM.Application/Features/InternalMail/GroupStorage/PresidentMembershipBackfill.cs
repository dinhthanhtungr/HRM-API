using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.GroupStorage;

// GROUP-STORAGE-TRANSITION: synchronization-api — compensates legacy observer access; remove after acceptance, not with live defaults.
public sealed class PresidentMembershipBackfill(IInternalMailDbContext db, InternalMailAreaAccessService areas)
{
    private const int MaximumReportRows = 20_000;

    /// <summary>Caller owns the mapping transaction/company lock. Never reactivates members or emits chat/notification events.</summary>
    public async Task ApplyAsync(GroupStorageSyncReport report, CancellationToken ct)
    {
        var company = report.CompanyId;
        var developers = areas.RoleEmployees(company, ApplicationRoleSets.InternalMail.CompanyConversationObservers);
        var presidents = await areas.RoleEmployees(company, ApplicationRoleSets.InternalMail.PricingExecutives).Where(id => !developers.Contains(id))
            .OrderBy(id => id).ToArrayAsync(ct);
        if (presidents.Length == 0) return;
        var groups = report.Rows.Where(g => g.GroupType != InternalConversationGroupType.General).ToArray();
        if ((long)groups.Length * presidents.Length > MaximumReportRows)
        {
            report.Outcome = "Blocked";
            report.Issues.Add("TooManyPresidentMembershipRowsNoChangesApplied");
            return;
        }
        // Same root locks as invite/remove; read membership only after acquiring them.
        foreach (var root in groups.Select(g => g.RootConversationId).Distinct().OrderBy(id => id))
            await db.LockInternalConversationAsync(company, root, ct);
        var rootIds = groups.Select(g => g.RootConversationId).Distinct().ToArray();
        var roots = await db.InternalConversations.AsNoTracking().Where(c => rootIds.Contains(c.InternalConversationId) &&
                c.CompanyId == company && c.IsActive && c.RelatedType == InternalMailRelatedType.SampleRequest &&
                db.SampleRequests.Any(s => s.SampleRequestId == c.RelatedId && s.CompanyId == company && s.IsActive))
            .Select(c => c.InternalConversationId).ToHashSetAsync(ct);
        var ids = groups.Select(g => g.ConversationId).ToArray();
        var existing = await db.InternalConversationParticipants.AsNoTracking()
            .Where(p => ids.Contains(p.InternalConversationId) && presidents.Contains(p.EmployeeId))
            .Select(p => new { p.InternalConversationId, p.EmployeeId, p.IsActive })
            .ToDictionaryAsync(p => (p.InternalConversationId, p.EmployeeId), ct);
        var forwardedGroups = await db.InternalMessageReferences.AsNoTracking().Where(r =>
                ids.Contains(r.Message.InternalConversationId) && r.RelatedType == InternalMailRelatedType.ConversationForwardSource)
            .Select(r => r.Message.InternalConversationId).Distinct().ToHashSetAsync(ct);
        // Batch the existing product eligibility queries, rather than running them for every group/President pair.
        var colorGroups = await areas.ColorConversationIds(company).Where(id => ids.Contains(id)).ToHashSetAsync(ct);
        var rdPresidents = await areas.RdLeaderIds(company).Where(id => presidents.Contains(id)).ToHashSetAsync(ct);
        var additions = new List<InternalConversationParticipant>();
        foreach (var group in groups)
        {
            var eligible = group.IsActive && roots.Contains(group.RootConversationId) &&
                group.GroupType is InternalConversationGroupType.Technical or InternalConversationGroupType.Pricing;
            var allowed = eligible ? presidents.Where(id => !colorGroups.Contains(group.ConversationId) || !rdPresidents.Contains(id)).ToArray() : [];
            if (eligible && forwardedGroups.Contains(group.ConversationId))
                allowed = await areas.ConstrainDefaultMembersToForwardSourcesAsync(group.ConversationId, allowed, ct);
            foreach (var employee in presidents)
            {
                var status = PresidentMembershipSyncStatus.Skipped;
                var reason = group.GroupType == InternalConversationGroupType.Custom ? "CustomRequiresExplicitInvitation" :
                    !group.IsActive ? "InactiveGroup" : !roots.Contains(group.RootConversationId) ? "InactiveOrNonSampleRequestRoot" :
                    "OutsideProductOrForwardSourceScope";
                if (eligible && existing.TryGetValue((group.ConversationId, employee), out var member))
                {
                    status = member.IsActive ? PresidentMembershipSyncStatus.Existing : PresidentMembershipSyncStatus.Removed;
                    reason = member.IsActive ? "ExistingMembershipPreserved" : "RemovedMembershipNotReactivated";
                }
                else if (eligible && allowed.Contains(employee))
                {
                    status = PresidentMembershipSyncStatus.Added;
                    reason = "MissingDefaultMembership";
                    additions.Add(new() { InternalConversationId = group.ConversationId, EmployeeId = employee,
                        Role = InternalConversationParticipantRole.Member,
                        JoinedAt = DateTime.SpecifyKind(report.StartedAt.LocalDateTime, DateTimeKind.Unspecified) });
                }
                report.PresidentMemberships.Add(new(group.ConversationId, group.RootConversationId, employee, status, reason));
            }
        }
        if (additions.Count == 0) return;
        await db.InternalConversationParticipants.AddRangeAsync(additions, ct);
        await db.SaveChangesAsync(ct);
    }
}
