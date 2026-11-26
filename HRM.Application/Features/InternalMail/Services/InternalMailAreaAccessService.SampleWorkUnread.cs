using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.InternalMail.Services;

public sealed partial class InternalMailAreaAccessService
{
    internal async Task<IReadOnlyDictionary<Guid, int>> SampleWorkUnreadAsync(Guid[] requestIds, CancellationToken ct)
    {
        if (requestIds.Length > 100) throw new ArgumentException("At most 100 sample requests are supported.");
        if (requestIds.Length == 0 || user.CompanyId is not { } company || user.EmployeeId == null)
            return new Dictionary<Guid, int>();

        using var timings = HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestSampleTrials.SampleWorkDiagnosticTags.Start("sample_unread");
        var roots = db.InternalConversations.AsNoTracking().Where(c => c.CompanyId == company && c.IsActive &&
            c.RelatedType == InternalMailRelatedType.SampleRequest && c.RelatedId.HasValue && requestIds.Contains(c.RelatedId.Value));
        var inboxIds = InboxConversations(false).Select(c => c.InternalConversationId);
        var groups = await (from groupScope in VisibleGroupScopes()
            join root in roots on groupScope.RootConversationId equals root.InternalConversationId
            where inboxIds.Contains(groupScope.ConversationId)
            select new { groupScope.ConversationId, SampleRequestId = root.RelatedId!.Value }).Distinct().ToArrayAsync(ct);
        timings?.Mark("visible-groups");
        if (groups.Length == 0) return new Dictionary<Guid, int>();
        var counts = await PageNotificationUnreadCountsAsync(
            groups.Select(g => g.ConversationId).Distinct().ToArray(), ct);
        timings?.Mark("counts");
        return groups.GroupBy(g => g.SampleRequestId)
            .ToDictionary(g => g.Key, g => g.Sum(c => counts.GetValueOrDefault(c.ConversationId)));
    }
}
