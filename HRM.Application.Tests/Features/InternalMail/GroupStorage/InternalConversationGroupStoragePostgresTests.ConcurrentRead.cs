using System.Diagnostics;
using System.Text.Json;
using HRM.Application.Features.InternalMail.Commands.MarkConversationRead;
using HRM.Application.Features.InternalMail.Services;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Tests.Features.InternalMail.GroupStorage;

public sealed partial class InternalConversationGroupStoragePostgresTests
{
    [PostgresGroupStorageFact]
    public async Task ConcurrentReadsAtSameBoundaryKeepLaterMessageUnread()
    {
        await PresidentBusinessSchemaAsync();
        await postgres.ExecuteAsync($$"""
            UPDATE "InternalMail"."InternalConversations" SET "RelatedType"=16,"RelatedId"=NULL WHERE "InternalConversationId"='{{Root}}';
            INSERT INTO "InternalMail"."InternalMessages" ("InternalMessageId","InternalConversationId","SenderEmployeeId","Body","SentAt")
              SELECT md5('lock-msg-' || i)::uuid,'{{Root}}','{{MissingPresident}}','Lock fixture',
                '2026-11-01'::timestamp + i * interval '1 second' FROM generate_series(1,20001) i;
            INSERT INTO "InternalMail"."InternalMessageReadStates" ("InternalMessageId","EmployeeId","IsRead")
              SELECT md5('lock-msg-' || i)::uuid,'{{Employee}}',i <= 19000 FROM generate_series(1,20001) i;
            INSERT INTO notification.notifications (id,company_id,payload_json,created_date,topic)
              SELECT md5('lock-event-' || i)::uuid,'{{Company}}',
                jsonb_build_object('conversationId','{{Root}}','messageId',md5('lock-msg-' || i)::uuid),
                '2026-11-02',38 FROM generate_series(19001,20001) i;
            INSERT INTO notification.notification_user_states (notification_id,user_id)
              SELECT md5('lock-event-' || i)::uuid,'{{Employee}}' FROM generate_series(19001,20001) i;
            ANALYZE;
            """);
        var boundary = await ScalarAsync<Guid>("SELECT md5('lock-msg-20000')::uuid");
        async Task<string> Read()
        {
            using var activity = new Activity("read-contention").Start();
            activity.SetTag(NotificationDiagnosticTags.ThreadTiming, "enabled");
            await using var connection = await postgres.OpenAsync();
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connection).Options);
            var user = new UnreadPerformanceUser("Developer");
            var handler = new MarkInternalConversationReadCommandHandler(db, user, new ReadPerformanceClock(),
                new InternalMailAreaAccessService(db, user));
            var result = await handler.Handle(new() { ConversationId = Guid.Parse(Root), ThroughMessageId = boundary }, default);
            Assert.True(result.Success);
            return Assert.IsType<string>(activity.GetTagItem(NotificationDiagnosticTags.ServerTiming));
        }

        var single = await Read();
        await postgres.ExecuteAsync($$"""
            UPDATE notification.notification_user_states SET is_read=false,read_date=NULL WHERE user_id='{{Employee}}';
            UPDATE "InternalMail"."InternalMessageReadStates" SET "IsRead"=false,"ReadAt"=NULL
              WHERE "EmployeeId"='{{Employee}}' AND "InternalMessageId" IN
              (SELECT md5('lock-msg-' || i)::uuid FROM generate_series(19001,20001) i);
            """);
        await using var locker = await postgres.OpenAsync();
        await using var tx = await locker.BeginTransactionAsync();
        await using var hold = locker.CreateCommand();
        hold.Transaction = tx;
        hold.CommandText = "SELECT notification_id FROM notification.notification_user_states WHERE notification_id=md5('lock-event-19500')::uuid FOR UPDATE";
        Assert.NotNull(await hold.ExecuteScalarAsync());
        var first = Read();
        var second = Read();
        var observed = 0L;
        var watch = Stopwatch.StartNew();
        while (observed < 2 && watch.Elapsed < TimeSpan.FromSeconds(15))
        {
            observed = await ScalarAsync<long>("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND cardinality(pg_blocking_pids(pid))>0");
            if (observed < 2) await Task.Delay(10);
        }
        await tx.RollbackAsync();
        var concurrent = await Task.WhenAll(first, second);
        Assert.Equal(2L, observed);
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM notification.notification_user_states WHERE NOT is_read AND notification_id IN (SELECT md5('lock-event-' || i)::uuid FROM generate_series(19001,20001) i)"));
        Assert.True(await ScalarAsync<bool>("SELECT NOT is_read FROM notification.notification_user_states WHERE notification_id=md5('lock-event-20001')::uuid"));
        var directory = Path.Combine(postgres.RepositoryRoot, "artifacts", "notification-read-performance");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "concurrent-read.json"),
            JsonSerializer.Serialize(new { history = 20001, acknowledged = 1000, blockedSessions = observed, single, concurrent }));
    }
}

