using System.Diagnostics;
using System.Text.Json;
using HRM.Application.Features.InternalMail.Queries.GetConversations;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Domain.Enums.InternalMailEnums;
using HRM.Domain.Enums.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageTheory]
    [InlineData("LabUser", true)]
    [InlineData("Developer", true)]
    [InlineData("SaleUser", true)]
    [InlineData("LabUser", false)]
    public async Task SampleUnreadApisPreserveScopeAndStablePagination(string role, bool storage)
        => await MeasureSampleAllAsync(role, storage, 64, compareUnread: true);

    private async Task CompareSampleUnreadAsync(ApplicationDbContext db, InternalMailAreaAccessService areas,
        GetInternalConversationsQueryHandler handler, UnreadPerformanceUser user,
        string role, bool storage, int eventCount, bool compareGroupsAndKeys = false)
    {
        // Synthetic read history outweighs unread threads. Include read winners with older unread events,
        // equal timestamps, archived states and a winner from another category (must not resurrect an older event).
        await postgres.ExecuteAsync("""
          ALTER TABLE notification.notifications
            ADD COLUMN IF NOT EXISTS "CreatedBy" uuid DEFAULT '00000000-0000-0000-0000-000000000001',
            ADD COLUMN IF NOT EXISTS "CreatedByNameSnapshot" text,
            ADD COLUMN IF NOT EXISTS severity integer DEFAULT 0,
            ADD COLUMN IF NOT EXISTS title text DEFAULT 'Synthetic',
            ADD COLUMN IF NOT EXISTS message text DEFAULT 'Synthetic',
            ADD COLUMN IF NOT EXISTS link text;
          UPDATE notification.notification_user_states SET is_read=true;
          UPDATE "InternalMail"."InternalMessageReadStates" SET "IsRead"=true;
          UPDATE notification.notification_user_states s SET is_read=false
          FROM notification.notifications n WHERE n.id=s.notification_id AND
            (n.payload_json->>'conversationId')::uuid IN
            (SELECT "InternalConversationId" FROM "InternalMail"."InternalConversations" WHERE "Subject" LIKE 'Synthetic%' ORDER BY "InternalConversationId" LIMIT 120);
          WITH latest AS (SELECT id,row_number() OVER (PARTITION BY payload_json->>'conversationId' ORDER BY created_date DESC,id DESC) AS r FROM notification.notifications)
          UPDATE notification.notification_user_states s SET is_read=true FROM latest l WHERE l.id=s.notification_id AND l.r=1;
          WITH latest AS (SELECT id,row_number() OVER (PARTITION BY payload_json->>'conversationId' ORDER BY created_date DESC,id DESC) AS r FROM notification.notifications)
          UPDATE notification.notifications n SET topic=36 FROM latest l WHERE l.id=n.id AND l.r=1 AND substring(n.id::text,1,1) IN ('a','b');
          WITH latest AS (SELECT id,max(created_date) OVER (PARTITION BY payload_json->>'conversationId') AS newest FROM notification.notifications)
          UPDATE notification.notifications n SET created_date=l.newest FROM latest l WHERE l.id=n.id AND substring(n.id::text,1,1)='c';
          UPDATE notification.notification_user_states s SET is_archived=true FROM notification.notifications n WHERE n.id=s.notification_id AND substring(n.id::text,1,2)='dd';
          INSERT INTO notification.notifications (id,company_id,payload_json,created_date,topic)
            VALUES ('ffffffff-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001','{}','2026-11-09',38);
          INSERT INTO notification.notification_user_states (notification_id,user_id,is_read)
            VALUES ('ffffffff-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001',false);
          DELETE FROM "InternalMail"."InternalConversationParticipants" WHERE "InternalConversationId" IN
            (SELECT "InternalConversationId" FROM "InternalMail"."InternalConversations" WHERE "Subject" LIKE 'Synthetic%' ORDER BY "InternalConversationId" LIMIT 8);
          ANALYZE;
          """);
        var samples = new InternalConversationSampleRequestInfoResolver(db);
        var quotes = new InternalConversationQuotationInfoResolver(db);
        var feed = new NotificationService(db, user, null!, null!, areas,
            new NotificationFeedConversationInfoResolver(db, areas, samples, quotes));
        if (compareGroupsAndKeys)
        {
            await CompareGroupsAndKeysAsync(db, areas, handler, user, role, storage, eventCount);
            return;
        }

        var unreadKeys = (await areas.UnreadConversationIdsAsync(InternalMailRelatedType.SampleRequest, default)).ToHashSet();
        var seenConversations = new HashSet<Guid>();
        var watch = Stopwatch.StartNew();
        var totalCount = 0;
        for (var page = 1; page <= 4; page++)
        {
            var request = new GetInternalConversationsQuery
            {
                RelatedType = InternalMailRelatedType.SampleRequest, Archived = false, UnreadOnly = true,
                PageNumber = page, PageSize = 50
            };
            var result = await handler.Handle(request, default);
            Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(await handler.Handle(request, default)));
            if (page == 1) totalCount = result.TotalCount;
            Assert.Equal(totalCount, result.TotalCount);
            Assert.All(result.Items, item =>
            {
                Assert.Contains(item.ConversationId, unreadKeys);
                Assert.False(item.IsArchived);
                Assert.True(seenConversations.Add(item.ConversationId));
            });
            if (result.Items.Count == 0) break;
        }
        Assert.NotEmpty(seenConversations);
        Assert.Equal(Math.Min(totalCount, 200), seenConversations.Count);

        var sampleTopics = NotificationTopicCatalog.GetTopics("sample_request", null);
        var seenNotifications = new HashSet<Guid>();
        Guid? cursor = null;
        DateTime? created = null;
        for (var page = 0; page < 6; page++)
        {
            Task<IReadOnlyList<NotificationDto>> ReadFeed() => feed.GetFeedAsync(take: 30, categoryCode: "sample_request",
                unreadOnly: true, afterId: cursor, afterCreated: created);
            var items = await ReadFeed();
            Assert.Equal(JsonSerializer.Serialize(items), JsonSerializer.Serialize(await ReadFeed()));
            Assert.All(items, item =>
            {
                Assert.Equal(user.CompanyId, item.CompanyId);
                Assert.Contains(item.Topic, sampleTopics);
                Assert.True(seenNotifications.Add(item.Id));
            });
            if (items.Count == 0) break;
            cursor = items[^1].Id;
            created = items[^1].CreatedDate;
        }
        Assert.NotEmpty(seenNotifications);
        watch.Stop();
        var path = Path.Combine(postgres.RepositoryRoot, "artifacts", "sample-unread-api-regression");
        Directory.CreateDirectory(path);
        await File.WriteAllTextAsync(Path.Combine(path, role + (storage ? ".canonical" : ".legacy") + ".json"),
            JsonSerializer.Serialize(new { role, storage, eventCount, synthetic = true, elapsedMs = watch.Elapsed.TotalMilliseconds,
                conversations = seenConversations.Count, notifications = seenNotifications.Count }));
    }
}
