using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Security;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

/// <summary>
/// Explicit, post-expansion reader for comparison. No endpoint calls it during phase 2, so deployments
/// without the new table keep using the existing permission/badge queries without probing for the table.
/// </summary>
// GROUP-STORAGE-TRANSITION: comparison-reader — remove with its DI registration and comparison-only tests after acceptance.
internal sealed class InternalConversationGroupReader(IInternalMailDbContext db, ICurrentUser user, InternalMailAreaAccessService access)
{
    internal const int MaximumBatchSize = 200;

    public async Task<IReadOnlyList<InternalConversationGroupInfo>> ReadAsync(IReadOnlyCollection<Guid> conversationIds, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.CompanyId is not { } company || company == Guid.Empty ||
            user.EmployeeId is not { } employee || employee == Guid.Empty || conversationIds.Count == 0) return [];
        if (conversationIds.Count > MaximumBatchSize) throw new ArgumentOutOfRangeException(nameof(conversationIds));
        var ids = conversationIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return [];
        return await Query(company, ids).OrderBy(g => g.ConversationId).ToArrayAsync(ct);
    }

    internal IQueryable<InternalConversationGroupInfo> Query(Guid company, Guid[] ids)
    {
        // Membership/capability stays authoritative in the existing access service. A mapping is not a grant.
        return from mapping in db.InternalConversationGroups.AsNoTracking()
               join conversation in access.Conversations().AsNoTracking() on mapping.ConversationId equals conversation.InternalConversationId
               join root in db.InternalConversationGroups.AsNoTracking() on mapping.RootConversationId equals root.ConversationId
               join rootConversation in db.InternalConversations.AsNoTracking() on root.ConversationId equals rootConversation.InternalConversationId
               where mapping.CompanyId == company && conversation.CompanyId == company && mapping.IsActive &&
                   root.CompanyId == company && rootConversation.CompanyId == company && root.IsActive && rootConversation.IsActive &&
                   root.GroupType == InternalConversationGroupType.General && root.RootConversationId == root.ConversationId &&
                   ids.Contains(mapping.ConversationId) &&
                   ((mapping.GroupType == InternalConversationGroupType.General && mapping.RootConversationId == mapping.ConversationId) ||
                    ((mapping.GroupType == InternalConversationGroupType.Technical || mapping.GroupType == InternalConversationGroupType.Pricing ||
                      mapping.GroupType == InternalConversationGroupType.Custom) && mapping.RootConversationId != mapping.ConversationId))
               select new InternalConversationGroupInfo(mapping.ConversationId, mapping.RootConversationId, mapping.GroupType, mapping.DisplayName);
    }
}
