using System.Data.Common;
using HRM.Application.Features.InternalMail.Commands.MarkConversationRead;
using HRM.Application.Features.InternalMail.Services;
using HRM.Domain.Enums.Notifications;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageFact]
    public async Task ReadSnapshotDoesNotConsumeMessageInsertedDuringAcknowledgementEvenIfBackdated()
    {
        await PresidentBusinessSchemaAsync();
        await postgres.ExecuteAsync($$"""
            UPDATE "InternalMail"."InternalConversations" SET "RelatedType"=16,"RelatedId"=NULL;
            INSERT INTO "InternalMail"."InternalMessages"
              ("InternalMessageId","InternalConversationId","SenderEmployeeId","Body","SentAt")
              VALUES ('a0000000-0000-0000-0000-000000000001','{{Root}}','{{MissingPresident}}','Boundary','2026-11-01'),
                     ('a0000000-0000-0000-0000-000000000002','{{Root}}','{{MissingPresident}}','Previous','2026-10-31');
            INSERT INTO "InternalMail"."InternalMessageReadStates" ("InternalMessageId","EmployeeId")
              VALUES ('a0000000-0000-0000-0000-000000000001','{{Employee}}'),
                     ('a0000000-0000-0000-0000-000000000002','{{Employee}}');
            INSERT INTO notification.notifications (id,company_id,payload_json,created_date,topic)
              SELECT md5('race-event-' || i)::uuid, '{{Company}}',
                     jsonb_build_object('conversationId','{{Root}}','messageId',
                         'a0000000-0000-0000-0000-' || lpad(i::text,12,'0')), '2026-11-01',38
              FROM generate_series(1,2) i;
            INSERT INTO notification.notification_user_states (notification_id,user_id)
              SELECT md5('race-event-' || i)::uuid,'{{Employee}}' FROM generate_series(1,2) i;
            """);
        var lateInsert = new ReadRaceInterceptor(() => postgres.ExecuteAsync($$"""
            INSERT INTO "InternalMail"."InternalMessages"
              ("InternalMessageId","InternalConversationId","SenderEmployeeId","Body","SentAt")
              VALUES ('90000000-0000-0000-0000-000000000001','{{Root}}','{{MissingPresident}}','Arrived after snapshot','2026-11-01');
            INSERT INTO "InternalMail"."InternalMessageReadStates" ("InternalMessageId","EmployeeId")
              VALUES ('90000000-0000-0000-0000-000000000001','{{Employee}}');
            INSERT INTO notification.notifications (id,company_id,payload_json,created_date,topic)
              VALUES ('f0000000-0000-0000-0000-000000000001','{{Company}}',
                '{"conversationId":"{{Root}}","messageId":"90000000-0000-0000-0000-000000000001"}','2026-11-01',38);
            INSERT INTO notification.notification_user_states (notification_id,user_id)
              VALUES ('f0000000-0000-0000-0000-000000000001','{{Employee}}');
            """));
        await using var connection = await postgres.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection).AddInterceptors(lateInsert).Options);
        var user = new UnreadPerformanceUser("Developer");
        var result = await new MarkInternalConversationReadCommandHandler(db, user, new ReadPerformanceClock(),
            new InternalMailAreaAccessService(db, user)).Handle(new()
            {
                ConversationId = Guid.Parse(Root),
                ThroughMessageId = Guid.Parse("a0000000-0000-0000-0000-000000000001")
            }, default);
        Assert.True(result.Success);
        Assert.True(lateInsert.Inserted);
        Assert.Equal(2, await db.InternalMessageReadStates.CountAsync(s => s.IsRead &&
            (s.Message.Body == "Boundary" || s.Message.Body == "Previous")));
        var counts = await new InternalMailAreaAccessService(db, user)
            .PageNotificationUnreadCountsAsync([Guid.Parse(Root)], default);
        Assert.Equal(1, counts.GetValueOrDefault(Guid.Parse(Root)));
        Assert.False(await db.InternalMessageReadStates.Where(s => s.Message.Body == "Arrived after snapshot")
            .Select(s => s.IsRead).SingleAsync());
        Assert.False(await db.NotificationUserStates.Where(s => s.NotificationId == Guid.Parse("f0000000-0000-0000-0000-000000000001"))
            .Select(s => s.IsRead).SingleAsync());
    }

    private sealed class ReadRaceInterceptor(Func<Task> insert) : DbCommandInterceptor
    {
        public bool Inserted;
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Inserted && command.CommandText.Contains("INSERT INTO \"InternalMail\".\"InternalMessageReadStates\""))
            {
                Inserted = true;
                await insert();
            }
            return result;
        }
    }

    [PostgresGroupStorageTheory]
    [InlineData("SaleUser", 5, false)]
    [InlineData("LabUser", 5, false)]
    [InlineData("President", 12, false)]
    [InlineData("Developer", 12, true)]
    public async Task BulkReadMatchesLegacyForHistoricalPayloadsAndObserver(string role, int relatedType, bool observer)
    {
        string? expected = null;
        foreach (var useBaseline in new[] { true, false })
        {
            await postgres.ResetAsync();
            await PresidentBusinessSchemaAsync();
            await postgres.ExecuteAsync($$"""
                UPDATE "InternalMail"."InternalConversations" SET "RelatedType"={{relatedType}}
                  WHERE "InternalConversationId"='{{Root}}';
                INSERT INTO "InternalMail"."InternalMessages"
                  ("InternalMessageId","InternalConversationId","SenderEmployeeId","Body","SentAt")
                SELECT md5('compat-' || i)::uuid,'{{Root}}','{{MissingPresident}}','Synthetic',
                  '2026-11-01'::timestamp + i * interval '1 second' FROM generate_series(1,3) i;
                INSERT INTO "InternalMail"."InternalMessageReadStates" ("InternalMessageId","EmployeeId","IsRead","ReadAt")
                SELECT md5('compat-' || i)::uuid,'{{Employee}}',i=1,
                  CASE WHEN i=1 THEN '2026-11-01'::timestamp ELSE NULL END FROM generate_series(1,3) i;
                INSERT INTO notification.notifications (id,company_id,payload_json,created_date,topic)
                SELECT md5('compat-notification-' || i)::uuid,'{{Company}}',
                  CASE i
                    WHEN 1 THEN jsonb_build_object('ConversationId','{{Root}}','MessageId',md5('compat-1')::uuid)
                    WHEN 2 THEN jsonb_build_object('conversationId','{{Root}}','messageId',md5('compat-2'))
                    WHEN 3 THEN jsonb_build_object('conversationId','{{Root}}','messageId','invalid')
                    WHEN 4 THEN jsonb_build_object('conversationId','{{Root}}')
                    WHEN 5 THEN jsonb_build_object('conversationId','{{Root}}','messageId',md5('compat-3')::uuid)
                    WHEN 6 THEN jsonb_build_object('conversationId','{{Root}}','messageId',NULL)
                    ELSE jsonb_build_object('conversationId','{{Root}}','MessageId',NULL,'messageId','invalid') END,
                  '2026-11-02',{{(int)TopicNotifications.QuotationPricingApproved}}
                  FROM generate_series(1,7) i;
                INSERT INTO notification.notification_user_states (notification_id,user_id)
                  SELECT md5('compat-notification-' || i)::uuid,'{{Employee}}' FROM generate_series(1,7) i;
                """);
            if (observer)
                await postgres.ExecuteAsync($$"""
                    DELETE FROM "InternalMail"."InternalConversationParticipants"
                    WHERE "InternalConversationId"='{{Root}}' AND "EmployeeId"='{{Employee}}';
                    """);
            await using var connection = await postgres.OpenAsync();
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
            var user = new UnreadPerformanceUser(role);
            var areas = new InternalMailAreaAccessService(db, user);
            var command = new MarkInternalConversationReadCommand
            {
                ConversationId = Guid.Parse(Root),
                ThroughMessageId = await db.InternalMessages.Where(m => m.Body == "Synthetic")
                    .OrderBy(m => m.SentAt).Skip(1).Select(m => m.InternalMessageId).FirstAsync()
            };
            var result = useBaseline
                ? await new Baselines.BaselineMarkConversationReadHandler(db, user, new ReadPerformanceClock(), areas).Handle(command, default)
                : await new MarkInternalConversationReadCommandHandler(db, user, new ReadPerformanceClock(), areas).Handle(command, default);
            Assert.True(result.Success);
            var state = await ScalarAsync<string>("""
                SELECT jsonb_build_object(
                    'notifications',(SELECT jsonb_agg(row_to_json(s) ORDER BY notification_id,user_id) FROM notification.notification_user_states s),
                    'messages',(SELECT jsonb_agg(row_to_json(s) ORDER BY "InternalMessageId","EmployeeId") FROM "InternalMail"."InternalMessageReadStates" s),
                    'participants',(SELECT jsonb_agg(row_to_json(s) ORDER BY "InternalConversationId","EmployeeId") FROM "InternalMail"."InternalConversationParticipants" s))::text
                """);
            if (useBaseline) expected = state;
            else Assert.Equal(expected, state);
            Assert.Equal(3, await db.NotificationUserStates.CountAsync(s => !s.IsRead && s.Notification.CreatedDate == new DateTime(2026, 11, 2)));
        }
    }
}
