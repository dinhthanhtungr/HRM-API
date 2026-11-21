using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;
using HRM.Application.Features.InternalMail.Dtos;

namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    // Server-owned identity, independent of the editable display name. Never copy to ordinary custom groups.
    internal const string ResearchMarker = "internal-mail:sample-request:rd:v1";
    public const string ResearchArea = "rd";

    internal IQueryable<Guid> ResearchMemberIds(Guid company) => db.MemberInGroups.Where(m =>
        m.IsActive && m.Group.CompanyId == company && m.Group.GroupType == "QAQC.RD" &&
        m.ProfileNavigation != null && m.ProfileNavigation.IsActive && m.ProfileNavigation.CompanyId == company)
        .Select(m => m.Profile!.Value).Distinct();

    internal IQueryable<Guid> ResearchConversationIds(Guid company) => db.InternalConversations.Where(c =>
        c.CompanyId == company && c.RelatedType == InternalMailRelatedType.ConversationCustom &&
        c.RelatedExternalId == ResearchMarker).Select(c => c.InternalConversationId);

    internal Task<bool> CanUseResearchAsync(CancellationToken ct) => CanObserveCompanyConversations
        ? Task.FromResult(true)
        : ResearchMemberIds(user.CompanyId ?? Guid.Empty).AnyAsync(id => id == user.EmployeeId, ct);
    internal async Task<IReadOnlyList<InternalConversationParticipantDto>?> PreviewResearchParticipantsAsync(Guid rootId, CancellationToken ct)
    {
        if (!await CanUseResearchAsync(ct) || !await Conversations().AnyAsync(c =>
            c.InternalConversationId == rootId && c.RelatedType == InternalMailRelatedType.SampleRequest, ct))
            return null;
        var company = user.CompanyId ?? Guid.Empty;
        var ids = ResearchMemberIds(company);
        var members = await db.Employees.AsNoTracking().Where(e => e.CompanyId == company && e.IsActive && ids.Contains(e.EmployeeId))
            .OrderBy(e => e.FullName).Select(e => new InternalConversationParticipantDto
            {
                EmployeeId = e.EmployeeId, EmployeeName = e.FullName,
                Role = InternalConversationParticipantRole.Member
            }).ToListAsync(ct);
        return await WithReaderGroupsAsync(await VisibleParticipantsAsync(InternalMailRelatedType.ConversationCustom, members, ct), ct);
    }
}
