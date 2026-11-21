using System.Diagnostics;
using System.Text.Json;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Enums.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageTheory]
    [InlineData("Developer", 400)]
    [InlineData("LabUser", 400)]
    [InlineData("SaleUser", 400)]
    [InlineData("LabUser", 4000)]
    public async Task SyntheticUnreadExistenceAndComposedFeedPreserveResults(string role, int conversations)
    {
        // This fixture starts and verifies its own loopback-only cluster, never the application DB.
        await PresidentBusinessSchemaAsync();
        await postgres.ExecuteAsync("""
            INSERT INTO "InternalMail"."InternalConversations"
              ("InternalConversationId","CompanyId","Subject","RelatedType","CreatedBy")
            SELECT md5('unread-conversation-' || i)::uuid,
              CASE WHEN i % 13 = 0 THEN '10000000-0000-0000-0000-000000000002'::uuid
                   ELSE '10000000-0000-0000-0000-000000000001'::uuid END,
              'Synthetic',16,'00000000-0000-0000-0000-000000000001' FROM generate_series(1,400) AS i;
            INSERT INTO "InternalMail"."InternalConversationParticipants" ("InternalConversationId","EmployeeId")
            SELECT md5('unread-conversation-' || i)::uuid,'00000000-0000-0000-0000-000000000001'
              FROM generate_series(1,400) AS i WHERE i % 5 <> 0;
            INSERT INTO "InternalMail"."InternalMessages"
              ("InternalMessageId","InternalConversationId","SenderEmployeeId","Body","SentAt")
            SELECT md5('unread-message-' || i)::uuid,md5('unread-conversation-' || i)::uuid,
              '00000000-0000-0000-0000-000000000002','Synthetic','2026-10-05'::timestamp + i * interval '1 minute'
              FROM generate_series(1,400) AS i;
            INSERT INTO "InternalMail"."InternalMessageReadStates" ("InternalMessageId","EmployeeId")
            SELECT md5('unread-message-' || i)::uuid,'00000000-0000-0000-0000-000000000001'
              FROM generate_series(1,400) AS i WHERE i % 11 = 0;
            INSERT INTO notification.notifications (id,company_id,payload_json,created_date,topic)
            SELECT md5('unread-notification-' || i || '-' || j)::uuid,
              CASE WHEN i % 13 = 0 THEN '10000000-0000-0000-0000-000000000002'::uuid
                   ELSE '10000000-0000-0000-0000-000000000001'::uuid END,
              jsonb_build_object('conversationId',md5('unread-conversation-' || i)::uuid),
              '2026-10-05'::timestamp + (i * 10 + j) * interval '1 minute',
              CASE WHEN j = 8 THEN 1 ELSE 38 END
              FROM generate_series(1,400) AS i CROSS JOIN generate_series(1,8) AS j;
            INSERT INTO notification.notification_user_states (notification_id,user_id,is_read,is_archived)
            SELECT md5('unread-notification-' || i || '-' || j)::uuid,
              '00000000-0000-0000-0000-000000000001', i % 3 = 0 OR j = 8, i % 7 = 0
              FROM generate_series(1,400) AS i CROSS JOIN generate_series(1,8) AS j;
            INSERT INTO notification.notifications (id,company_id,payload_json,created_date,topic) VALUES
              ('f0000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001',NULL,'2026-10-10',38),
              ('f0000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000001','{}','2026-10-10',38),
              ('f0000000-0000-0000-0000-000000000003','10000000-0000-0000-0000-000000000001','{"conversationId":"invalid"}','2026-10-10',38),
              ('f0000000-0000-0000-0000-000000000004','10000000-0000-0000-0000-000000000001','{"conversationId":null}','2026-10-10',38);
            INSERT INTO notification.notification_user_states (notification_id,user_id)
            SELECT id,'00000000-0000-0000-0000-000000000001' FROM notification.notifications WHERE id::text LIKE 'f0000000-%';
            ANALYZE;
            """.Replace("generate_series(1,400)", $"generate_series(1,{conversations})"));
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        db.Database.SetCommandTimeout(10);
        var user = new UnreadPerformanceUser(role);
        var areas = new InternalMailAreaAccessService(db, user);
        var oldCounts = areas.NotificationUnreadCounts();
        var newIds = areas.UnreadConversationIds();
        var rows = areas.InboxConversations(false);
        var oldUnreadRows = rows.Where(r => oldCounts.Any(c => c.ConversationId == r.InternalConversationId && c.UnreadCount > 0));
        var newUnreadRows = rows.Where(r => newIds.Contains(r.InternalConversationId));
        var scopedUnreadRows = RejectedCorrelatedUnreadBaseline(areas, db, user);
        async Task<int> StagedCount()
        {
            var keys = await areas.UnreadConversationIds().Distinct().ToArrayAsync();
            return await rows.Where(r => keys.Contains(r.InternalConversationId)).CountAsync();
        }
        if (conversations > 400)
        {
            var expectedIds = await newUnreadRows.Select(r => r.InternalConversationId).Order().ToArrayAsync();
            var stagedKeys = await areas.UnreadConversationIds().Distinct().ToArrayAsync();
            Assert.Equal(expectedIds, await rows.Where(r => stagedKeys.Contains(r.InternalConversationId))
                .Select(r => r.InternalConversationId).Order().ToArrayAsync());
            Assert.Equal(await newUnreadRows.CountAsync(), await StagedCount());
            var global = new List<double>();
            var staged = new List<double>();
            for (var sample = 0; sample < 5; sample++)
            {
                if (sample % 2 == 0)
                    Assert.Equal(await MeasureCount(() => newUnreadRows.CountAsync(), global), await MeasureCount(StagedCount, staged));
                else
                    Assert.Equal(await MeasureCount(StagedCount, staged), await MeasureCount(() => newUnreadRows.CountAsync(), global));
            }
            var largeReport = new { role, conversations, notifications = conversations * 8 + 4, samples = 5,
                countExists = Summary(global), countStaged = Summary(staged), stagedConversationKeys = stagedKeys.Length };
            var largeDestination = Path.Combine(postgres.RepositoryRoot, "artifacts", "unread-performance");
            Directory.CreateDirectory(largeDestination);
            await File.WriteAllTextAsync(Path.Combine(largeDestination, role + "-" + conversations + ".json"), JsonSerializer.Serialize(largeReport));
            return;
        }
        var oldInbox = oldUnreadRows
            .OrderByDescending(r => r.ActivityAt).ThenByDescending(r => r.InternalConversationId)
            .Select(r => r.InternalConversationId).Take(50);
        var newInbox = newUnreadRows
            .OrderByDescending(r => r.ActivityAt).ThenByDescending(r => r.InternalConversationId)
            .Select(r => r.InternalConversationId).Take(50);
        Assert.Equal(await oldInbox.ToArrayAsync(), await newInbox.ToArrayAsync());
        Assert.Equal(await oldUnreadRows.CountAsync(), await newUnreadRows.CountAsync());
        Assert.Equal(await newUnreadRows.CountAsync(), await StagedCount());
        Assert.Equal(await oldUnreadRows.Select(r => r.InternalConversationId).Order().ToArrayAsync(),
            await scopedUnreadRows.Select(r => r.InternalConversationId).Order().ToArrayAsync());

        var company = user.CompanyId!.Value;
        var employee = user.EmployeeId!.Value;
        var reactions = InternalMailPersonalActivity.ReactionTopics;
        var inbox = db.Notifications.AsNoTracking().Where(n => n.CompanyId == company &&
            n.UserStates.Any(s => s.UserId == employee && !s.IsArchived) && !reactions.Contains(n.Topic));
        var visible = areas.Notifications(inbox);
        async Task<Guid[]> OldFeed()
        {
            var eligible = await areas.UnreadHubEvents(visible, inbox).Select(n => n.Id).ToArrayAsync();
            return await areas.LatestHubEvents(visible.Where(n => eligible.Contains(n.Id)), inbox, null)
                .OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Select(n => n.Id).Take(30).ToArrayAsync();
        }
        Task<Guid[]> NewFeed() => areas.UnreadHubEvents(areas.LatestHubEvents(visible, inbox, null), inbox)
            .OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Select(n => n.Id).Take(30).ToArrayAsync();
        async Task<Guid[]> ConversationStagedFeed()
        {
            var keys = await areas.UnreadHubConversationIds(inbox).ToArrayAsync();
            var eligible = areas.UnreadHubEventsForConversations(visible, inbox, keys);
            return await areas.LatestHubEvents(eligible, inbox, null)
                .OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Select(n => n.Id).Take(30).ToArrayAsync();
        }
        Assert.Equal(await OldFeed(), await NewFeed());
        Assert.Equal(await OldFeed(), await ConversationStagedFeed());
        var standaloneIds = await OldFeed();
        Assert.Contains(Guid.Parse("f0000000-0000-0000-0000-000000000001"), standaloneIds);
        Assert.Contains(Guid.Parse("f0000000-0000-0000-0000-000000000002"), standaloneIds);
        Assert.DoesNotContain(Guid.Parse("f0000000-0000-0000-0000-000000000003"), standaloneIds);
        var originalEligibleNotificationIds = await areas.UnreadHubEvents(visible, inbox).Select(n => n.Id).CountAsync();
        var stagedConversationKeys = await areas.UnreadHubConversationIds(inbox).CountAsync();
        var inboxOld = new List<double>();
        var inboxNew = new List<double>();
        var feedOld = new List<double>();
        var feedNew = new List<double>();
        var feedConversationStaged = new List<double>();
        var countOld = new List<double>();
        var countExists = new List<double>();
        var countScoped = new List<double>();
        var countStaged = new List<double>();
        for (var sample = 0; sample < 5; sample++)
        {
            // Alternate order after warm-up; compare results on every measured run.
            if (sample % 2 == 0)
            {
                Assert.Equal(await Measure(() => oldInbox.ToArrayAsync(), inboxOld), await Measure(() => newInbox.ToArrayAsync(), inboxNew));
                Assert.Equal(await Measure(OldFeed, feedOld), await Measure(NewFeed, feedNew));
                Assert.Equal(await OldFeed(), await Measure(ConversationStagedFeed, feedConversationStaged));
                Assert.Equal(await MeasureCount(() => oldUnreadRows.CountAsync(), countOld), await MeasureCount(() => newUnreadRows.CountAsync(), countExists));
                Assert.Equal(await newUnreadRows.CountAsync(), await MeasureCount(() => scopedUnreadRows.CountAsync(), countScoped));
                Assert.Equal(await newUnreadRows.CountAsync(), await MeasureCount(StagedCount, countStaged));
            }
            else
            {
                Assert.Equal(await Measure(() => newInbox.ToArrayAsync(), inboxNew), await Measure(() => oldInbox.ToArrayAsync(), inboxOld));
                Assert.Equal(await Measure(NewFeed, feedNew), await Measure(OldFeed, feedOld));
                Assert.Equal(await Measure(ConversationStagedFeed, feedConversationStaged), await OldFeed());
                Assert.Equal(await MeasureCount(() => newUnreadRows.CountAsync(), countExists), await MeasureCount(() => oldUnreadRows.CountAsync(), countOld));
                Assert.Equal(await MeasureCount(() => scopedUnreadRows.CountAsync(), countScoped), await newUnreadRows.CountAsync());
                Assert.Equal(await MeasureCount(StagedCount, countStaged), await newUnreadRows.CountAsync());
            }
        }
        // Only synthetic timing labels, never SQL, credentials or business data.
        var report = new { role, samples = 5, conversations, notifications = conversations * 8 + 4,
            originalEligibleNotificationIds, stagedConversationKeys,
            countOld = Summary(countOld), countExists = Summary(countExists), countScoped = Summary(countScoped),
            countStaged = Summary(countStaged),
            inboxOld = Summary(inboxOld), inboxNew = Summary(inboxNew),
            feedOld = Summary(feedOld), feedComposed = Summary(feedNew),
            feedConversationStaged = Summary(feedConversationStaged) };
        var destination = Path.Combine(postgres.RepositoryRoot, "artifacts", "unread-performance");
        Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(Path.Combine(destination, role + ".json"), JsonSerializer.Serialize(report));
    }

    private static async Task<Guid[]> Measure(Func<Task<Guid[]>> action, List<double> durations)
    {
        var timer = Stopwatch.StartNew();
        var result = await action();
        durations.Add(timer.Elapsed.TotalMilliseconds);
        return result;
    }

    private static double Median(List<double> values) => values.Order().ElementAt(values.Count / 2);

    // Retain the rejected correlated candidate only as a benchmark baseline, never production behavior.
    private static IQueryable<InternalConversationInboxRow> RejectedCorrelatedUnreadBaseline(
        InternalMailAreaAccessService areas, ApplicationDbContext db, UnreadPerformanceUser user)
    {
        var employee = user.EmployeeId!.Value;
        var company = user.CompanyId!.Value;
        var allowObserver = areas.CanObserveCompanyConversations;
        var links = db.QueryInternalMailNotificationLinks(company);
        var ownStates = db.NotificationUserStates.Where(s => s.UserId == employee && s.Notification.CompanyId == company);
        var reactions = InternalMailPersonalActivity.ReactionTopics;
        var unread = ownStates.Where(s => !s.IsRead && !s.IsArchived && !reactions.Contains(s.Notification.Topic));
        var notifiedIds = links.Where(l => l.ConversationId.HasValue && unread.Any(s => s.NotificationId == l.NotificationId))
            .Select(l => l.ConversationId!.Value);
        return areas.InboxConversations(false).Where(row =>
            ((allowObserver || row.IsMember) && notifiedIds.Contains(row.InternalConversationId)) ||
            (row.IsMember && !row.IsArchived &&
                (!row.IsMuted || row.Conversation.RelatedType == HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationTechnical ||
                 row.Conversation.RelatedType == HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationPricing ||
                 row.Conversation.RelatedType == HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.ConversationCustom) &&
                row.Conversation.Messages.Any(m => !m.IsDeleted && m.SenderEmployeeId != employee &&
                    (m.PayloadJson == null || !EF.Functions.JsonContains(m.PayloadJson, InternalMailMentions.AudienceMarker)) &&
                    m.ReadStates.Any(s => s.EmployeeId == employee && !s.IsRead) &&
                    !links.Any(l => l.ConversationId == row.InternalConversationId && l.MessageId == m.InternalMessageId &&
                        ownStates.Any(s => s.NotificationId == l.NotificationId)))));
    }
    private static async Task<int> MeasureCount(Func<Task<int>> action, List<double> durations)
    {
        var timer = Stopwatch.StartNew();
        var result = await action();
        durations.Add(timer.Elapsed.TotalMilliseconds);
        return result;
    }
    private static object Summary(List<double> values) => new
    {
        medianMs = Median(values), minMs = values.Min(), maxMs = values.Max()
    };

    private sealed class UnreadPerformanceUser(string role) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid UserId => Guid.Parse(Employee);
        public Guid? EmployeeId => Guid.Parse(Employee);
        public Guid? CompanyId => Guid.Parse(Company);
        public string? UserName => "synthetic";
        public string? Email => null;
        public IReadOnlyCollection<string> Roles => [role];
        public IReadOnlyCollection<string> Permissions => [];
        public bool HasExplicitPermissionSet => false;
        public bool IsInRole(string value) => value == role;
    }
}
