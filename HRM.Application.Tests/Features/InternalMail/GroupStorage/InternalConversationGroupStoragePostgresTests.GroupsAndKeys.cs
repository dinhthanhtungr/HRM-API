using System.Diagnostics;
using System.Text.Json;
using HRM.Application.Features.InternalMail.Queries.GetConversations;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageTheory]
    [InlineData("Developer", true)]
    [InlineData("President", true)]
    [InlineData("LabUser", true)]
    [InlineData("SaleUser", true)]
    [InlineData("LabUser", false)]
    public async Task GroupsAndUnreadKeysPreserveResponsesAndMeasurePairedBaseline(string role, bool storage)
        => await MeasureSampleAllAsync(role, storage, 64, compareUnread: true, compareGroupsAndKeys: true);

    [PostgresGroupStorageFact]
    public Task LargeUnreadInboxPreservesKeysAndPages()
        => MeasureSampleAllAsync("LabUser", true, 256, compareUnread: true, compareGroupsAndKeys: true);

    private async Task CompareGroupsAndKeysAsync(ApplicationDbContext db, InternalMailAreaAccessService areas,
        GetInternalConversationsQueryHandler handler, UnreadPerformanceUser user, string role, bool storage, int eventCount)
    {
        if (eventCount > 64)
            await postgres.ExecuteAsync("UPDATE notification.notification_user_states SET is_read=false WHERE NOT is_archived;");
        var page = await handler.Handle(new GetInternalConversationsQuery {
            RelatedType = InternalMailRelatedType.SampleRequest, Archived = false, UnreadOnly = true, PageSize = 50, PageNumber = 1
        }, default);
        var roots = page.Items.Select(i => i.GroupConversationId).ToArray();
        var counts = page.Items.ToDictionary(i => i.ConversationId, i => i.UnreadCount);
        var measurements = new List<object>();
        async Task<string> Run(string part, bool old)
        {
            if (part == "unread-keys")
            {
                var keys = old ? await areas.UnreadConversationIds(InternalMailRelatedType.SampleRequest).Distinct().ToArrayAsync()
                    : await areas.UnreadConversationIdsAsync(InternalMailRelatedType.SampleRequest, default);
                return JsonSerializer.Serialize(keys.OrderBy(id => id));
            }
            var groups = old ? await ReferenceQuickGroupsAsync(db, areas, user, roots, default, counts)
                : await areas.QuickGroupsAsync(roots, default, counts);
            return JsonSerializer.Serialize(groups.OrderBy(pair => pair.Key));
        }
        foreach (var part in new[] { "unread-keys", "quick-groups" })
        {
            Assert.Equal(await Run(part, true), await Run(part, false));
            var before = new List<double>(); var after = new List<double>();
            async Task<string> Timed(bool old)
            {
                var timer = Stopwatch.StartNew(); var value = await Run(part, old);
                (old ? before : after).Add(timer.Elapsed.TotalMilliseconds); return value;
            }
            for (var i = 0; i < 5; i++) Assert.Equal(await Timed(i % 2 == 0), await Timed(i % 2 != 0));
            measurements.Add(new { part, samples = 5, beforeMedianMs = before.Order().ElementAt(2), afterMedianMs = after.Order().ElementAt(2),
                beforeRange = new[] { before.Min(), before.Max() }, afterRange = new[] { after.Min(), after.Max() } });
        }
        // Every page keeps the entire DTO; only replace the groups with the frozen reader and compare.
        for (var number = 1; number <= 9; number++)
        {
            var current = await handler.Handle(new GetInternalConversationsQuery {
                RelatedType = InternalMailRelatedType.SampleRequest, Archived = false, UnreadOnly = true, PageSize = 50, PageNumber = number
            }, default);
            var json = JsonSerializer.Serialize(current);
            var reference = await ReferenceQuickGroupsAsync(db, areas, user, current.Items.Select(i => i.GroupConversationId), default,
                current.Items.ToDictionary(i => i.ConversationId, i => i.UnreadCount));
            foreach (var item in current.Items) item.QuickGroups = reference.GetValueOrDefault(item.GroupConversationId) ?? [];
            Assert.Equal(json, JsonSerializer.Serialize(current));
        }
        var path = Path.Combine(postgres.RepositoryRoot, "artifacts", "groups-unread-keys-comparison");
        Directory.CreateDirectory(path);
        var totalNotifications = await db.Notifications.CountAsync();
        var unreadStates = await db.NotificationUserStates.CountAsync(s => s.UserId == user.EmployeeId && !s.IsRead && !s.IsArchived &&
            s.Notification.CompanyId == user.CompanyId);
        await File.WriteAllTextAsync(Path.Combine(path, role + (storage ? ".canonical" : ".legacy") + (eventCount > 64 ? ".large" : "") + ".json"),
            JsonSerializer.Serialize(new { role, storage, eventCount, totalNotifications, unreadStates,
                unreadSampleConversations = page.TotalCount, synthetic = true, responseParity = true, measurements }));
    }

    private static IQueryable<Guid> ReferenceTypedUnreadKeys(ApplicationDbContext db, InternalMailAreaAccessService access,
        UnreadPerformanceUser user, bool storage)
    {
        var company = user.CompanyId!.Value; var employee = user.EmployeeId!.Value;
        var observer = access.CanObserveCompanyConversations;
        var readable = access.Conversations().Where(c => observer || c.Participants.Any(p => p.EmployeeId == employee && p.IsActive));
        var reactions = InternalMailPersonalActivity.ReactionTopics;
        var states = db.NotificationUserStates.Where(s => s.UserId == employee && !s.IsRead && !s.IsArchived &&
            s.Notification.CompanyId == company && !reactions.Contains(s.Notification.Topic));
        var links = db.QueryInternalMailNotificationLinks(company);
        var legacy = access.LegacyUnreadMessages();
        if (storage)
        {
            var typedIds = db.InternalConversations.Where(c => c.CompanyId == company &&
                (c.RelatedType == InternalMailRelatedType.SampleRequest ||
                 ((c.RelatedType == InternalMailRelatedType.ConversationTechnical || c.RelatedType == InternalMailRelatedType.ConversationPricing || c.RelatedType == InternalMailRelatedType.ConversationCustom) &&
                  db.InternalConversations.Any(parent => parent.InternalConversationId == c.RelatedId && parent.CompanyId == company && parent.RelatedType == InternalMailRelatedType.SampleRequest))))
                .Select(c => c.InternalConversationId);
            links = links.Where(l => l.ConversationId.HasValue && typedIds.Contains(l.ConversationId.Value));
            legacy = legacy.Where(m => typedIds.Contains(m.InternalConversationId));
        }
        var ids = from state in states join link in links on state.NotificationId equals link.NotificationId
                  where link.ConversationId.HasValue && readable.Any(c => c.InternalConversationId == link.ConversationId)
                  select link.ConversationId!.Value;
        return ids.Concat(legacy.Select(m => m.InternalConversationId));
    }
}
