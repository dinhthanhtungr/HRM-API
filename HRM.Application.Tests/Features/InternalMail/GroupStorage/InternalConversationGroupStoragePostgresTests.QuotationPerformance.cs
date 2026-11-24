using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.InternalMail.Queries.GetConversations;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageTheory]
    [InlineData("Developer")]
    [InlineData("LabUser")]
    [InlineData("SaleUser")]
    public async Task QuotationUnreadApisExecuteWithRealMetadataOnIsolatedPostgres(string role)
    {
        await PresidentBusinessSchemaAsync();
        await postgres.ExecuteAsync("""
            ALTER TABLE hr."Employees" ADD COLUMN IF NOT EXISTS "FullName" text DEFAULT 'Synthetic';
            CREATE TABLE IF NOT EXISTS "SampleRequests"."SampleRequestWorkflowEvents"
              ("CompanyId" uuid,"MessageId" uuid,"EventType" text);
            ALTER TABLE "SampleRequests"."SampleRequestWorkflowEvents" ADD COLUMN IF NOT EXISTS "NotificationId" uuid;
            ALTER TABLE "Customer"."Quotations"
              ADD COLUMN IF NOT EXISTS "CustomerId" uuid,
              ADD COLUMN IF NOT EXISTS "SaleEmployeeId" uuid,
              ADD COLUMN IF NOT EXISTS "ExternalId" text,
              ADD COLUMN IF NOT EXISTS "IsActive" boolean DEFAULT true;
            CREATE TABLE IF NOT EXISTS "Customer"."Customer"
              ("CustomerId" uuid,"CompanyId" uuid,"ExternalId" text,"CustomerName" text);
            INSERT INTO "Customer"."Customer" VALUES
              ('92000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','SYNTHETIC','Synthetic');
            UPDATE "InternalMail"."InternalConversations" SET "RelatedType"=16,"RelatedId"=NULL;
            INSERT INTO "Customer"."Quotations" ("QuotationId","CompanyId","CustomerId","SaleEmployeeId","ExternalId")
            SELECT md5('quote-' || i)::uuid,'10000000-0000-0000-0000-000000000001',
              '92000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001','SYNTHETIC'
              FROM generate_series(1,120) AS i;
            INSERT INTO "InternalMail"."InternalConversations"
              ("InternalConversationId","CompanyId","Subject","RelatedType","RelatedId","CreatedBy")
            SELECT md5('quote-root-' || i)::uuid,'10000000-0000-0000-0000-000000000001','Synthetic quote',12,
              md5('quote-' || i)::uuid,'00000000-0000-0000-0000-000000000001' FROM generate_series(1,120) AS i;
            INSERT INTO "InternalMail"."InternalConversations"
              ("InternalConversationId","CompanyId","Subject","RelatedType","RelatedId","CreatedBy")
            SELECT md5('quote-private-' || i)::uuid,'10000000-0000-0000-0000-000000000001','Synthetic private',18,
              md5('quote-root-' || i)::uuid,'00000000-0000-0000-0000-000000000001' FROM generate_series(1,60) AS i;
            INSERT INTO "InternalMail"."InternalConversations"
              ("InternalConversationId","CompanyId","Subject","RelatedType","CreatedBy")
            SELECT md5('other-root-' || i)::uuid,'10000000-0000-0000-0000-000000000001','Synthetic other',16,
              '00000000-0000-0000-0000-000000000001' FROM generate_series(1,400) AS i;
            INSERT INTO "InternalMail"."InternalConversationParticipants" ("InternalConversationId","EmployeeId")
            SELECT "InternalConversationId",'00000000-0000-0000-0000-000000000001'
              FROM "InternalMail"."InternalConversations" WHERE "Subject" LIKE 'Synthetic%';
            INSERT INTO "InternalMail"."InternalMessages"
              ("InternalMessageId","InternalConversationId","SenderEmployeeId","Body","SentAt")
            SELECT md5(c."InternalConversationId"::text || '-message')::uuid,c."InternalConversationId",
              '00000000-0000-0000-0000-000000000002','Synthetic',
              '2026-11-05'::timestamp + (row_number() OVER (ORDER BY c."InternalConversationId")) * interval '1 minute'
              FROM "InternalMail"."InternalConversations" c WHERE c."Subject" LIKE 'Synthetic%';
            INSERT INTO "InternalMail"."InternalMessageReadStates" ("InternalMessageId","EmployeeId")
            SELECT "InternalMessageId",'00000000-0000-0000-0000-000000000001'
              FROM "InternalMail"."InternalMessages" WHERE "Body"='Synthetic';
            ALTER TABLE notification.notifications
              ADD COLUMN IF NOT EXISTS "CreatedBy" uuid DEFAULT '00000000-0000-0000-0000-000000000001',
              ADD COLUMN IF NOT EXISTS "CreatedByNameSnapshot" text,
              ADD COLUMN IF NOT EXISTS severity integer DEFAULT 0,
              ADD COLUMN IF NOT EXISTS title text DEFAULT 'Synthetic',
              ADD COLUMN IF NOT EXISTS message text DEFAULT 'Synthetic',
              ADD COLUMN IF NOT EXISTS link text;
            INSERT INTO notification.notifications (id,company_id,payload_json,created_date,topic)
            SELECT md5(c."InternalConversationId"::text || '-event-' || j)::uuid,c."CompanyId",
              jsonb_build_object('conversationId',c."InternalConversationId",'messageId',md5(c."InternalConversationId"::text || '-message')::uuid),
              '2026-11-05'::timestamp + ((row_number() OVER (ORDER BY c."InternalConversationId")) * 10 + j) * interval '1 minute',
              CASE WHEN c."RelatedType" IN (12,18) THEN 36 ELSE 38 END
              FROM "InternalMail"."InternalConversations" c CROSS JOIN generate_series(1,8) AS j
              WHERE c."Subject" LIKE 'Synthetic%';
            INSERT INTO notification.notification_user_states (notification_id,user_id,is_read)
            SELECT n.id,'00000000-0000-0000-0000-000000000001',false FROM notification.notifications n
              WHERE n.created_date >= '2026-11-05';
            ANALYZE;
            """);
        await using var connection = await postgres.OpenAsync();
        var commands = new QuotationCommandMetrics();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection).AddInterceptors(commands).Options);
        db.Database.SetCommandTimeout(10);
        var user = new UnreadPerformanceUser(role);
        var areas = new InternalMailAreaAccessService(db, user);
        var samples = new InternalConversationSampleRequestInfoResolver(db);
        var quotes = new InternalConversationQuotationInfoResolver(db);
        var handler = new GetInternalConversationsQueryHandler(db, user, samples, quotes, areas);
        var feed = new NotificationService(db, user, null!, null!, areas,
            new NotificationFeedConversationInfoResolver(db, areas, samples, quotes));
        Task<HRM.Application.Commons.Pagination.PagedResult<HRM.Application.Features.InternalMail.Dtos.InternalConversationListItemDto>> Conversations()
            => handler.Handle(new GetInternalConversationsQuery
            {
                RelatedType = InternalMailRelatedType.Quotation, Archived = false,
                UnreadOnly = true, PageNumber = 1, PageSize = 50
            }, default);
        var firstConversations = await Conversations();
        var firstFeed = await feed.GetFeedAsync(take: 30, categoryCode: "quotation", unreadOnly: true);
        Assert.Equal(50, firstConversations.Items.Count);
        Assert.Equal(30, firstFeed.Count);
        Assert.All(firstConversations.Items, item => Assert.NotNull(item.QuotationInfo));
        Assert.All(firstFeed, item => Assert.NotNull(item.QuotationInfo));
        Assert.All(firstConversations.Items, item =>
        {
            Assert.Null(item.RepresentativeTopicCode);
            Assert.Null(item.RepresentativeEventGroupCode);
            Assert.Equal(1, item.UnreadCount);
            Assert.Equal("SYNTHETIC", item.QuotationInfo!.QuotationCode);
        });
        Assert.All(firstFeed, item => Assert.False(item.IsRead));
        var records = new List<object>();
        var threadId = firstConversations.Items[0].ConversationId;
        Task<List<InternalMessageDto>> MessagePage() => areas.Messages().Where(message => message.InternalConversationId == threadId)
            .OrderByDescending(message => message.SentAt).ThenByDescending(message => message.InternalMessageId).Take(30)
            .Select(message => new InternalMessageDto { MessageId = message.InternalMessageId, IsDeleted = message.IsDeleted })
            .ToListAsync();
        var expectedMessages = await MessagePage();
        await areas.AttachUnreadBadgesAsync(threadId, expectedMessages, default);
        var expectedBadges = JsonSerializer.Serialize(expectedMessages.Select(message => message.UnreadBadges));
        var messageTimes = new List<double>();
        var badgeTimes = new List<double>();
        for (var sample = 0; sample < 5; sample++)
        {
            var clock = Stopwatch.StartNew();
            var messages = await MessagePage();
            messageTimes.Add(clock.Elapsed.TotalMilliseconds);
            clock.Restart();
            await areas.AttachUnreadBadgesAsync(threadId, messages, default);
            badgeTimes.Add(clock.Elapsed.TotalMilliseconds);
            Assert.Equal(expectedMessages.Select(message => message.MessageId), messages.Select(message => message.MessageId));
            Assert.Equal(expectedBadges, JsonSerializer.Serialize(messages.Select(message => message.UnreadBadges)));
        }
        records.Add(new { kind = "thread-page-projection", samples = 5, medianMs = messageTimes.Order().ElementAt(2), minMs = messageTimes.Min(), maxMs = messageTimes.Max() });
        records.Add(new { kind = "thread-unread-badges", samples = 5, medianMs = badgeTimes.Order().ElementAt(2), minMs = badgeTimes.Min(), maxMs = badgeTimes.Max() });
        foreach (var kind in new[] { "conversations", "feed" })
        {
            var elapsed = new List<double>();
            var sqlRuns = new List<IReadOnlyList<QuotationCommandMetric>>();
            for (var sample = 0; sample < 5; sample++)
            {
                commands.Items.Clear();
                var timer = Stopwatch.StartNew();
                if (kind == "conversations")
                {
                    var result = await Conversations();
                    Assert.Equal(firstConversations.Items.Select(i => i.ConversationId), result.Items.Select(i => i.ConversationId));
                }
                else
                {
                    var result = await feed.GetFeedAsync(take: 30, categoryCode: "quotation", unreadOnly: true);
                    Assert.Equal(firstFeed.Select(i => i.Id), result.Select(i => i.Id));
                }
                elapsed.Add(timer.Elapsed.TotalMilliseconds);
                sqlRuns.Add(commands.Items.ToArray());
            }
            records.Add(new { kind, samples = 5, medianMs = elapsed.Order().ElementAt(2),
                minMs = elapsed.Min(), maxMs = elapsed.Max(), commands = sqlRuns });
        }
        var destination = Path.Combine(postgres.RepositoryRoot, "artifacts", "quotation-unread-performance");
        Directory.CreateDirectory(destination);
        await File.WriteAllTextAsync(Path.Combine(destination, role + ".json"), JsonSerializer.Serialize(new
        {
            role, quoteRoots = 120, privateChildren = 60, unrelatedRoots = 400, records
        }));
        var pageIds = firstConversations.Items.Select(i => i.ConversationId).ToArray();
        var links = db.QueryInternalMailNotificationLinks(user.CompanyId!.Value);
        var visibleMessages = areas.Messages().Where(m => pageIds.Contains(m.InternalConversationId) && !m.IsDeleted);
        var chatTopics = HRM.Domain.Enums.Notifications.NotificationTopicCatalog.GetTopics(null, "message")
            .Concat(InternalMailPersonalActivity.ReactionTopics).ToArray();
        var oldIconCandidates = areas.Notifications(db.NotificationUserStates.Select(s => s.Notification))
            .Where(n => !chatTopics.Contains(n.Topic) && links.Any(l => l.NotificationId == n.Id && l.MessageId.HasValue &&
                visibleMessages.Any(m => m.InternalConversationId == l.ConversationId && m.InternalMessageId == l.MessageId)))
            .Select(n => new { n.Id, n.CreatedDate, n.Topic }).Distinct();
        var oldIcons = await (from notification in oldIconCandidates
                              join link in links on notification.Id equals link.NotificationId
                              where link.ConversationId.HasValue && pageIds.Contains(link.ConversationId.Value)
                              group notification by link.ConversationId.Value into events
                              select new { ConversationId = events.Key,
                                  Topic = events.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Select(n => n.Topic).First() })
            .ToDictionaryAsync(e => e.ConversationId, e => e.Topic);
        var newIcons = await areas.VisibleMessageBusinessEvents(pageIds).GroupBy(n => n.ConversationId)
            .Select(events => events.OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).First())
            .ToDictionaryAsync(e => e.ConversationId, e => e.Topic);
        Assert.Equal(oldIcons.OrderBy(i => i.Key), newIcons.OrderBy(i => i.Key));

        var ownInbox = db.Notifications.AsNoTracking().Where(n => n.CompanyId == user.CompanyId &&
            n.UserStates.Any(s => s.UserId == user.EmployeeId && !s.IsArchived) && !InternalMailPersonalActivity.ReactionTopics.Contains(n.Topic));
        var baselineUnreadKeys = await areas.UnreadHubConversationIds(ownInbox).ToArrayAsync();
        var baselineVisible = areas.UnreadHubEventsForConversations(areas.Notifications(ownInbox), ownInbox, baselineUnreadKeys);
        var quotationTopics = HRM.Domain.Enums.Notifications.NotificationTopicCatalog.GetTopics("quotation", null);
        var baselineFeed = await areas.LatestHubEvents(baselineVisible, ownInbox, null)
            .Where(n => quotationTopics.Contains(n.Topic) && n.CreatedDate >= HRM.Domain.Enums.Notifications.NotificationTopicCategoryRules.CurrentDataStartDate)
            .OrderByDescending(n => n.CreatedDate).ThenByDescending(n => n.Id).Take(30)
            .Select(n => new { n.Id, n.Topic, n.Title, n.Message, n.CreatedDate }).ToArrayAsync();
        Assert.Equal(baselineFeed.Select(n => (n.Id, n.Topic, n.Title, n.Message, n.CreatedDate)),
            firstFeed.Select(n => (n.Id, n.Topic, n.Title, n.Message, n.CreatedDate)));
    }

    private sealed record QuotationCommandMetric(int Sequence, double Milliseconds, int SqlCharacters, string Shape);

    private sealed class QuotationCommandMetrics : DbCommandInterceptor
    {
        public List<QuotationCommandMetric> Items { get; } = [];
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData data,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            // Labels and aggregate timing only, never SQL text, parameters, credentials or IDs.
            var shape = command.CommandText.StartsWith("SELECT count", StringComparison.OrdinalIgnoreCase) ? "count" :
                command.CommandText.Contains("AS \"QuotationCode\"") ? "quotation-metadata" :
                command.CommandText.Contains("AS \"LastMessageBody\"") ? "conversation-page" :
                command.CommandText.Contains("AS \"IsRead\"") ? "notification-page" : "other";
            Items.Add(new(Items.Count + 1, data.Duration.TotalMilliseconds, command.CommandText.Length, shape));
            return ValueTask.FromResult(result);
        }
    }
}
