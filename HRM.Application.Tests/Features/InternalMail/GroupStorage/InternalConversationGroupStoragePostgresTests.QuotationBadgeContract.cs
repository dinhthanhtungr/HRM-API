using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Features.InternalMail.Commands.MarkConversationRead;
using HRM.Application.Features.InternalMail.Services;
using HRM.Application.Features.Notifications.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageTheory]
    [InlineData("President", true)]
    [InlineData("Developer", true)]
    [InlineData("LabUser", true)]
    [InlineData("SaleUser", true)]
    [InlineData("President", false)]
    [InlineData("SaleUser", false)]
    public async Task QuotationBadgesCountOnlyDistinctVisibleMessages(string role, bool storage)
    {
        await PresidentBusinessSchemaAsync();
        await postgres.ExecuteAsync("""
            ALTER TABLE notification.notifications ADD COLUMN IF NOT EXISTS "CreatedBy" uuid,
                ADD COLUMN IF NOT EXISTS "CreatedByNameSnapshot" text, ADD COLUMN IF NOT EXISTS severity integer DEFAULT 0,
                ADD COLUMN IF NOT EXISTS title text DEFAULT 'Badge', ADD COLUMN IF NOT EXISTS message text DEFAULT '',
                ADD COLUMN IF NOT EXISTS link text;
            CREATE TABLE IF NOT EXISTS "SampleRequests"."SampleRequestWorkflowEvents"
                ("CompanyId" uuid,"MessageId" uuid,"EventType" text,"NotificationId" uuid);
            ALTER TABLE hr."Employees" ADD COLUMN IF NOT EXISTS "FullName" text DEFAULT 'Synthetic';
            DELETE FROM notification.notification_user_states;
            DELETE FROM notification.notification_recipients;
            DELETE FROM notification.notifications;
            UPDATE "InternalMail"."InternalMessageReadStates" SET "IsRead"=true;
            INSERT INTO "InternalMail"."InternalConversations" ("InternalConversationId","CompanyId","Subject","RelatedType","CreatedBy")
                SELECT md5('badge-root-'||i)::uuid,'10000000-0000-0000-0000-000000000001','Badge '||i,
                    CASE WHEN i=6 THEN 16 ELSE 12 END,'00000000-0000-0000-0000-000000000002'::uuid FROM generate_series(1,6) i;
            INSERT INTO "InternalMail"."InternalConversationParticipants" ("InternalConversationId","EmployeeId")
                SELECT md5('badge-root-'||i)::uuid,'00000000-0000-0000-0000-000000000001' FROM generate_series(1,6) i;
            INSERT INTO "InternalMail"."InternalMessages"
                ("InternalMessageId","InternalConversationId","SenderEmployeeId","Body","SentAt","IsDeleted")
                SELECT md5('badge-message-'||i)::uuid,md5('badge-root-'||i)::uuid,
                    CASE WHEN i=5 THEN '00000000-0000-0000-0000-000000000001'::uuid
                    ELSE '00000000-0000-0000-0000-000000000002'::uuid END,'Badge','2026-11-05',i=4 FROM generate_series(1,6) i;
            INSERT INTO "InternalMail"."InternalMessageReadStates" ("InternalMessageId","EmployeeId","IsRead")
                SELECT md5('badge-message-'||i)::uuid,'00000000-0000-0000-0000-000000000001',i IN (2,3) FROM generate_series(1,6) i;
            INSERT INTO notification.notifications (id,company_id,payload_json,created_date,topic)
                SELECT md5('badge-valid-'||i||'-'||j)::uuid,'10000000-0000-0000-0000-000000000001',
                    jsonb_build_object('ConversationId',md5('badge-root-'||i)::uuid,'MessageId',
                        md5('badge-message-'||CASE WHEN i=3 THEN 1 ELSE i END)::uuid),
                    '2026-11-05',CASE WHEN i=6 THEN 38 ELSE 36 END
                FROM generate_series(1,6) i CROSS JOIN generate_series(1,2) j WHERE i<>2;
            INSERT INTO notification.notifications (id,company_id,payload_json,created_date,topic)
                SELECT md5('badge-hidden-'||i||'-'||j)::uuid,'10000000-0000-0000-0000-000000000001',
                    jsonb_build_object('conversationId',md5('badge-root-'||i)::uuid),'2026-11-06',48
                FROM generate_series(1,2) i CROSS JOIN generate_series(1,8) j;
            INSERT INTO notification.notification_user_states (notification_id,user_id,is_read,is_archived)
                SELECT id,'00000000-0000-0000-0000-000000000001',false,false FROM notification.notifications;
            UPDATE notification.notifications SET "CreatedBy"='00000000-0000-0000-0000-000000000002' WHERE "CreatedBy" IS NULL;
            """);
        if(storage) await postgres.ExecuteAsync("""
            INSERT INTO "InternalMail"."InternalConversationGroups" ("ConversationId","CompanyId","RootConversationId","GroupType","IsActive")
                SELECT "InternalConversationId","CompanyId","InternalConversationId",0,"IsActive"
                FROM "InternalMail"."InternalConversations" WHERE "Subject" LIKE 'Badge %';
            """);
        await using var connection=await postgres.OpenAsync();
        await using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var user=new UnreadPerformanceUser(role);
        var areas=new InternalMailAreaAccessService(db,user,Options.Create(new InternalMailGroupStorageOptions { Enabled=storage }));
        var service=new NotificationService(db,user,null!,null!,areas,new NotificationFeedConversationInfoResolver(db,areas,
            new InternalConversationSampleRequestInfoResolver(db),new InternalConversationQuotationInfoResolver(db)));
        var ids=await db.InternalConversations.Where(c=>c.Subject.StartsWith("Badge "))
            .OrderBy(c=>c.Subject).Select(c=>c.InternalConversationId).ToListAsync();
        var counts=await areas.NotificationUnreadCounts(ids.ToArray()).ToDictionaryAsync(x=>x.ConversationId,x=>x.UnreadCount);
        Assert.Equal(1,counts.GetValueOrDefault(ids[0]));
        for(var i=1;i<5;i++) Assert.Equal(0,counts.GetValueOrDefault(ids[i]));
        Assert.Equal(2,counts.GetValueOrDefault(ids[5])); // Other modules retain their count policy.
        var keys=await areas.UnreadConversationIds().Distinct().ToArrayAsync();
        Assert.Equal(keys.Order(), (await areas.UnreadConversationIdsAsync(null, default)).Order());
        var quoteKeys=await areas.UnreadConversationIds(HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.Quotation).Distinct().ToArrayAsync();
        Assert.Equal(quoteKeys.Order(), (await areas.UnreadConversationIdsAsync(HRM.Domain.Enums.InternalMailEnums.InternalMailRelatedType.Quotation, default)).Order());
        Assert.Contains(ids[0],keys);
        for(var i=1;i<5;i++) Assert.DoesNotContain(ids[i],keys);
        var summary=await service.GetUnreadSummaryAsync();
        Assert.Equal(3,summary.TotalUnread);
        Assert.Equal(1,summary.Categories.Single(c=>c.CategoryCode=="quotation").UnreadCount);
        Assert.Equal(summary.TotalUnread,await service.GetUnreadCountAsync());
        var inbox=db.Notifications.Where(n=>n.CompanyId==user.CompanyId && n.UserStates.Any(s=>s.UserId==user.EmployeeId));
        var feedKeys=await areas.UnreadHubConversationIds(inbox).ToArrayAsync();
        Assert.Contains(ids[0],feedKeys);
        for(var i=1;i<5;i++) Assert.DoesNotContain(ids[i],feedKeys);
        var visibleFeed=await service.GetFeedAsync(take:30,categoryCode:"quotation",unreadOnly:true);
        Assert.NotEmpty(visibleFeed);
        Assert.All(visibleFeed,item=> {
            Assert.Equal(ids[0],item.ConversationId);
            Assert.Equal(1,item.ConversationUnreadCount);
        });
        // Simulate persisted read receipt for linked notifications, leaving all invisible approval events unread.
        await postgres.ExecuteAsync("""
            UPDATE notification.notification_user_states s SET is_read=true FROM notification.notifications n
                WHERE n.id=s.notification_id AND n.topic=36;
            UPDATE "InternalMail"."InternalMessageReadStates" SET "IsRead"=true;
            """);
        Assert.Equal(2,(await service.GetUnreadSummaryAsync()).TotalUnread);
        Assert.DoesNotContain(ids[0],await areas.UnreadConversationIds().Distinct().ToArrayAsync());
        Assert.DoesNotContain(ids[0],await areas.UnreadHubConversationIds(inbox).ToArrayAsync());
        Assert.Empty(await areas.NotificationUnreadCounts([ids[0]]).Where(c=>c.UnreadCount>0).ToArrayAsync());
        Assert.Empty(await service.GetFeedAsync(take:30,categoryCode:"quotation",unreadOnly:true));
        Assert.Equal(16,await db.NotificationUserStates.CountAsync(s=>!s.IsRead && s.Notification.Topic==HRM.Domain.Enums.Notifications.TopicNotifications.QuotationPricingApproved));
        // Verify provider-neutral implementation agrees with optimized SQL for every unread state.
        var reference=QuotationBadgeNotificationQuery.Suppressed(
            (await db.InternalConversations.ToListAsync()).AsQueryable(),(await db.InternalMessages.ToListAsync()).AsQueryable(),
            (await db.Notifications.ToListAsync()).AsQueryable(),(await db.NotificationUserStates.ToListAsync()).AsQueryable(),
            (await db.QueryInternalMailNotificationLinks(Guid.Parse(Company)).ToListAsync()).AsQueryable(),Guid.Parse(Company),Guid.Parse(Employee)).OrderBy(x=>x).ToArray();
        Assert.Equal(reference,await db.QueryQuotationBadgeSuppressedNotificationIds(Guid.Parse(Company),Guid.Parse(Employee)).OrderBy(x=>x).ToArrayAsync());
    }
}
