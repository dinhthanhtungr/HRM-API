using HRM.Domain.Entities.InternalMailSchema;
using HRM.Domain.Enums.InternalMailEnums;

namespace HRM.Infrastructure.Services.InternalMail;

// GROUP-STORAGE-TRANSITION: legacy-adapter — interpret legacy child fields for validation/backfill/live structural writes.
// Replace this input shape only after canonical writers are complete; retain the validator and company/root checks.
internal sealed record ConversationGroupSource(Guid Id, Guid CompanyId, string Subject, InternalMailRelatedType? RelatedType,
    Guid? RelatedId, string? ExternalId, bool IsActive)
{
    internal bool IsChild => RelatedType is InternalMailRelatedType.ConversationTechnical or InternalMailRelatedType.ConversationPricing or InternalMailRelatedType.ConversationCustom;
    internal Guid RootId => IsChild ? RelatedId ?? Guid.Empty : Id;
    internal InternalConversationGroupType Type => RelatedType switch
    {
        InternalMailRelatedType.ConversationTechnical => InternalConversationGroupType.Technical,
        InternalMailRelatedType.ConversationPricing => InternalConversationGroupType.Pricing,
        InternalMailRelatedType.ConversationCustom => InternalConversationGroupType.Custom,
        _ => InternalConversationGroupType.General
    };
}

internal static class InternalConversationGroupPlan
{
    internal static List<string> Validate(IReadOnlyList<ConversationGroupSource> sources,
        IReadOnlyList<InternalConversationGroup> mappings, Guid company, bool allowLifecycleUpdate)
    {
        var issues = new List<string>();
        var byId = sources.ToDictionary(c => c.Id);
        var mapped = mappings.ToDictionary(m => m.ConversationId);
        foreach (var c in sources)
        {
            if (c.CompanyId != company || c.Id == Guid.Empty) issues.Add("SourceCompanyOrIdentityInvalid");
            if (c.RelatedType == InternalMailRelatedType.ConversationForwardSource ||
                (c.RelatedType.HasValue && !Enum.IsDefined(c.RelatedType.Value))) issues.Add("UnsupportedConversationType");
            if (c.IsChild && (!byId.TryGetValue(c.RootId, out var root) || root.IsChild || root.CompanyId != company || root.Id == c.Id))
                issues.Add("MissingCrossCompanyOrNestedRoot");
            if (c.Type == InternalConversationGroupType.Custom && (string.IsNullOrWhiteSpace(c.Subject) || c.Subject.Length > 100))
                issues.Add("CustomNameInvalid");
            if (mapped.TryGetValue(c.Id, out var m) && (m.CompanyId != company || m.RootConversationId != c.RootId || m.GroupType != c.Type ||
                (!allowLifecycleUpdate && m.IsActive != c.IsActive))) issues.Add("ExistingMappingConflict");
        }
        if (sources.Where(c => c.IsActive && c.Type is InternalConversationGroupType.Technical or InternalConversationGroupType.Pricing)
            .GroupBy(c => (c.RootId, c.Type)).Any(g => g.Count() > 1)) issues.Add("DuplicateActiveDefaultGroup");
        if (mappings.Any(m => m.CompanyId == company && !byId.ContainsKey(m.ConversationId))) issues.Add("OrphanMapping");
        return issues.Distinct().ToList();
    }
}
