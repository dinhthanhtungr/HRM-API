using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    // GROUP-STORAGE-TRANSITION: switch — temporary rollout/rollback flag; the mapped query below is permanent.
    internal bool UsesGroupStorage => storageOptions?.Value.Enabled == true;

    // No schema probing or fallback on database errors. A missing/mismatched mapping fails closed.
    // Legacy fields remain a checked compatibility mirror until all writers have been migrated.
    internal IQueryable<InternalConversationGroupScope> GroupScopes(Guid company)
    {
        var conversations = db.InternalConversations.Where(c => c.CompanyId == company && c.IsActive);
        // GROUP-STORAGE-TRANSITION: legacy-adapter BEGIN — remove the false branch after final cutover acceptance.
        if (!UsesGroupStorage)
            return conversations.Select(c => new InternalConversationGroupScope
            {
                Conversation = c, ConversationId = c.InternalConversationId,
                RootConversationId = c.RelatedType == InternalMailRelatedType.ConversationTechnical ||
                    c.RelatedType == InternalMailRelatedType.ConversationPricing || c.RelatedType == InternalMailRelatedType.ConversationCustom
                    ? c.RelatedId!.Value : c.InternalConversationId,
                GroupType = c.RelatedType == InternalMailRelatedType.ConversationTechnical ? InternalConversationGroupType.Technical :
                    c.RelatedType == InternalMailRelatedType.ConversationPricing ? InternalConversationGroupType.Pricing :
                    c.RelatedType == InternalMailRelatedType.ConversationCustom ? InternalConversationGroupType.Custom : InternalConversationGroupType.General,
                DisplayName = c.Subject
            });

        // GROUP-STORAGE-TRANSITION: legacy-adapter END.
        // GROUP-STORAGE-TRANSITION: mirror-validation — legacy RelatedType/RelatedId checks remain until old writers are replaced.
        // Keep company, active, root/type consistency and authorization checks when removing the legacy mirror checks.
        return from mapping in db.InternalConversationGroups
               join conversation in conversations on mapping.ConversationId equals conversation.InternalConversationId
               join root in db.InternalConversationGroups on mapping.RootConversationId equals root.ConversationId
               join rootConversation in conversations on root.ConversationId equals rootConversation.InternalConversationId
               where mapping.CompanyId == company && mapping.IsActive && root.CompanyId == company && root.IsActive &&
                   root.GroupType == InternalConversationGroupType.General && root.RootConversationId == root.ConversationId &&
                   rootConversation.RelatedType != InternalMailRelatedType.ConversationTechnical &&
                   rootConversation.RelatedType != InternalMailRelatedType.ConversationPricing &&
                   rootConversation.RelatedType != InternalMailRelatedType.ConversationCustom &&
                   rootConversation.RelatedType != InternalMailRelatedType.ConversationForwardSource &&
                   ((mapping.GroupType == InternalConversationGroupType.General && mapping.RootConversationId == mapping.ConversationId &&
                     conversation.RelatedType != InternalMailRelatedType.ConversationTechnical &&
                     conversation.RelatedType != InternalMailRelatedType.ConversationPricing &&
                     conversation.RelatedType != InternalMailRelatedType.ConversationCustom &&
                     conversation.RelatedType != InternalMailRelatedType.ConversationForwardSource) ||
                    (mapping.RootConversationId != mapping.ConversationId && conversation.RelatedId == mapping.RootConversationId &&
                     ((mapping.GroupType == InternalConversationGroupType.Technical && conversation.RelatedType == InternalMailRelatedType.ConversationTechnical) ||
                      (mapping.GroupType == InternalConversationGroupType.Pricing && conversation.RelatedType == InternalMailRelatedType.ConversationPricing) ||
                      (mapping.GroupType == InternalConversationGroupType.Custom && conversation.RelatedType == InternalMailRelatedType.ConversationCustom))))
               select new InternalConversationGroupScope
               {
                   Conversation = conversation, ConversationId = mapping.ConversationId,
                   RootConversationId = mapping.RootConversationId, GroupType = mapping.GroupType,
                   DisplayName = mapping.DisplayName ?? conversation.Subject
               };
    }

    internal IQueryable<InternalConversationGroupScope> VisibleGroupScopes()
    {
        var visible = Conversations().Select(c => c.InternalConversationId);
        return GroupScopes(user.CompanyId ?? Guid.Empty).Where(g => visible.Contains(g.ConversationId));
    }

    internal Task<Guid> RootIdAsync(Guid conversationId, CancellationToken ct) =>
        GroupScopes(user.CompanyId ?? Guid.Empty).Where(g => g.ConversationId == conversationId)
            .Select(g => g.RootConversationId).SingleAsync(ct);

    internal IQueryable<InternalConversation> GroupChildren(Guid rootId, InternalMailRelatedType? legacyType = null)
    {
        var groups = GroupScopes(user.CompanyId ?? Guid.Empty).Where(g => g.RootConversationId == rootId && g.ConversationId != rootId);
        if (legacyType.HasValue)
        {
            var type = legacyType == InternalMailRelatedType.ConversationTechnical ? InternalConversationGroupType.Technical :
                legacyType == InternalMailRelatedType.ConversationPricing ? InternalConversationGroupType.Pricing : InternalConversationGroupType.Custom;
            groups = groups.Where(g => g.GroupType == type);
        }
        var ids = groups.Select(g => g.ConversationId);
        return db.InternalConversations.Where(c => c.CompanyId == user.CompanyId && c.IsActive && ids.Contains(c.InternalConversationId));
    }

    internal async Task<IReadOnlyDictionary<Guid, InternalConversationGroupInfo>> GroupInfoAsync(Guid[] ids, CancellationToken ct) =>
        await GroupScopes(user.CompanyId ?? Guid.Empty).AsNoTracking().Where(g => ids.Contains(g.ConversationId))
            .Select(g => new InternalConversationGroupInfo(g.ConversationId, g.RootConversationId, g.GroupType, g.DisplayName))
            .ToDictionaryAsync(g => g.ConversationId, ct);

    internal static string AreaOf(InternalConversationGroupType type) => type switch
    {
        InternalConversationGroupType.Technical => InternalMailAreas.Technical,
        InternalConversationGroupType.Pricing => InternalMailAreas.Pricing,
        InternalConversationGroupType.Custom => InternalMailAreas.Custom,
        _ => InternalMailAreas.General
    };
}
