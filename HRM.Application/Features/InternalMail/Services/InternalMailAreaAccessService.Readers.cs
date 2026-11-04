using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    private const string UnmappedLabReaderGroupType = "LAB";
    // Chỉ gom sau bộ lọc quyền cuối cùng; không lấy tên leader ngoài danh sách được xem.
    internal async Task<IReadOnlyList<InternalConversationParticipantDto>> WithReaderGroupsAsync(
        IReadOnlyList<InternalConversationParticipantDto> visible, CancellationToken ct, InternalMailParticipantRoles? roleSnapshot = null)
    {
        if (visible.Count == 0) return visible;
        foreach (var participant in visible) participant.ReaderGroup = null;
        var company = user.CompanyId ?? Guid.Empty;
        if (company == Guid.Empty) return visible;
        var ids = visible.Select(p => p.EmployeeId).ToArray();
        var types = SampleRequestRecipientRules.RequiredLeaderGroupTypes;
        var memberships = await db.MemberInGroups.AsNoTracking()
            .Where(m => m.IsActive && m.Profile.HasValue && ids.Contains(m.Profile.Value) &&
                m.Group.CompanyId == company && types.Contains(m.Group.GroupType!) &&
                m.ProfileNavigation != null && m.ProfileNavigation.IsActive && m.ProfileNavigation.CompanyId == company)
            .Select(m => new { EmployeeId = m.Profile!.Value, m.GroupId, m.Group.GroupType, IsAdmin = m.IsAdmin == true })
            .ToListAsync(ct);
        var roleLeaders = visible.Where(p => p.IsLeader).Select(p => p.EmployeeId).ToHashSet();
        var labLeaders = memberships.Where(m => m.IsAdmin || roleLeaders.Contains(m.EmployeeId))
            .Select(m => m.EmployeeId).ToHashSet();
        var labMembers = roleSnapshot?.LabUsers ?? await RoleEmployees(company, [ApplicationRoles.Lab.LabUser])
            .Where(id => ids.Contains(id)).ToHashSetAsync(ct);
        labLeaders.UnionWith(roleLeaders.Where(labMembers.Contains));
        var assigned = new HashSet<Guid>();
        foreach (var group in memberships.GroupBy(m => m.GroupId).OrderBy(g => g.Key))
        {
            var groupIds = group.Select(m => m.EmployeeId).ToHashSet();
            var leader = visible.Where(p => groupIds.Contains(p.EmployeeId) && labLeaders.Contains(p.EmployeeId))
                .OrderByDescending(p => group.Any(m => m.EmployeeId == p.EmployeeId && m.IsAdmin))
                .ThenBy(p => p.JoinedAt).ThenBy(p => p.EmployeeId).FirstOrDefault();
            var metadata = new InternalConversationReaderGroupDto(group.Key, group.First().GroupType!, leader?.EmployeeId);
            foreach (var participant in visible.Where(p => groupIds.Contains(p.EmployeeId) && !labLeaders.Contains(p.EmployeeId)))
                if (assigned.Add(participant.EmployeeId)) participant.ReaderGroup = metadata;
        }
        var commonLabLeader = visible.Where(p => labMembers.Contains(p.EmployeeId) && labLeaders.Contains(p.EmployeeId))
            .OrderBy(p => p.JoinedAt).ThenBy(p => p.EmployeeId).FirstOrDefault();
        // Guid.Empty là khóa hiển thị của Lab chung, không phải Group hay membership mới trong DB.
        var commonLab = new InternalConversationReaderGroupDto(Guid.Empty, UnmappedLabReaderGroupType, commonLabLeader?.EmployeeId);
        foreach (var participant in visible.Where(p => p.ReaderGroup is null && labMembers.Contains(p.EmployeeId) && !labLeaders.Contains(p.EmployeeId)))
            participant.ReaderGroup = commonLab;
        return visible.OrderByDescending(p => labLeaders.Contains(p.EmployeeId))
            .ThenByDescending(p => p.IsLeader).ThenBy(p => p.JoinedAt).ThenBy(p => p.EmployeeId).ToArray();
    }
}
